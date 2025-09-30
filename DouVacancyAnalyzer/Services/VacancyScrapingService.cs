using DouVacancyAnalyzer.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Net;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;

namespace DouVacancyAnalyzer.Services;

public class VacancyScrapingService : IVacancyScrapingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VacancyScrapingService> _logger;
    private readonly ScrapingSettings _settings;

    public VacancyScrapingService(
        HttpClient httpClient,
        ILogger<VacancyScrapingService> logger,
        IOptions<ScrapingSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
        
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
    }

    public async Task<List<Vacancy>> GetVacanciesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🚀 Starting DOU.ua scraping with Selenium to get ALL vacancies [NEW VERSION WITH FULL DESCRIPTION LOADING]");
        
        var vacancies = new List<Vacancy>();
        IWebDriver? driver = null;
        
        try
        {
            new DriverManager().SetUpDriver(new ChromeConfig());
            var options = new ChromeOptions();
            options.AddArguments("--headless");
            options.AddArguments("--no-sandbox");
            options.AddArguments("--disable-dev-shm-usage");
            options.AddArguments("--disable-gpu");
            options.AddArguments("--window-size=1920,1080");
            options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            
            driver = new ChromeDriver(options);
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
            
            _logger.LogInformation("Loading DOU.ua page with .NET category");
            driver.Navigate().GoToUrl(_settings.BaseUrl);
            
            wait.Until(d => d.FindElements(By.CssSelector("li.l-vacancy")).Count > 0);
            
            var previousCount = 0;
            var attempts = 0;
            var maxAttempts = 20;
            
            _logger.LogInformation("Starting to collect all vacancies by clicking 'Load More'");
            
            while (attempts < maxAttempts)
            {
                var currentElements = driver.FindElements(By.CssSelector("li.l-vacancy"));
                var currentCount = currentElements.Count;
                
                _logger.LogInformation("Attempt {Attempt}: found {Count} vacancies", attempts + 1, currentCount);
                
                if (currentCount == previousCount)
                {
                    _logger.LogInformation("No new vacancies loaded, stopping");
                    break;
                }
                
                try
                {
                    var moreButton = driver.FindElement(By.XPath("//a[contains(text(), 'Більше вакансій')]"));
                    
                    if (moreButton.Displayed && moreButton.Enabled)
                    {
                        _logger.LogInformation("Clicking 'Load More' button");
                        
                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", moreButton);
                        await Task.Delay(1000, cancellationToken);
                        
                        moreButton.Click();
                        
                        await Task.Delay(3000, cancellationToken);
                        
                        try
                        {
                            wait.Until(d => d.FindElements(By.CssSelector("li.l-vacancy")).Count > currentCount);
                        }
                        catch (WebDriverTimeoutException)
                        {
                            _logger.LogWarning("Timeout waiting for new vacancies to load");
                        }
                    }
                    else
                    {
                        _logger.LogInformation("'Load More' button is not available");
                        break;
                    }
                }
                catch (NoSuchElementException)
                {
                    _logger.LogInformation("'Load More' button not found - reached end of list");
                    break;
                }
                
                previousCount = currentCount;
                attempts++;
                
                if (cancellationToken.IsCancellationRequested)
                    break;
            }
            
            var finalHtml = driver.PageSource;
            var doc = new HtmlDocument();
            doc.LoadHtml(finalHtml);
            
            var vacancyElements = doc.DocumentNode.SelectNodes("//li[contains(@class, 'l-vacancy')]") ?? 
                                 Enumerable.Empty<HtmlNode>();
            
            _logger.LogInformation("Parsing {Count} vacancy elements from final page", vacancyElements.Count());
            
            foreach (var element in vacancyElements)
            {
                try
                {
                    var vacancy = ParseVacancyElement(element);
                    if (vacancy != null)
                    {
                        // Load full description from vacancy page using Selenium
                        if (!string.IsNullOrEmpty(vacancy.Url))
                        {
                            _logger.LogInformation("📥 Loading full description for: {Title} from {Url}", vacancy.Title, vacancy.Url);
                            try
                            {
                                driver.Navigate().GoToUrl(vacancy.Url);

                                // Wait for page to load - try multiple selectors
                                try
                                {
                                    wait.Until(d =>
                                        d.FindElements(By.CssSelector("div.text")).Count > 0 ||
                                        d.FindElements(By.CssSelector("article")).Count > 0 ||
                                        d.FindElements(By.TagName("article")).Count > 0 ||
                                        d.FindElements(By.ClassName("vacancy-section")).Count > 0);
                                }
                                catch (WebDriverTimeoutException)
                                {
                                    _logger.LogWarning("Timeout waiting for description to load on {Url}", vacancy.Url);
                                }

                                await Task.Delay(2000, cancellationToken); // Give more time for content to render

                                var pageHtml = driver.PageSource;
                                var pageDoc = new HtmlDocument();
                                pageDoc.LoadHtml(pageHtml);

                                // Strategy: Find the vacancy title (h1) and get all content from there
                                // Try multiple approaches to find the main content
                                var descriptionNode = pageDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'l-vacancy')]") ??
                                                     pageDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'b-vacancy')]") ??
                                                     pageDoc.DocumentNode.SelectSingleNode("//main") ??
                                                     pageDoc.DocumentNode.SelectSingleNode("//article");

                                // If no specific container found, get everything after h1 (vacancy title)
                                if (descriptionNode == null)
                                {
                                    var h1Node = pageDoc.DocumentNode.SelectSingleNode("//h1");
                                    if (h1Node != null)
                                    {
                                        // Get parent that contains h1 and the rest of the content
                                        descriptionNode = h1Node.ParentNode;
                                    }
                                }

                                if (descriptionNode != null)
                                {
                                    // Remove unnecessary elements (navigation, sidebar, footer, ads, etc.)
                                    var unwantedNodes = descriptionNode.SelectNodes(
                                        ".//script | .//style | .//nav | .//header[contains(@class, 'g-header')] | " +
                                        ".//footer | .//aside | .//div[contains(@class, 'b-sidebar')] | " +
                                        ".//div[contains(@class, 'b-ad')] | .//div[contains(@class, 'ad-')] | " +
                                        ".//div[@class='similar-vacancies']");

                                    if (unwantedNodes != null)
                                    {
                                        foreach (var node in unwantedNodes)
                                        {
                                            node.Remove();
                                        }
                                    }

                                    var fullDescription = WebUtility.HtmlDecode(descriptionNode.InnerText?.Trim() ?? string.Empty);

                                    // Clean up multiple whitespaces and newlines
                                    fullDescription = System.Text.RegularExpressions.Regex.Replace(fullDescription, @"\s+", " ");
                                    fullDescription = fullDescription.Trim();

                                    _logger.LogInformation("📄 Extracted {NewLength} chars (original: {OldLength} chars) for {Title}",
                                        fullDescription.Length, vacancy.Description.Length, vacancy.Title);

                                    if (!string.IsNullOrEmpty(fullDescription) && fullDescription.Length > vacancy.Description.Length)
                                    {
                                        vacancy.Description = fullDescription;
                                        _logger.LogInformation("✅ Loaded {Length} characters of description for {Title}",
                                            fullDescription.Length, vacancy.Title);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("⚠️ Loaded description ({Length} chars) is not longer than original ({Original} chars)",
                                            fullDescription.Length, vacancy.Description.Length);
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("❌ Could not find description on page: {Url}", vacancy.Url);

                                    // Save HTML for debugging
                                    try
                                    {
                                        var debugPath = Path.Combine(Path.GetTempPath(), $"vacancy_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                                        File.WriteAllText(debugPath, pageHtml);
                                        _logger.LogWarning("Saved HTML to: {Path}", debugPath);
                                    }
                                    catch { }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to load full description from: {Url}", vacancy.Url);
                            }

                            await Task.Delay(_settings.DelayBetweenRequests, cancellationToken);
                        }

                        vacancies.Add(vacancy);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing vacancy element");
                }
            }
            
            _logger.LogInformation("Successfully scraped {Count} vacancies from DOU.ua using Selenium", vacancies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Selenium scraping");
            throw;
        }
        finally
        {
            try
            {
                driver?.Quit();
                _logger.LogInformation("Chrome driver closed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing Chrome driver");
            }
        }
        
        return vacancies;
    }

    private Vacancy? ParseVacancyElement(HtmlNode element)
    {
        var titleNode = element.SelectSingleNode(".//a[contains(@class, 'vt')]") ??
                       element.SelectSingleNode(".//h2//a");

        if (titleNode == null) return null;

        var vacancy = new Vacancy
        {
            Title = WebUtility.HtmlDecode(titleNode.InnerText?.Trim() ?? string.Empty),
            Url = GetAbsoluteUrl(titleNode.GetAttributeValue("href", string.Empty))
        };

        var companyNode = element.SelectSingleNode(".//a[contains(@class, 'company')]");
        if (companyNode != null)
        {
            vacancy.Company = WebUtility.HtmlDecode(companyNode.InnerText?.Trim() ?? string.Empty);
        }

        var descriptionNode = element.SelectSingleNode(".//div[contains(@class, 'sh-info')]") ??
                             element.SelectSingleNode(".//div[contains(@class, 'description')]");
        if (descriptionNode != null)
        {
            vacancy.Description = WebUtility.HtmlDecode(descriptionNode.InnerText?.Trim() ?? string.Empty);
        }

        var salaryNode = element.SelectSingleNode(".//span[contains(@class, 'salary')]");
        if (salaryNode != null)
        {
            vacancy.Salary = WebUtility.HtmlDecode(salaryNode.InnerText?.Trim() ?? string.Empty);
        }

        var locationNode = element.SelectSingleNode(".//span[contains(@class, 'cities')]");
        if (locationNode != null)
        {
            vacancy.Location = WebUtility.HtmlDecode(locationNode.InnerText?.Trim() ?? string.Empty);
        }

        var dateNode = element.SelectSingleNode(".//div[contains(@class, 'date')]");
        if (dateNode != null)
        {
            ParsePublishedDate(vacancy, dateNode.InnerText?.Trim() ?? string.Empty);
        }

        vacancy.IsRemote = vacancy.Location.ToLowerInvariant().Contains("remote") ||
                          vacancy.Location.ToLowerInvariant().Contains("віддалено") ||
                          vacancy.Description.ToLowerInvariant().Contains("remote");

        // Note: ParseTechnologies, ParseExperienceLevel, ParseEnglishLevel
        // should be called AFTER loading full description, not here

        return vacancy;
    }

    private void ParsePublishedDate(Vacancy vacancy, string dateText)
    {
        var currentYear = DateTime.Now.Year;
        
        var monthsUkr = new Dictionary<string, int>
        {
            {"січня", 1}, {"лютого", 2}, {"березня", 3}, {"квітня", 4},
            {"травня", 5}, {"червня", 6}, {"липня", 7}, {"серпня", 8},
            {"вересня", 9}, {"жовтня", 10}, {"листопада", 11}, {"грудня", 12}
        };

        try
        {
            if (dateText.Contains("сьогодні"))
            {
                vacancy.PublishedDate = DateTime.Today;
            }
            else if (dateText.Contains("вчора"))
            {
                vacancy.PublishedDate = DateTime.Today.AddDays(-1);
            }
            else
            {
                var parts = dateText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && int.TryParse(parts[0], out int day))
                {
                    var monthName = parts[1];
                    if (monthsUkr.TryGetValue(monthName, out int month))
                    {
                        vacancy.PublishedDate = new DateTime(currentYear, month, day);
                        
                        if (vacancy.PublishedDate > DateTime.Today)
                        {
                            vacancy.PublishedDate = vacancy.PublishedDate.AddYears(-1);
                        }
                    }
                }
            }
        }
        catch
        {
            vacancy.PublishedDate = DateTime.Today;
        }
    }

    private string GetAbsoluteUrl(string relativeUrl)
    {
        if (string.IsNullOrEmpty(relativeUrl)) return string.Empty;
        
        if (relativeUrl.StartsWith("http")) return relativeUrl;
        
        return $"https://jobs.dou.ua{relativeUrl}";
    }
}
