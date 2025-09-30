namespace DouVacancyAnalyzer.Core.Domain.Constants;

public static class AnalysisConstants
{
    // Match scoring thresholds
    public const int MinimumFullstackBackendScore = 70;
    public const int ModernStackBonusScore = 2;
    public const int AcceptableEnglishBonusScore = 1;
    public const int NoTimeTrackerBonusScore = 1;
    public const int MaxBonusScore = 4;

    // Progress percentages
    public const int ProgressScrapingStart = 10;
    public const int ProgressSavingToDatabase = 20;
    public const int ProgressAnalysisStart = 30;
    public const int ProgressCalculatingStatistics = 60;
    public const int ProgressCompleting = 100;
    public const int AnalysisProgressBasePercent = 30;
    public const int AnalysisProgressRange = 60;

    // Retry settings
    public const int AnalysisRetryDelayMs = 1000;
    public const int MaxAnalysisRetries = 1;

    // JSON parsing
    public const char JsonStartChar = '{';
    public const char JsonEndChar = '}';

    // URL constants
    public const string DouBaseUrl = "https://jobs.dou.ua";
    public const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";

    // Database constants
    public const string DatabaseConnectionString = "Data Source=vacancies.db";

    // Vacancy history limit
    public const int DefaultVacancyHistoryLimit = 30;

    // Selenium settings
    public const int SeleniumWaitTimeoutSeconds = 15;
    public const int SeleniumPageLoadDelayMs = 2000;
    public const int SeleniumLoadMoreDelayMs = 1000;
    public const int SeleniumLoadMoreWaitDelayMs = 3000;
    public const int MaxLoadMoreAttempts = 20;
    public const string SeleniumWindowSize = "1920,1080";

    // CSS Selectors
    public const string VacancyListItemSelector = "li.l-vacancy";
    public const string LoadMoreButtonXPath = "//a[contains(text(), 'Більше вакансій')]";
    public const string VacancyTitleSelector = ".//a[contains(@class, 'vt')]";
    public const string CompanySelector = ".//a[contains(@class, 'company')]";
    public const string DescriptionSelector = ".//div[contains(@class, 'sh-info')]";
    public const string SalarySelector = ".//span[contains(@class, 'salary')]";
    public const string LocationSelector = ".//span[contains(@class, 'cities')]";
    public const string DateSelector = ".//div[contains(@class, 'date')]";

    // Location keywords
    public static readonly string[] RemoteKeywords = { "remote", "віддалено" };

    // Date parsing
    public static readonly Dictionary<string, int> UkrainianMonths = new()
    {
        { "січня", 1 }, { "лютого", 2 }, { "березня", 3 }, { "квітня", 4 },
        { "травня", 5 }, { "червня", 6 }, { "липня", 7 }, { "серпня", 8 },
        { "вересня", 9 }, { "жовтня", 10 }, { "листопада", 11 }, { "грудня", 12 }
    };

    public const string TodayKeyword = "сьогодні";
    public const string YesterdayKeyword = "вчора";

    // Truncation limits
    public const int VacancyTitleTruncateLength = 50;
    public const int VacancyTitleTruncateShortLength = 47;
    public const string TruncateSuffix = "...";

    // Chrome driver arguments
    public static readonly string[] ChromeHeadlessArguments =
    {
        "--headless",
        "--no-sandbox",
        "--disable-dev-shm-usage",
        "--disable-gpu"
    };
}
