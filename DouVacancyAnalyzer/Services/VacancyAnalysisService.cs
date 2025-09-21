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
                _logger.LogWarning("OpenAI client not available, using fallback analysis");
                return CreateFallbackAnalysis(vacancy);
            }

            var aiAnalysis = await PerformAiAnalysisAsync(vacancy, cancellationToken);

            if (aiAnalysis != null)
            {
                PopulateCriteriaMatch(aiAnalysis);
                return aiAnalysis;
            }

            _logger.LogWarning("AI analysis failed for vacancy {Title}, using fallback", vacancy.Title);
            return CreateFallbackAnalysis(vacancy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing vacancy {Title}", vacancy.Title);
            return CreateErrorResult(ex.Message);
        }
    }

    public async Task<AnalysisReport> AnalyzeVacanciesAsync(List<Vacancy> vacancies, CancellationToken cancellationToken = default)
    {
        var matches = new List<VacancyMatch>();

        foreach (var vacancy in vacancies)
        {
            var analysis = await AnalyzeVacancyAsync(vacancy, cancellationToken);

            if (IsMatch(analysis))
            {
                matches.Add(new VacancyMatch
                {
                    Vacancy = vacancy,
                    Analysis = analysis
                });
            }
        }

        return new AnalysisReport
        {
            TotalVacancies = vacancies.Count,
            MatchingVacancies = matches.Count,
            Matches = matches.OrderByDescending(m => m.Analysis.MatchScore).ToList()
        };
    }

    public TechnologyStatistics GetTechnologyStatistics(List<Vacancy> vacancies)
    {
        var stats = new TechnologyStatistics
        {
            Total = vacancies.Count,
            ModernTechCount = new Dictionary<string, int>(),
            OutdatedTechCount = new Dictionary<string, int>(),
            DesktopKeywordCount = new Dictionary<string, int>(),
            FrontendKeywordCount = new Dictionary<string, int>(),
            YearsRequirements = new Dictionary<string, int>(),
            VacancyCategories = new Dictionary<string, int>()
        };

        foreach (var vacancy in vacancies)
        {
            var text = $"{vacancy.Title} {vacancy.Description}".ToLowerInvariant();

            if (text.Contains(".net core") || text.Contains("asp.net core") || text.Contains("docker") || text.Contains("kubernetes"))
            {
                stats.WithModernTech++;
            }

            if (text.Contains("time track") || text.Contains("time-track"))
            {
                stats.WithTimeTracker++;
            }

            if (text.Contains("desktop") || text.Contains("wpf") || text.Contains("winforms"))
            {
                stats.WithDesktopApps++;
            }

            if (text.Contains("react") || text.Contains("angular") || text.Contains("vue") || text.Contains("frontend"))
            {
                stats.WithFrontend++;
            }
        }

        return stats;
    }

    public async Task<TechnologyStatistics> GetAiTechnologyStatisticsAsync(List<Vacancy> vacancies, CancellationToken cancellationToken = default)
    {
        var stats = new TechnologyStatistics
        {
            Total = vacancies.Count,
            ModernTechCount = new Dictionary<string, int>(),
            OutdatedTechCount = new Dictionary<string, int>(),
            DesktopKeywordCount = new Dictionary<string, int>(),
            FrontendKeywordCount = new Dictionary<string, int>(),
            YearsRequirements = new Dictionary<string, int>(),
            VacancyCategories = new Dictionary<string, int>()
        };

        if (_openAiClient == null)
            return stats;

        try
        {
            var allAnalyses = new List<VacancyAnalysisResult>();

            foreach (var vacancy in vacancies)
            {
                var analysis = await AnalyzeVacancyAsync(vacancy, cancellationToken);
                allAnalyses.Add(analysis);

                if (analysis.IsModernStack ?? false)
                    stats.WithModernTech++;

                if (analysis.HasNoTimeTracker == false)
                    stats.WithTimeTracker++;

                UpdateStatisticsFromAiAnalysis(stats, analysis);
            }

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get AI statistics");
            return stats;
        }
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

                jsonString = jsonString
                    .Replace("\"Not specified\"", "\"Unspecified\"")
                    .Replace("\"not specified\"", "\"Unspecified\"")
                    .Replace("\"Senior\"", "\"Senior\"")
                    .Replace("\"Middle\"", "\"Middle\"")
                    .Replace("\"Junior\"", "\"Junior\"");

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

    private double CalculateMatchScore(VacancyAnalysisResult analysis)
    {
        var score = 0.0;

        if (analysis.IsModernStack ?? false) score += 30;
        if (analysis.IsMiddleLevel ?? false) score += 25;
        if (analysis.HasAcceptableEnglish ?? false) score += 25;
        if (analysis.HasNoTimeTracker ?? true) score += 20;

        return score;
    }

    private bool IsMatch(VacancyAnalysisResult analysis)
    {
        return (analysis.IsBackendSuitable ?? false) &&
               (analysis.IsModernStack ?? false) &&
               (analysis.IsMiddleLevel ?? false) &&
               (analysis.HasAcceptableEnglish ?? false) &&
               (analysis.HasNoTimeTracker ?? true);
    }


    private VacancyAnalysisResult CreateFallbackAnalysis(Vacancy vacancy)
    {
        return new VacancyAnalysisResult
        {
            VacancyCategory = VacancyCategory.Other,
            DetectedExperienceLevel = ExperienceLevel.Unspecified,
            DetectedEnglishLevel = EnglishLevel.Unspecified,
            IsModernStack = false,
            IsMiddleLevel = false,
            HasAcceptableEnglish = null,
            HasNoTimeTracker = null,
            IsBackendSuitable = false,
            AnalysisReason = "AI аналіз недоступний, використано fallback",
            MatchScore = 0,
            DetectedTechnologies = new List<string>()
        };
    }

    private VacancyAnalysisResult CreateErrorResult(string error)
    {
        return new VacancyAnalysisResult
        {
            AnalysisReason = $"Помилка аналізу: {error}",
            MatchScore = 0,
            VacancyCategory = VacancyCategory.Other,
            DetectedExperienceLevel = ExperienceLevel.Unspecified,
            DetectedEnglishLevel = EnglishLevel.Unspecified,
            DetectedTechnologies = new List<string>()
        };
    }

    private void UpdateStatisticsFromAiAnalysis(TechnologyStatistics stats, VacancyAnalysisResult analysis)
    {
        var categoryName = analysis.VacancyCategory.ToString();
        stats.VacancyCategories[categoryName] = stats.VacancyCategories.GetValueOrDefault(categoryName, 0) + 1;

        foreach (var tech in analysis.DetectedTechnologies)
        {
            stats.ModernTechCount[tech] = stats.ModernTechCount.GetValueOrDefault(tech, 0) + 1;
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
        var stats = new TechnologyStatistics
        {
            Total = analyses.Count,
            ModernTechCount = new Dictionary<string, int>(),
            OutdatedTechCount = new Dictionary<string, int>(),
            DesktopKeywordCount = new Dictionary<string, int>(),
            FrontendKeywordCount = new Dictionary<string, int>(),
            YearsRequirements = new Dictionary<string, int>(),
            VacancyCategories = new Dictionary<string, int>()
        };

        foreach (var analysis in analyses)
        {
            if (analysis.IsModernStack ?? false)
                stats.WithModernTech++;

            if (analysis.HasNoTimeTracker == false)
                stats.WithTimeTracker++;

            UpdateStatisticsFromAiAnalysis(stats, analysis);
        }

        return stats;
    }
}