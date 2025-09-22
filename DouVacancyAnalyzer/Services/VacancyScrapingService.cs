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
        _logger.LogInformation("Starting DOU.ua scraping with Selenium to get ALL vacancies");
        
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

    public async Task<List<Vacancy>> GetTestVacanciesAsync(int limit, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting DOU.ua test scraping for {Limit} vacancies using Selenium (test mode)", limit);

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

            _logger.LogInformation("Loading DOU.ua page with .NET category for test mode");
            driver.Navigate().GoToUrl(_settings.BaseUrl);

            wait.Until(d => d.FindElements(By.CssSelector("li.l-vacancy")).Count > 0);

            _logger.LogInformation("Getting initial vacancies for test mode (no 'Load More' clicks)");

            var html = driver.PageSource;
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var vacancyElements = doc.DocumentNode.SelectNodes("//li[contains(@class, 'l-vacancy')]") ??
                                 Enumerable.Empty<HtmlNode>();

            _logger.LogInformation("Found {Count} vacancy elements on first page", vacancyElements.Count());

            foreach (var element in vacancyElements.Take(limit))
            {
                try
                {
                    var vacancy = ParseVacancyElement(element);
                    if (vacancy != null)
                    {
                        vacancies.Add(vacancy);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing vacancy element in test mode");
                }
            }

            _logger.LogInformation("Test scraping completed. Returning {Count} vacancies", vacancies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during test scraping with Selenium");
            throw;
        }
        finally
        {
            try
            {
                driver?.Quit();
                _logger.LogInformation("Chrome driver closed (test mode)");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing Chrome driver in test mode");
            }
        }

        return vacancies;
    }

    private async Task<List<Vacancy>> GetVacanciesFromCategoryAsync(string category, CancellationToken cancellationToken)
    {
        var baseUrl = "https://jobs.dou.ua/vacancies/";
        var url = string.IsNullOrEmpty(category) ? baseUrl : $"{baseUrl}?category={category}";
        
        var response = await _httpClient.GetStringAsync(url, cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(response);

        var vacancyElements = doc.DocumentNode
            .SelectNodes("//li[contains(@class, 'l-vacancy')]") ?? 
            Enumerable.Empty<HtmlNode>();

        var vacancies = new List<Vacancy>();

        foreach (var element in vacancyElements)
        {
            try
            {
                var vacancy = ParseVacancyElement(element);
                if (vacancy != null)
                {
                    vacancies.Add(vacancy);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing vacancy element from category {Category}", category);
            }
        }

        return vacancies;
    }

    private async Task<List<Vacancy>> GetVacanciesFromPageAsync(int page, CancellationToken cancellationToken)
    {
        var url = page == 1 ? _settings.BaseUrl : $"{_settings.BaseUrl}&page={page}";
        
        var response = await _httpClient.GetStringAsync(url, cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(response);

        var vacancyElements = doc.DocumentNode
            .SelectNodes("//li[contains(@class, 'l-vacancy')]") ?? 
            Enumerable.Empty<HtmlNode>();

        var vacancies = new List<Vacancy>();

        foreach (var element in vacancyElements)
        {
            try
            {
                var vacancy = ParseVacancyElement(element);
                if (vacancy != null)
                {
                    vacancies.Add(vacancy);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing vacancy element");
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

        ParseTechnologies(vacancy);
        ParseExperienceLevel(vacancy);
        ParseEnglishLevel(vacancy);

        return vacancy;
    }

    private void ParseTechnologies(Vacancy vacancy)
    {
        var text = $"{vacancy.Title} {vacancy.Description}".ToLowerInvariant();
        var technologies = new List<string>();

        var techKeywords = new[]
        {
            ".net", "c#", "asp.net", "entity framework", "sql server", "postgresql", "mongodb",
            "redis", "docker", "kubernetes", "azure", "aws", "react", "angular", "vue.js",
            "blazor", "grpc", "graphql", "microservices", "rabbitmq", "kafka"
        };

        foreach (var tech in techKeywords)
        {
            if (text.Contains(tech))
            {
                technologies.Add(tech);
            }
        }

        vacancy.Technologies = technologies;
    }

    private void ParseExperienceLevel(Vacancy vacancy)
    {
        var text = $"{vacancy.Title} {vacancy.Description}".ToLowerInvariant();
        
        if (text.Contains("junior") || text.Contains("джуніор"))
        {
            vacancy.Experience = "Junior";
        }
        else if (text.Contains("middle") || text.Contains("мідл") || text.Contains("middle"))
        {
            vacancy.Experience = "Middle";
        }
        else if (text.Contains("senior") || text.Contains("сеньйор") || text.Contains("lead"))
        {
            vacancy.Experience = "Senior";
        }
        else
        {
            vacancy.Experience = "Unknown";
        }
    }

    private void ParseEnglishLevel(Vacancy vacancy)
    {
        var text = $"{vacancy.Title} {vacancy.Description}".ToLowerInvariant();
        
        var englishLevels = new[]
        {
            ("upper-intermediate", "Upper-Intermediate"),
            ("intermediate", "Intermediate"),
            ("pre-intermediate", "Pre-Intermediate"),
            ("advanced", "Advanced"),
            ("upper intermediate", "Upper-Intermediate"),
            ("pre intermediate", "Pre-Intermediate"),
            ("b2", "B2"),
            ("b1", "B1"),
            ("c1", "C1")
        };

        foreach (var (keyword, level) in englishLevels)
        {
            if (text.Contains(keyword))
            {
                vacancy.EnglishLevel = level;
                return;
            }
        }

        vacancy.EnglishLevel = "Not specified";
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
