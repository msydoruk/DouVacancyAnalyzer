using DouVacancyAnalyzer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DouVacancyAnalyzer.Services;

public class VacancyAnalysisService : IVacancyAnalysisService
{
    private readonly OpenAIClient _openAiClient;
    private readonly ILogger<VacancyAnalysisService> _logger;
    private readonly OpenAiSettings _openAiSettings;

    public VacancyAnalysisService(
        OpenAIClient openAiClient,
        ILogger<VacancyAnalysisService> logger,
        IOptions<OpenAiSettings> openAiSettings)
    {
        _openAiClient = openAiClient;
        _logger = logger;
        _openAiSettings = openAiSettings.Value;
    }

    public async Task<VacancyAnalysisResult> AnalyzeVacancyAsync(Vacancy vacancy, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_openAiClient == null)
            {
                _logger.LogError("OpenAI client not available - AI analysis is required");
                throw new InvalidOperationException("AI analysis is required but OpenAI client is not configured");
            }

            var aiAnalysis = await PerformAiAnalysisAsync(vacancy, cancellationToken);

            if (aiAnalysis != null)
            {
                PopulateCriteriaMatch(aiAnalysis);
                return aiAnalysis;
            }

            _logger.LogError("AI analysis failed for vacancy {Title} - retrying", vacancy.Title);

            await Task.Delay(1000, cancellationToken);
            aiAnalysis = await PerformAiAnalysisAsync(vacancy, cancellationToken);

            if (aiAnalysis != null)
            {
                PopulateCriteriaMatch(aiAnalysis);
                return aiAnalysis;
            }

            throw new InvalidOperationException($"AI analysis failed for vacancy: {vacancy.Title}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error analyzing vacancy {Title}", vacancy.Title);
            throw;
        }
    }


    public TechnologyStatistics GetTechnologyStatistics(List<Vacancy> vacancies)
    {
        return new TechnologyStatistics
        {
            Total = vacancies.Count,
            ModernTechCount = new Dictionary<string, int>(),
            OutdatedTechCount = new Dictionary<string, int>(),
            DesktopKeywordCount = new Dictionary<string, int>(),
            FrontendKeywordCount = new Dictionary<string, int>(),
            YearsRequirements = new Dictionary<string, int>(),
            VacancyCategories = new Dictionary<string, int>()
        };
    }


    private async Task<VacancyAnalysisResult?> PerformAiAnalysisAsync(Vacancy vacancy, CancellationToken cancellationToken)
    {
        try
        {
            var userPrompt = _openAiSettings.Prompts.UserPromptTemplate
                .Replace("{title}", vacancy.Title)
                .Replace("{company}", vacancy.Company)
                .Replace("{description}", vacancy.Description)
                .Replace("{experience}", vacancy.Experience)
                .Replace("{englishLevel}", vacancy.EnglishLevel)
                .Replace("{location}", vacancy.Location);

            var chatClient = _openAiClient.GetChatClient(_openAiSettings.Model);

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(_openAiSettings.Prompts.SystemPrompt),
                ChatMessage.CreateUserMessage(userPrompt)
            };

            var response = await chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
            return ParseAiResponse(response.Value.Content[0].Text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI analysis failed for vacancy {Title}", vacancy.Title);
            return null;
        }
    }

    private VacancyAnalysisResult? ParseAiResponse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);


                return JsonSerializer.Deserialize<VacancyAnalysisResult>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response: {Response}", response);
        }

        return null;
    }


    private void PopulateCriteriaMatch(VacancyAnalysisResult analysis)
    {
        analysis.CriteriaMatch = new Dictionary<MatchCriteria, bool>
        {
            [MatchCriteria.ModernStack] = analysis.IsModernStack ?? false,
            [MatchCriteria.MiddleLevel] = analysis.IsMiddleLevel ?? false,
            [MatchCriteria.AcceptableEnglish] = analysis.HasAcceptableEnglish ?? false,
            [MatchCriteria.NoTimeTracker] = analysis.HasNoTimeTracker ?? true,
            [MatchCriteria.BackendSuitable] = analysis.IsBackendSuitable ?? false
        };
    }


    private bool IsMatch(VacancyAnalysisResult analysis)
    {
        return (analysis.IsBackendSuitable ?? false) &&
               (analysis.IsModernStack ?? false) &&
               (analysis.IsMiddleLevel ?? false) &&
               (analysis.HasAcceptableEnglish ?? false) &&
               (analysis.HasNoTimeTracker ?? true);
    }




    private void UpdateStatisticsFromAiAnalysis(TechnologyStatistics stats, VacancyAnalysisResult analysis)
    {
        var categoryName = analysis.VacancyCategory.ToString();
        stats.VacancyCategories[categoryName] = stats.VacancyCategories.GetValueOrDefault(categoryName, 0) + 1;

        if (analysis.DetectedTechnologies != null && analysis.DetectedTechnologies.Any())
        {
            foreach (var tech in analysis.DetectedTechnologies)
            {
                stats.ModernTechCount[tech] = stats.ModernTechCount.GetValueOrDefault(tech, 0) + 1;
            }
        }

        switch (analysis.DetectedExperienceLevel)
        {
            case ExperienceLevel.Junior:
                stats.JuniorLevel++;
                break;
            case ExperienceLevel.Middle:
                stats.MiddleLevel++;
                break;
            case ExperienceLevel.Senior:
            case ExperienceLevel.Lead:
                stats.SeniorLevel++;
                break;
            default:
                stats.UnspecifiedLevel++;
                break;
        }

        switch (analysis.VacancyCategory)
        {
            case VacancyCategory.Desktop:
                stats.WithDesktopApps++;
                break;
            case VacancyCategory.Frontend:
                stats.WithFrontend++;
                break;
        }
    }

    public TechnologyStatistics GetAiTechnologyStatisticsFromAnalyses(List<VacancyAnalysisResult> analyses)
    {
        return GetAiTechnologyStatisticsFromAnalysesWithVacancies(analyses, new List<VacancyMatch>());
    }

    public TechnologyStatistics GetAiTechnologyStatisticsFromAnalysesWithVacancies(List<VacancyAnalysisResult> analyses, List<VacancyMatch> allMatches)
    {
        var stats = new TechnologyStatistics
        {
            Total = analyses.Count,
            ModernTechCount = new Dictionary<string, int>(),
            OutdatedTechCount = new Dictionary<string, int>(),
            DesktopKeywordCount = new Dictionary<string, int>(),
            FrontendKeywordCount = new Dictionary<string, int>(),
            YearsRequirements = new Dictionary<string, int>(),
            VacancyCategories = new Dictionary<string, int>(),
            ModernVacancies = new List<VacancyMatch>()
        };

        foreach (var analysis in analyses)
        {
            if (analysis.IsModernStack ?? false)
                stats.WithModernTech++;

            if (analysis.HasNoTimeTracker == false)
                stats.WithTimeTracker++;

            UpdateStatisticsFromAiAnalysis(stats, analysis);
        }

        stats.ModernVacancies = allMatches
            .Where(m => m.Analysis.IsModernStack ?? false)
            .OrderByDescending(m => m.Analysis.MatchScore)
            .ToList();

        return stats;
    }

    public async Task<(AnalysisReport report, List<VacancyAnalysisResult> allAnalyses, List<VacancyMatch> allMatches)> AnalyzeVacanciesWithProgressAsync(
        List<Vacancy> vacancies,
        Func<string, int, Task> progressCallback,
        CancellationToken cancellationToken = default)
    {
        var matches = new List<VacancyMatch>();
        var allMatches = new List<VacancyMatch>();
        var allAnalyses = new List<VacancyAnalysisResult>();
        var totalVacancies = vacancies.Count;

        for (int i = 0; i < vacancies.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var vacancy = vacancies[i];
            var progressPercent = 30 + (int)((i / (double)totalVacancies) * 30);

            var truncatedTitle = vacancy.Title.Length > 50
                ? vacancy.Title.Substring(0, 47) + "..."
                : vacancy.Title;

            await progressCallback($"Analyzing vacancy {i + 1}/{totalVacancies}: {truncatedTitle}", progressPercent);

            _logger.LogInformation("Analyzing vacancy {Index}/{Total}: {Title} at {Company}",
                i + 1, totalVacancies, vacancy.Title, vacancy.Company);

            try
            {
                var analysis = await AnalyzeVacancyAsync(vacancy, cancellationToken);
                allAnalyses.Add(analysis);

                var vacancyMatch = new VacancyMatch
                {
                    Vacancy = vacancy,
                    Analysis = analysis
                };
                allMatches.Add(vacancyMatch);

                _logger.LogInformation("Vacancy analysis completed: {Title} - Category: {Category}, Score: {Score}, Match: {IsMatch}",
                    vacancy.Title, analysis.VacancyCategory, analysis.MatchScore,
                    (analysis.IsBackendSuitable ?? false) && (analysis.IsModernStack ?? false));

                if (IsVacancyMatch(analysis))
                {
                    matches.Add(vacancyMatch);
                    _logger.LogInformation("✅ Vacancy {Title} matched criteria!", vacancy.Title);
                }
                else
                {
                    _logger.LogInformation("❌ Vacancy {Title} did not match criteria: Backend={Backend}, Modern={Modern}, Middle={Middle}, English={English}, NoTracker={NoTracker}",
                        vacancy.Title,
                        analysis.IsBackendSuitable ?? false,
                        analysis.IsModernStack ?? false,
                        analysis.IsMiddleLevel ?? false,
                        analysis.HasAcceptableEnglish ?? false,
                        analysis.HasNoTimeTracker ?? true);
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
            MatchPercentage = totalVacancies > 0 ? (matches.Count * 100.0) / totalVacancies : 0,
            Matches = matches.OrderByDescending(m => m.Analysis.MatchScore).ToList()
        };

        _logger.LogInformation("📊 Analysis Summary: {Total} total vacancies, {Matching} matching, {Percentage:F1}% match rate",
            report.TotalVacancies, report.MatchingVacancies, report.MatchPercentage);

        return (report, allAnalyses, allMatches);
    }

    private bool IsVacancyMatch(VacancyAnalysisResult analysis)
    {
        var score = 0;
        var reasons = new List<string>();

        if (analysis.IsBackendSuitable ?? false)
        {
            score += 2;
            reasons.Add("Backend suitable (+2)");
        }
        else
        {
            reasons.Add("Not backend suitable (0)");
        }

        if (analysis.IsModernStack ?? false)
        {
            score += 2;
            reasons.Add("Modern stack (+2)");
        }
        else
        {
            reasons.Add("Not modern stack (0)");
        }

        if (analysis.IsMiddleLevel ?? false)
        {
            score += 1;
            reasons.Add("Middle level (+1)");
        }
        else
        {
            reasons.Add("Not middle level (0)");
        }

        if (analysis.HasAcceptableEnglish ?? false)
        {
            score += 1;
            reasons.Add("Good English (+1)");
        }
        else
        {
            reasons.Add("English not acceptable (0)");
        }

        if (analysis.HasNoTimeTracker ?? true)
        {
            score += 1;
            reasons.Add("No time tracker (+1)");
        }
        else
        {
            reasons.Add("Has time tracker (0)");
        }

        var isMatch = score >= 4;
        _logger.LogInformation("Match scoring: {Score}/7, Match: {IsMatch}. Details: {Reasons}",
            score, isMatch, string.Join(", ", reasons));

        return isMatch;
    }
}