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
            await _hubContext.Clients.All.SendAsync("AnalysisStarted");

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["CollectingVacancies"].Value, 10);
            var vacancies = await _scrapingService.GetTestVacanciesAsync(_scrapingSettings.TestModeLimit);

            await _hubContext.Clients.All.SendAsync("ProgressUpdate",
                string.Format(_progressLocalizer["FoundVacancies"].Value, vacancies.Count), 30);

            var (report, allAnalyses) = await AnalyzeVacanciesWithProgress(vacancies);

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["CalculatingStatistics"].Value, 60);
            var techStats = _analysisService.GetTechnologyStatistics(vacancies);

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["GettingAiAnalysis"].Value, 80);
            var aiStats = _analysisService.GetAiTechnologyStatisticsFromAnalyses(allAnalyses);

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
            _logger.LogError(ex, "Error during test analysis");
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

            var (report, allAnalyses) = await AnalyzeVacanciesWithProgress(vacancies);

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["CalculatingStatistics"].Value, 60);
            var techStats = _analysisService.GetTechnologyStatistics(vacancies);

            await _hubContext.Clients.All.SendAsync("ProgressUpdate", _progressLocalizer["GettingAiAnalysis"].Value, 80);
            var aiStats = _analysisService.GetAiTechnologyStatisticsFromAnalyses(allAnalyses);

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

    private async Task<(AnalysisReport report, List<VacancyAnalysisResult> allAnalyses)> AnalyzeVacanciesWithProgress(List<Vacancy> vacancies)
    {
        var matches = new List<VacancyMatch>();
        var allAnalyses = new List<VacancyAnalysisResult>();
        var totalVacancies = vacancies.Count;

        for (int i = 0; i < vacancies.Count; i++)
        {
            var vacancy = vacancies[i];
            var progressPercent = 30 + (int)((i / (double)totalVacancies) * 30); // 30-60%

            var truncatedTitle = vacancy.Title.Length > 50
                ? vacancy.Title.Substring(0, 47) + "..."
                : vacancy.Title;

            await _hubContext.Clients.All.SendAsync("ProgressUpdate",
                string.Format(_progressLocalizer["AnalyzingVacancy"].Value, i + 1, totalVacancies, truncatedTitle),
                progressPercent);

            _logger.LogInformation("Analyzing vacancy {Index}/{Total}: {Title} at {Company}",
                i + 1, totalVacancies, vacancy.Title, vacancy.Company);

            try
            {
                var analysis = await _analysisService.AnalyzeVacancyAsync(vacancy);
                allAnalyses.Add(analysis);

                _logger.LogInformation("Vacancy analysis completed: {Title} - Category: {Category}, Score: {Score}, Match: {IsMatch}",
                    vacancy.Title, analysis.VacancyCategory, analysis.MatchScore,
                    (analysis.IsBackendSuitable ?? false) && (analysis.IsModernStack ?? false));

                if (IsVacancyMatch(analysis))
                {
                    matches.Add(new VacancyMatch
                    {
                        Vacancy = vacancy,
                        Analysis = analysis
                    });

                    _logger.LogInformation("✅ Vacancy {Title} matched criteria!", vacancy.Title);
                }
                else
                {
                    _logger.LogDebug("❌ Vacancy {Title} did not match criteria: {Reason}",
                        vacancy.Title, analysis.AnalysisReason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze vacancy {Title}", vacancy.Title);
            }
        }

        var report = new AnalysisReport
        {
            TotalVacancies = totalVacancies,
            MatchingVacancies = matches.Count,
            Matches = matches.OrderByDescending(m => m.Analysis.MatchScore).ToList()
        };

        return (report, allAnalyses);
    }

    private bool IsVacancyMatch(VacancyAnalysisResult analysis)
    {
        return (analysis.IsBackendSuitable ?? false) &&
               (analysis.IsModernStack ?? false) &&
               (analysis.IsMiddleLevel ?? false) &&
               (analysis.HasAcceptableEnglish ?? false) &&
               (analysis.HasNoTimeTracker ?? true);
    }
}