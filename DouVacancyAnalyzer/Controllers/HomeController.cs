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
    private readonly IHubContext<AnalysisHub> _hubContext;
    private readonly ILogger<HomeController> _logger;
    private readonly ScrapingSettings _scrapingSettings;
    private readonly IStringLocalizer<ProgressMessages> _progressLocalizer;

    public HomeController(
        IVacancyScrapingService scrapingService,
        IVacancyAnalysisService analysisService,
        IHubContext<AnalysisHub> hubContext,
        ILogger<HomeController> logger,
        IOptions<ScrapingSettings> scrapingSettings,
        IStringLocalizer<ProgressMessages> progressLocalizer)
    {
        _scrapingService = scrapingService;
        _analysisService = analysisService;
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
            var vacancies = await _scrapingService.GetTestVacanciesAsync(_scrapingSettings.TestModeLimit);

            _logger.LogInformation("🧪 Test mode collected {Count} vacancies", vacancies.Count);
            await _hubContext.Clients.All.SendAsync("ProgressUpdate",
                string.Format(_progressLocalizer["FoundVacancies"].Value, vacancies.Count), 30);

            var (report, allAnalyses, allMatches) = await _analysisService.AnalyzeVacanciesWithProgressAsync(
                vacancies,
                async (message, progress) => await _hubContext.Clients.All.SendAsync("ProgressUpdate", message, progress));

            _logger.LogInformation("🧪 Test analysis completed: {Total} total, {Matching} matching ({Percentage:F1}%)",
                report.TotalVacancies, report.MatchingVacancies, report.MatchPercentage);

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["CalculatingStatistics"].Value, 60);
            var techStats = _analysisService.GetTechnologyStatistics(vacancies);

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["GettingAiAnalysis"].Value, 80);
            var aiStats = _analysisService.GetAiTechnologyStatisticsFromAnalysesWithVacancies(allAnalyses, allMatches);

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["Completing"].Value, 100);

            var result = new
            {
                Report = report,
                TechStats = techStats,
                AiStats = aiStats
            };

            _logger.LogInformation("🧪 Test mode preparing to send results:");
            _logger.LogInformation("  Report: TotalVacancies={Total}, MatchingVacancies={Matching}, MatchPercentage={Percentage:F1}%",
                report.TotalVacancies, report.MatchingVacancies, report.MatchPercentage);
            _logger.LogInformation("  TechStats: Total={Total}, WithModernTech={Modern}",
                techStats.Total, techStats.WithModernTech);
            _logger.LogInformation("  AiStats: VacancyCategories count={Count}",
                aiStats.VacancyCategories?.Count ?? 0);

            await _hubContext.Clients.All.SendAsync("AnalysisCompleted", result);
            _logger.LogInformation("🧪 Test mode results sent to frontend");

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during test analysis");
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
            var vacancies = await _scrapingService.GetVacanciesAsync();

            await _hubContext.Clients.All.SendAsync("ProgressUpdate",
                string.Format(_progressLocalizer["FoundVacancies"].Value, vacancies.Count), 30);

            var (report, allAnalyses, allMatches) = await _analysisService.AnalyzeVacanciesWithProgressAsync(
                vacancies,
                async (message, progress) => await _hubContext.Clients.All.SendAsync("ProgressUpdate", message, progress));

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["CalculatingStatistics"].Value, 60);
            var techStats = _analysisService.GetTechnologyStatistics(vacancies);

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["GettingAiAnalysis"].Value, 80);
            var aiStats = _analysisService.GetAiTechnologyStatisticsFromAnalysesWithVacancies(allAnalyses, allMatches);

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["Completing"].Value, 100);

            var result = new
            {
                Report = report,
                TechStats = techStats,
                AiStats = aiStats
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

}