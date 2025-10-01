using DouVacancyAnalyzer.Presentation.Hubs;
using DouVacancyAnalyzer.Core.Application.DTOs;
using DouVacancyAnalyzer.Core.Application.Interfaces;
using DouVacancyAnalyzer.Core.Domain.Entities;
using DouVacancyAnalyzer.Core.Domain.Enums;
using DouVacancyAnalyzer.Core.Domain.Constants;
using DouVacancyAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DouVacancyAnalyzer.Presentation.Controllers;

public class HomeController : Controller
{
    private readonly IVacancyScrapingService _scrapingService;
    private readonly IVacancyAnalysisService _analysisService;
    private readonly IVacancyStorageService _storageService;
    private readonly IHubContext<AnalysisHub> _hubContext;
    private readonly ILogger<HomeController> _logger;
    private readonly ScrapingSettings _scrapingSettings;
    private readonly VacancyDbContext _dbContext;

    public HomeController(
        IVacancyScrapingService scrapingService,
        IVacancyAnalysisService analysisService,
        IVacancyStorageService storageService,
        IHubContext<AnalysisHub> hubContext,
        ILogger<HomeController> logger,
        IOptions<ScrapingSettings> scrapingSettings,
        VacancyDbContext dbContext)
    {
        _scrapingService = scrapingService;
        _analysisService = analysisService;
        _storageService = storageService;
        _hubContext = hubContext;
        _logger = logger;
        _scrapingSettings = scrapingSettings.Value;
        _dbContext = dbContext;
    }

    public IActionResult Index()
    {
        return View();
    }



    [HttpPost]
    public async Task<IActionResult> StartAnalysis()
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("AnalysisStarted");

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", "Collecting vacancies...", AnalysisConstants.ProgressScrapingStart);
            var (newVacancies, allVacancyUrls) = await _scrapingService.GetVacanciesAsync();

            // Update vacancy activity status - mark missing vacancies as inactive
            await _hubContext.Clients.All.SendAsync("ProgressUpdate", "Updating vacancy status...", AnalysisConstants.ProgressSavingToDatabase);
            var deactivatedCount = await _storageService.UpdateVacancyActivityStatusAsync(allVacancyUrls);

            // Save to database and detect new vacancies
            await _hubContext.Clients.All.SendAsync("ProgressUpdate", "Saving to database...", AnalysisConstants.ProgressSavingToDatabase);
            var savedVacancies = await _storageService.SaveVacanciesAsync(newVacancies);

            var totalVacanciesInDb = await _storageService.GetTotalVacancyCountAsync();
            var newVacancyCount = newVacancies.Count;

            _logger.LogInformation("📊 Scraping results: {NewCount} new vacancies, {DeactivatedCount} deactivated, {TotalInDb} total in DB",
                newVacancyCount, deactivatedCount, totalVacanciesInDb);

            await _hubContext.Clients.All.SendAsync("ProgressUpdate",
                string.Format("Found {0} total in DB, {1} new, {2} deactivated", totalVacanciesInDb, newVacancyCount, deactivatedCount), AnalysisConstants.ProgressAnalysisStart);

            // Get only unanalyzed vacancies for analysis
            var unanalyzedVacancies = await _storageService.GetUnanalyzedVacanciesAsync();
            var vacanciesForAnalysis = unanalyzedVacancies.Select(v => v.ToVacancy()).ToList();

            if (vacanciesForAnalysis.Count == 0)
            {
                _logger.LogInformation("No new vacancies to analyze, loading existing results");
                await _hubContext.Clients.All.SendAsync("ProgressUpdate", "No new vacancies to analyze, loading existing results...", AnalysisConstants.ProgressCalculatingStatistics);

                // Load existing analysis results
                var existingResults = await GetStoredAnalysisResults();
                await _hubContext.Clients.All.SendAsync("AnalysisCompleted", (object)existingResults);
                return Json(new { success = true });
            }

            // Use optimized parallel analysis for better performance
            var (report, allAnalyses, allMatches) = await _analysisService.AnalyzeVacanciesAsync(
                vacanciesForAnalysis,
                async (message, progress) => await _hubContext.Clients.All.SendAsync("ProgressUpdate", message, progress));

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", "Calculating statistics...", AnalysisConstants.ProgressCalculatingStatistics);

            // Create combined report with all analyzed vacancies
            var combinedReport = await GetStoredAnalysisResults();

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", "Completing analysis...", AnalysisConstants.ProgressCompleting);

            var result = new
            {
                Report = combinedReport.Report,
                TechStats = combinedReport.TechStats,
                AiStats = combinedReport.AiStats
            };

            // Create vacancy count history record
            var reportForHistory = (AnalysisReport)combinedReport.Report;
            await CreateVacancyCountHistoryRecord(reportForHistory);

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

    [HttpPost]
    public async Task<IActionResult> ReAnalyzeExisting()
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("AnalysisStarted");

            // Reset analysis data for all vacancies
            await _hubContext.Clients.All.SendAsync("ProgressUpdate", "Resetting analysis data...", AnalysisConstants.ProgressScrapingStart);
            await _storageService.ResetAnalysisDataAsync();

            // Get all active vacancies for re-analysis
            await _hubContext.Clients.All.SendAsync("ProgressUpdate", "Loading vacancies for re-analysis...", AnalysisConstants.ProgressSavingToDatabase);
            var allVacancies = await _storageService.GetActiveVacanciesAsync();
            var vacanciesForAnalysis = allVacancies.Select(v => v.ToVacancy()).ToList();

            if (vacanciesForAnalysis.Count == 0)
            {
                await _hubContext.Clients.All.SendAsync("AnalysisError", "No vacancies found for re-analysis");
                return Json(new { success = false, error = "No vacancies found for re-analysis" });
            }

            await _hubContext.Clients.All.SendAsync("ProgressUpdate",
                $"Starting re-analysis of {vacanciesForAnalysis.Count} vacancies...", AnalysisConstants.ProgressAnalysisStart);

            // Analyze all vacancies
            var (report, allAnalyses, allMatches) = await _analysisService.AnalyzeVacanciesAsync(
                vacanciesForAnalysis,
                async (message, progress) => await _hubContext.Clients.All.SendAsync("ProgressUpdate", message, progress));

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", "Calculating statistics...", AnalysisConstants.ProgressCalculatingStatistics);

            // Create combined report with all re-analyzed vacancies
            var combinedReport = await GetStoredAnalysisResults();

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", "Completing re-analysis...", AnalysisConstants.ProgressCompleting);

            var result = new
            {
                Report = combinedReport.Report,
                TechStats = combinedReport.TechStats,
                AiStats = combinedReport.AiStats
            };

            // Create vacancy count history record
            var reportForHistory = (AnalysisReport)combinedReport.Report;
            await CreateVacancyCountHistoryRecord(reportForHistory);

            await _hubContext.Clients.All.SendAsync("AnalysisCompleted", result);

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during re-analysis");
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
    public async Task<IActionResult> GetNewVacancies()
    {
        try
        {
            var newVacancies = await _storageService.GetNewVacanciesAsync();
            var newVacanciesData = newVacancies.Select(v => new
            {
                Id = v.Id,
                Title = v.Title,
                Company = v.Company,
                Location = v.Location,
                Experience = v.DetectedExperienceLevel?.ToString() ?? "Unspecified",
                EnglishLevel = v.DetectedEnglishLevel?.ToString() ?? "Unspecified",
                Url = v.Url,
                CreatedAt = v.CreatedAt,
                IsAnalyzed = v.MatchScore.HasValue
            }).ToList();

            return Json(new { success = true, data = newVacanciesData });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting new vacancies");
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
            var matches = vacanciesWithAnalysis.Where(v => v.MatchScore.HasValue).Select(v =>
            {
                _logger.LogInformation("Creating match for vacancy: {Title}, IsModernStack: {IsModernStack}, IsMiddleLevel: {IsMiddleLevel}, DetectedYears: {Years}",
                    v.Title, v.IsModernStack, v.IsMiddleLevel, v.DetectedYearsOfExperience);

                return new VacancyMatch
                {
                    Vacancy = v.ToVacancy(),
                    Analysis = new VacancyAnalysisResult
                    {
                        VacancyCategory = v.VacancyCategory ?? VacancyCategory.Other,
                        DetectedExperienceLevel = v.DetectedExperienceLevel ?? ExperienceLevel.Unspecified,
                        DetectedYearsOfExperience = v.DetectedYearsOfExperience,
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
                };
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

            // Create AI stats from stored analysis
            var analysisResults = matches.Select(m => m.Analysis).ToList();
            var aiStats = _analysisService.GetAiTechnologyStatisticsFromAnalysesWithVacancies(analysisResults, matches);

            // Create TechStats with real data from analysis results
            var techStats = new TechnologyStatistics
            {
                Total = vacanciesWithAnalysis.Count,
                WithModernTech = analysisResults.Count(a => a.DetectedTechnologies != null && a.DetectedTechnologies.Any()),
                WithOutdatedTech = 0, // This would need specific outdated tech detection logic
                WithDesktopApps = analysisResults.Count(a => a.VacancyCategory == VacancyCategory.Desktop),
                WithFrontend = analysisResults.Count(a => a.VacancyCategory == VacancyCategory.Frontend),
                WithTimeTracker = analysisResults.Count(a => a.HasNoTimeTracker == false),
                ModernTechCount = new Dictionary<string, int>(),
                OutdatedTechCount = new Dictionary<string, int>(),
                DesktopKeywordCount = new Dictionary<string, int>(),
                FrontendKeywordCount = new Dictionary<string, int>(),
                YearsRequirements = new Dictionary<string, int>(),
                VacancyCategories = new Dictionary<string, int>()
            };

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
        var matches = vacanciesWithAnalysis.Where(v => v.MatchScore.HasValue).Select(v =>
        {
            _logger.LogInformation("Creating match for vacancy: {Title}, IsModernStack: {IsModernStack}, IsMiddleLevel: {IsMiddleLevel}",
                v.Title, v.IsModernStack, v.IsMiddleLevel);

            return new VacancyMatch
            {
                Vacancy = v.ToVacancy(),
                Analysis = new VacancyAnalysisResult
                {
                    VacancyCategory = v.VacancyCategory ?? VacancyCategory.Other,
                    DetectedExperienceLevel = v.DetectedExperienceLevel ?? ExperienceLevel.Unspecified,
                    DetectedYearsOfExperience = v.DetectedYearsOfExperience,
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
            };
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

        // Create AI stats from stored analysis
        var analysisResults = matches.Select(m => m.Analysis).ToList();
        var aiStats = _analysisService.GetAiTechnologyStatisticsFromAnalysesWithVacancies(analysisResults, matches);

        // Create TechStats with real data from analysis results
        var techStats = new TechnologyStatistics
        {
            Total = vacanciesWithAnalysis.Count,
            WithModernTech = analysisResults.Count(a => a.DetectedTechnologies != null && a.DetectedTechnologies.Any()),
            WithOutdatedTech = 0, // This would need specific outdated tech detection logic
            WithDesktopApps = analysisResults.Count(a => a.VacancyCategory == VacancyCategory.Desktop),
            WithFrontend = analysisResults.Count(a => a.VacancyCategory == VacancyCategory.Frontend),
            WithTimeTracker = analysisResults.Count(a => a.HasNoTimeTracker == false),
            ModernTechCount = new Dictionary<string, int>(),
            OutdatedTechCount = new Dictionary<string, int>(),
            DesktopKeywordCount = new Dictionary<string, int>(),
            FrontendKeywordCount = new Dictionary<string, int>(),
            YearsRequirements = new Dictionary<string, int>(),
            VacancyCategories = new Dictionary<string, int>()
        };

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
            if (matchScore < AnalysisConstants.MinimumFullstackBackendScore)
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

    private async Task CreateVacancyCountHistoryRecord(AnalysisReport report)
    {
        try
        {
            var totalVacancies = await _storageService.GetTotalVacancyCountAsync();
            var activeVacancies = await _storageService.GetActiveVacancyCountAsync();
            var newVacancies = await _storageService.GetNewVacancyCountAsync();

            // Calculate deactivated vacancies (rough estimate)
            var deactivatedVacancies = totalVacancies - activeVacancies;

            await _storageService.CreateVacancyCountHistoryAsync(
                totalVacancies,
                activeVacancies,
                newVacancies,
                deactivatedVacancies,
                report.MatchingVacancies,
                (decimal)report.MatchPercentage
            );

            _logger.LogInformation("📊 Created vacancy count history record: Total={Total}, Active={Active}, New={New}, Matching={Matching}, Match%={MatchPercentage:F1}%",
                totalVacancies, activeVacancies, newVacancies, report.MatchingVacancies, report.MatchPercentage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to create vacancy count history record");
        }
    }

    // ===== VACANCY RESPONSE STATUS API =====

    [HttpPost]
    public async Task<IActionResult> ToggleVacancyResponse([FromBody] VacancyResponseRequest request)
    {
        try
        {
            var existingResponse = await _dbContext.VacancyResponses
                .FirstOrDefaultAsync(vr => vr.VacancyUrl == request.VacancyUrl);

            if (existingResponse != null)
            {
                // Toggle the response status
                existingResponse.HasResponded = !existingResponse.HasResponded;
                existingResponse.ResponseDate = existingResponse.HasResponded ? DateTime.UtcNow : null;
                existingResponse.UpdatedAt = DateTime.UtcNow;
                existingResponse.Notes = request.Notes;
            }
            else
            {
                // Create new response record
                var newResponse = new VacancyResponse
                {
                    VacancyUrl = request.VacancyUrl,
                    VacancyTitle = request.VacancyTitle,
                    CompanyName = request.CompanyName,
                    HasResponded = true,
                    ResponseDate = DateTime.UtcNow,
                    Notes = request.Notes
                };
                _dbContext.VacancyResponses.Add(newResponse);
            }

            await _dbContext.SaveChangesAsync();

            return Json(new {
                success = true,
                hasResponded = existingResponse?.HasResponded ?? true,
                responseDate = existingResponse?.ResponseDate ?? DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling vacancy response status for URL: {VacancyUrl}", request.VacancyUrl);
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetVacancyResponses()
    {
        try
        {
            var responses = await _dbContext.VacancyResponses
                .OrderByDescending(vr => vr.ResponseDate)
                .ToListAsync();

            _logger.LogInformation("Found {Count} vacancy responses in database", responses.Count);

            var responseData = responses.ToDictionary(
                vr => vr.VacancyUrl,
                vr => new
                {
                    vr.HasResponded,
                    vr.ResponseDate,
                    vr.Notes,
                    vr.VacancyTitle,
                    vr.CompanyName
                }
            );

            _logger.LogInformation("Returning response data with {Count} entries", responseData.Count);
            return Json(new { success = true, responses = responseData });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vacancy responses");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetVacancyCountHistory()
    {
        try
        {
            var history = await _storageService.GetVacancyCountHistoryAsync();

            return Json(new
            {
                success = true,
                history = history.Select(h => new
                {
                    h.CheckDate,
                    h.TotalVacancies,
                    h.ActiveVacancies,
                    h.NewVacancies,
                    h.DeactivatedVacancies,
                    h.MatchingVacancies,
                    h.MatchPercentage
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vacancy count history");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> DebugVacancyResponses()
    {
        try
        {
            var responses = await _dbContext.VacancyResponses.ToListAsync();
            var vacancies = await _dbContext.Vacancies.Select(v => new { v.Url, v.Title, v.Company }).Take(5).ToListAsync();

            return Json(new
            {
                success = true,
                responseCount = responses.Count,
                vacancyCount = vacancies.Count,
                responses = responses.Take(5).Select(r => new
                {
                    r.VacancyUrl,
                    r.VacancyTitle,
                    r.HasResponded,
                    r.ResponseDate
                }),
                vacancies = vacancies
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetVacancyResponseStatus(string vacancyUrl)
    {
        try
        {
            var response = await _dbContext.VacancyResponses
                .FirstOrDefaultAsync(vr => vr.VacancyUrl == vacancyUrl);

            return Json(new
            {
                success = true,
                hasResponded = response?.HasResponded ?? false,
                responseDate = response?.ResponseDate,
                notes = response?.Notes
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vacancy response status for URL: {VacancyUrl}", vacancyUrl);
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> DiagnoseDatabase()
    {
        try
        {
            var totalActive = await _dbContext.Vacancies.CountAsync(v => v.IsActive);
            var newCount = await _dbContext.Vacancies.CountAsync(v => v.IsNew && v.IsActive);
            var analyzedCount = await _dbContext.Vacancies.CountAsync(v => v.LastAnalyzedAt != null && v.IsActive);
            var withYears = await _dbContext.Vacancies.CountAsync(v => v.DetectedYearsOfExperience != null && v.IsActive);

            var sampleNew = await _dbContext.Vacancies
                .Where(v => v.IsNew && v.IsActive)
                .Select(v => new { v.Id, v.Title, v.IsNew, v.LastAnalyzedAt, v.DetectedYearsOfExperience, v.CreatedAt })
                .OrderByDescending(v => v.CreatedAt)
                .Take(10)
                .ToListAsync();

            var sampleAnalyzed = await _dbContext.Vacancies
                .Where(v => v.LastAnalyzedAt != null && v.IsActive)
                .Select(v => new { v.Id, v.Title, v.IsNew, v.DetectedYearsOfExperience, v.MatchScore })
                .OrderByDescending(v => v.MatchScore)
                .Take(5)
                .ToListAsync();

            return Json(new
            {
                success = true,
                stats = new
                {
                    totalActive,
                    newCount,
                    analyzedCount,
                    withYears
                },
                sampleNew,
                sampleAnalyzed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error diagnosing database");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> FixAnalyzedVacanciesStatus()
    {
        try
        {
            // Mark all analyzed vacancies as not new
            var updatedCount = await _dbContext.Vacancies
                .Where(v => v.IsNew && v.LastAnalyzedAt != null)
                .ExecuteUpdateAsync(v => v.SetProperty(x => x.IsNew, false));

            _logger.LogInformation("Fixed IsNew status for {Count} analyzed vacancies", updatedCount);

            return Json(new
            {
                success = true,
                message = $"Fixed {updatedCount} analyzed vacancies",
                updatedCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fixing analyzed vacancies status");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> TestSerialization()
    {
        var testVacancy = await _dbContext.Vacancies
            .Where(v => v.DetectedYearsOfExperience != null)
            .FirstOrDefaultAsync();

        if (testVacancy == null)
        {
            return Json(new { error = "No vacancy with years found" });
        }

        var match = new VacancyMatch
        {
            Vacancy = testVacancy.ToVacancy(),
            Analysis = new VacancyAnalysisResult
            {
                VacancyCategory = testVacancy.VacancyCategory ?? VacancyCategory.Other,
                DetectedExperienceLevel = testVacancy.DetectedExperienceLevel ?? ExperienceLevel.Unspecified,
                DetectedYearsOfExperience = testVacancy.DetectedYearsOfExperience,
                DetectedEnglishLevel = testVacancy.DetectedEnglishLevel ?? EnglishLevel.Unspecified,
                IsModernStack = testVacancy.IsModernStack,
                IsMiddleLevel = testVacancy.IsMiddleLevel,
                HasAcceptableEnglish = testVacancy.HasAcceptableEnglish,
                HasNoTimeTracker = testVacancy.HasNoTimeTracker,
                IsBackendSuitable = testVacancy.IsBackendSuitable,
                AnalysisReason = testVacancy.AnalysisReason ?? "",
                MatchScore = testVacancy.MatchScore ?? 0,
                DetectedTechnologies = new List<string> { "Test" }
            }
        };

        return Json(new
        {
            success = true,
            testVacancy = new
            {
                Title = testVacancy.Title,
                DetectedYearsOfExperience = testVacancy.DetectedYearsOfExperience
            },
            match
        });
    }
}

public class VacancyResponseRequest
{
    public string VacancyUrl { get; set; } = string.Empty;
    public string VacancyTitle { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Notes { get; set; }
}