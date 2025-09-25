using DouVacancyAnalyzer.Hubs;
using DouVacancyAnalyzer.Models;
using DouVacancyAnalyzer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Localization;
using System.Text.Json;

namespace DouVacancyAnalyzer.Controllers;

public class HomeController : Controller
{
    private readonly IVacancyScrapingService _scrapingService;
    private readonly IVacancyAnalysisService _analysisService;
    private readonly IVacancyStorageService _storageService;
    private readonly IHubContext<AnalysisHub> _hubContext;
    private readonly ILogger<HomeController> _logger;
    private readonly ScrapingSettings _scrapingSettings;
    private readonly IStringLocalizer<ProgressMessages> _progressLocalizer;

    public HomeController(
        IVacancyScrapingService scrapingService,
        IVacancyAnalysisService analysisService,
        IVacancyStorageService storageService,
        IHubContext<AnalysisHub> hubContext,
        ILogger<HomeController> logger,
        IOptions<ScrapingSettings> scrapingSettings,
        IStringLocalizer<ProgressMessages> progressLocalizer)
    {
        _scrapingService = scrapingService;
        _analysisService = analysisService;
        _storageService = storageService;
        _hubContext = hubContext;
        _logger = logger;
        _scrapingSettings = scrapingSettings.Value;
        _progressLocalizer = progressLocalizer;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> StartTestAnalysis()
    {
        try
        {
            _logger.LogInformation("🧪 Starting TEST ANALYSIS with limit: {Limit}", _scrapingSettings.TestModeLimit);
            await _hubContext.Clients.All.SendAsync("AnalysisStarted");

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["CollectingVacancies"].Value, 10);
            var scrapedVacancies = await _scrapingService.GetTestVacanciesAsync(_scrapingSettings.TestModeLimit);

            _logger.LogInformation("🧪 Test mode collected {Count} vacancies", scrapedVacancies.Count);

            // Save to database and detect new vacancies
            await _hubContext.Clients.All.SendAsync("ProgressUpdate", "Saving to database...", 20);
            var savedVacancies = await _storageService.SaveVacanciesAsync(scrapedVacancies);
            var newVacancyCount = await _storageService.GetNewVacancyCountAsync();

            await _hubContext.Clients.All.SendAsync("ProgressUpdate",
                string.Format("Found {0} vacancies ({1} new)", scrapedVacancies.Count, newVacancyCount), 30);

            // Get only unanalyzed vacancies for analysis
            var unanalyzedVacancies = await _storageService.GetUnanalyzedVacanciesAsync();
            var vacanciesForAnalysis = unanalyzedVacancies.Select(v => v.ToVacancy()).ToList();

            if (vacanciesForAnalysis.Count == 0)
            {
                _logger.LogInformation("🧪 No new vacancies to analyze, loading existing results");
                await _hubContext.Clients.All.SendAsync("ProgressUpdate", "No new vacancies to analyze, loading existing results...", 90);

                // Load existing analysis results
                var existingResults = await GetStoredAnalysisResults();
                await _hubContext.Clients.All.SendAsync("AnalysisCompleted", (object)existingResults);
                return Json(new { success = true });
            }

            var (report, allAnalyses, allMatches) = await _analysisService.AnalyzeVacanciesWithProgressAsync(
                vacanciesForAnalysis,
                async (message, progress) => await _hubContext.Clients.All.SendAsync("ProgressUpdate", message, progress));

            _logger.LogInformation("🧪 Test analysis completed: {Total} total, {Matching} matching ({Percentage:F1}%)",
                report.TotalVacancies, report.MatchingVacancies, report.MatchPercentage);

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["CalculatingStatistics"].Value, 60);

            // Get all vacancies for statistics (existing + newly analyzed)
            var allVacanciesForStats = await _storageService.GetVacanciesWithAnalysisAsync();
            var allVacanciesData = allVacanciesForStats.Select(v => v.ToVacancy()).ToList();
            var techStats = _analysisService.GetTechnologyStatistics(allVacanciesData);

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["GettingAiAnalysis"].Value, 80);

            // Create combined report with all analyzed vacancies
            var combinedReport = await GetStoredAnalysisResults();

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["Completing"].Value, 100);

            var result = new
            {
                Report = combinedReport.Report,
                TechStats = combinedReport.TechStats,
                AiStats = combinedReport.AiStats
            };

            _logger.LogInformation("🧪 Test mode preparing to send results:");
            var reportForLogging = (AnalysisReport)combinedReport.Report;
            var techStatsForLogging = (TechnologyStatistics)combinedReport.TechStats;
            var aiStatsForLogging = (TechnologyStatistics)combinedReport.AiStats;
            _logger.LogInformation("  Report: TotalVacancies={Total}, MatchingVacancies={Matching}, MatchPercentage={Percentage:F1}%",
                reportForLogging.TotalVacancies, reportForLogging.MatchingVacancies, reportForLogging.MatchPercentage);
            _logger.LogInformation("  TechStats: Total={Total}",
                techStatsForLogging.Total);
            _logger.LogInformation("  AiStats: VacancyCategories count={Count}",
                aiStatsForLogging.VacancyCategories?.Count ?? 0);

            await _hubContext.Clients.All.SendAsync("AnalysisCompleted", result);
            _logger.LogInformation("🧪 Test mode results sent to frontend");

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during test analysis");
            _logger.LogError("Exception Type: {ExceptionType}", ex.GetType().Name);
            _logger.LogError("Exception Message: {Message}", ex.Message);
            if (ex.InnerException != null)
            {
                _logger.LogError("Inner Exception Type: {InnerExceptionType}", ex.InnerException.GetType().Name);
                _logger.LogError("Inner Exception Message: {InnerMessage}", ex.InnerException.Message);
                _logger.LogError("Inner Exception Stack: {InnerStack}", ex.InnerException.StackTrace);
            }
            _logger.LogError("Full Stack Trace: {StackTrace}", ex.StackTrace);

            await _hubContext.Clients.All.SendAsync("AnalysisError", ex.Message);
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> StartAnalysis()
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("AnalysisStarted");

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["CollectingVacancies"].Value, 10);
            var scrapedVacancies = await _scrapingService.GetVacanciesAsync();

            // Save to database and detect new vacancies
            await _hubContext.Clients.All.SendAsync("ProgressUpdate", "Saving to database...", 20);
            var savedVacancies = await _storageService.SaveVacanciesAsync(scrapedVacancies);
            var newVacancyCount = await _storageService.GetNewVacancyCountAsync();

            await _hubContext.Clients.All.SendAsync("ProgressUpdate",
                string.Format("Found {0} vacancies ({1} new)", scrapedVacancies.Count, newVacancyCount), 30);

            // Get only unanalyzed vacancies for analysis
            var unanalyzedVacancies = await _storageService.GetUnanalyzedVacanciesAsync();
            var vacanciesForAnalysis = unanalyzedVacancies.Select(v => v.ToVacancy()).ToList();

            if (vacanciesForAnalysis.Count == 0)
            {
                _logger.LogInformation("No new vacancies to analyze, loading existing results");
                await _hubContext.Clients.All.SendAsync("ProgressUpdate", "No new vacancies to analyze, loading existing results...", 90);

                // Load existing analysis results
                var existingResults = await GetStoredAnalysisResults();
                await _hubContext.Clients.All.SendAsync("AnalysisCompleted", (object)existingResults);
                return Json(new { success = true });
            }

            var (report, allAnalyses, allMatches) = await _analysisService.AnalyzeVacanciesWithProgressAsync(
                vacanciesForAnalysis,
                async (message, progress) => await _hubContext.Clients.All.SendAsync("ProgressUpdate", message, progress));

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["CalculatingStatistics"].Value, 60);

            // Create combined report with all analyzed vacancies
            var combinedReport = await GetStoredAnalysisResults();

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["Completing"].Value, 100);

            var result = new
            {
                Report = combinedReport.Report,
                TechStats = combinedReport.TechStats,
                AiStats = combinedReport.AiStats
            };

            await _hubContext.Clients.All.SendAsync("AnalysisCompleted", result);

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during analysis");
            await _hubContext.Clients.All.SendAsync("AnalysisError", ex.Message);
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetDatabaseStats()
    {
        try
        {
            var totalCount = await _storageService.GetTotalVacancyCountAsync();
            var newCount = await _storageService.GetNewVacancyCountAsync();

            return Json(new
            {
                success = true,
                totalVacancies = totalCount,
                newVacancies = newCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database stats");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ClearDatabase()
    {
        try
        {
            await _storageService.ClearDatabaseAsync();
            return Json(new { success = true, message = "Database cleared successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing database");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> RecalculateHashes()
    {
        try
        {
            await _storageService.RecalculateContentHashesAsync();
            return Json(new { success = true, message = "Content hashes recalculated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating hashes");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> MarkVacanciesAsViewed()
    {
        try
        {
            await _storageService.MarkVacanciesAsViewedAsync();
            return Json(new { success = true, message = "All vacancies marked as viewed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking vacancies as viewed");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetStoredAnalysis()
    {
        try
        {
            var vacanciesWithAnalysis = await _storageService.GetVacanciesWithAnalysisAsync();
            var totalCount = await _storageService.GetTotalVacancyCountAsync();
            var newCount = await _storageService.GetNewVacancyCountAsync();

            // Convert to VacancyMatch objects for compatibility with existing frontend
            var matches = vacanciesWithAnalysis.Where(v => v.MatchScore.HasValue).Select(v => new VacancyMatch
            {
                Vacancy = v.ToVacancy(),
                Analysis = new VacancyAnalysisResult
                {
                    VacancyCategory = v.VacancyCategory ?? VacancyCategory.Other,
                    DetectedExperienceLevel = v.DetectedExperienceLevel ?? ExperienceLevel.Unspecified,
                    DetectedEnglishLevel = v.DetectedEnglishLevel ?? EnglishLevel.Unspecified,
                    IsModernStack = v.IsModernStack,
                    IsMiddleLevel = v.IsMiddleLevel,
                    HasAcceptableEnglish = v.HasAcceptableEnglish,
                    HasNoTimeTracker = v.HasNoTimeTracker,
                    IsBackendSuitable = v.IsBackendSuitable,
                    AnalysisReason = v.AnalysisReason ?? "",
                    MatchScore = v.MatchScore ?? 0,
                    DetectedTechnologies = string.IsNullOrEmpty(v.DetectedTechnologies)
                        ? new List<string>()
                        : System.Text.Json.JsonSerializer.Deserialize<List<string>>(v.DetectedTechnologies) ?? new List<string>()
                }
            }).ToList();

            // Use the same matching logic as the analysis service
            var matchingVacancies = matches.Where(m => IsVacancyMatchForDisplay(m.Analysis)).ToList();

            var report = new AnalysisReport
            {
                TotalVacancies = vacanciesWithAnalysis.Count,
                MatchingVacancies = matchingVacancies.Count,
                MatchPercentage = vacanciesWithAnalysis.Count > 0 ? (matchingVacancies.Count * 100.0) / vacanciesWithAnalysis.Count : 0,
                Matches = matchingVacancies.OrderByDescending(m => m.Analysis.MatchScore).ToList()
            };

            // Create dummy TechStats for compatibility with existing UI
            var techStats = new TechnologyStatistics
            {
                Total = vacanciesWithAnalysis.Count,
                ModernTechCount = new Dictionary<string, int>(),
                OutdatedTechCount = new Dictionary<string, int>(),
                DesktopKeywordCount = new Dictionary<string, int>(),
                FrontendKeywordCount = new Dictionary<string, int>(),
                YearsRequirements = new Dictionary<string, int>(),
                VacancyCategories = new Dictionary<string, int>()
            };

            // Create AI stats from stored analysis
            var analysisResults = matches.Select(m => m.Analysis).ToList();
            var aiStats = _analysisService.GetAiTechnologyStatisticsFromAnalysesWithVacancies(analysisResults, matches);

            var result = new
            {
                Report = report,
                TechStats = techStats,
                AiStats = aiStats,
                DatabaseStats = new { totalCount, newCount },
                HasNewVacancies = newCount > 0
            };

            return Json(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stored analysis");
            return Json(new { success = false, error = ex.Message });
        }
    }

    private async Task<dynamic> GetStoredAnalysisResults()
    {
        var vacanciesWithAnalysis = await _storageService.GetVacanciesWithAnalysisAsync();
        var totalCount = await _storageService.GetTotalVacancyCountAsync();
        var newCount = await _storageService.GetNewVacancyCountAsync();

        // Convert to VacancyMatch objects for compatibility with existing frontend
        var matches = vacanciesWithAnalysis.Where(v => v.MatchScore.HasValue).Select(v => new VacancyMatch
        {
            Vacancy = v.ToVacancy(),
            Analysis = new VacancyAnalysisResult
            {
                VacancyCategory = v.VacancyCategory ?? VacancyCategory.Other,
                DetectedExperienceLevel = v.DetectedExperienceLevel ?? ExperienceLevel.Unspecified,
                DetectedEnglishLevel = v.DetectedEnglishLevel ?? EnglishLevel.Unspecified,
                IsModernStack = v.IsModernStack,
                IsMiddleLevel = v.IsMiddleLevel,
                HasAcceptableEnglish = v.HasAcceptableEnglish,
                HasNoTimeTracker = v.HasNoTimeTracker,
                IsBackendSuitable = v.IsBackendSuitable,
                AnalysisReason = v.AnalysisReason ?? "",
                MatchScore = v.MatchScore ?? 0,
                DetectedTechnologies = string.IsNullOrEmpty(v.DetectedTechnologies)
                    ? new List<string>()
                    : System.Text.Json.JsonSerializer.Deserialize<List<string>>(v.DetectedTechnologies) ?? new List<string>()
            }
        }).ToList();

        // Use the same matching logic as the analysis service
        var matchingVacancies = matches.Where(m => IsVacancyMatchForDisplay(m.Analysis)).ToList();

        var report = new AnalysisReport
        {
            TotalVacancies = vacanciesWithAnalysis.Count,
            MatchingVacancies = matchingVacancies.Count,
            MatchPercentage = vacanciesWithAnalysis.Count > 0 ? (matchingVacancies.Count * 100.0) / vacanciesWithAnalysis.Count : 0,
            Matches = matchingVacancies.OrderByDescending(m => m.Analysis.MatchScore).ToList()
        };

        // Create dummy TechStats for compatibility with existing UI
        var techStats = new TechnologyStatistics
        {
            Total = vacanciesWithAnalysis.Count,
            ModernTechCount = new Dictionary<string, int>(),
            OutdatedTechCount = new Dictionary<string, int>(),
            DesktopKeywordCount = new Dictionary<string, int>(),
            FrontendKeywordCount = new Dictionary<string, int>(),
            YearsRequirements = new Dictionary<string, int>(),
            VacancyCategories = new Dictionary<string, int>()
        };

        // Create AI stats from stored analysis
        var analysisResults = matches.Select(m => m.Analysis).ToList();
        var aiStats = _analysisService.GetAiTechnologyStatisticsFromAnalysesWithVacancies(analysisResults, matches);

        return new
        {
            Report = report,
            TechStats = techStats,
            AiStats = aiStats,
            DatabaseStats = new { totalCount, newCount },
            HasNewVacancies = newCount > 0
        };
    }

    private bool IsVacancyMatchForDisplay(VacancyAnalysisResult analysis)
    {
        // Same logic as VacancyAnalysisService.IsVacancyMatch but simplified

        // 1. Must be Backend suitable
        var isBackendSuitable = analysis.IsBackendSuitable ?? false;
        if (!isBackendSuitable)
        {
            return false;
        }

        // 2. Check if it's Fullstack but needs strong Backend focus
        if (analysis.VacancyCategory == VacancyCategory.Fullstack)
        {
            var matchScore = analysis.MatchScore;
            if (matchScore < 70)
            {
                return false;
            }
        }

        // 3. Experience level check - NO Senior/Lead positions
        var experienceLevel = analysis.DetectedExperienceLevel;
        if (experienceLevel == ExperienceLevel.Senior || experienceLevel == ExperienceLevel.Lead)
        {
            return false;
        }

        // 4. Must be suitable for Middle level (3+ years experience)
        var isMiddleLevel = analysis.IsMiddleLevel ?? false;
        if (!isMiddleLevel && experienceLevel != ExperienceLevel.Middle && experienceLevel != ExperienceLevel.Unspecified)
        {
            return false;
        }

        return true;
    }

}