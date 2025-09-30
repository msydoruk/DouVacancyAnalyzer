using DouVacancyAnalyzer.Models;
using DouVacancyAnalyzer.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace DouVacancyAnalyzer.Services;

public class VacancyAnalysisService : IVacancyAnalysisService
{
    private readonly IAiClient _aiClient;
    private readonly ILogger<VacancyAnalysisService> _logger;
    private readonly AnalysisPrompts _prompts;
    private readonly IServiceScopeFactory _scopeFactory;

    public VacancyAnalysisService(
        IAiClient aiClient,
        ILogger<VacancyAnalysisService> logger,
        IOptions<AnalysisPrompts> prompts,
        IServiceScopeFactory scopeFactory)
    {
        _aiClient = aiClient;
        _logger = logger;
        _prompts = prompts.Value;
        _scopeFactory = scopeFactory;
    }

    public async Task<VacancyAnalysisResult> AnalyzeVacancyAsync(Vacancy vacancy, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_aiClient == null)
            {
                _logger.LogError("AI client not available - AI analysis is required");
                throw new InvalidOperationException("AI analysis is required but AI client is not configured");
            }

            var aiAnalysis = await PerformMultiStageAnalysisAsync(vacancy, cancellationToken);

            if (aiAnalysis != null)
            {
                PopulateCriteriaMatch(aiAnalysis);
                return aiAnalysis;
            }

            _logger.LogError("AI analysis failed for vacancy {Title} - retrying", vacancy.Title);

            await Task.Delay(1000, cancellationToken);
            aiAnalysis = await PerformMultiStageAnalysisAsync(vacancy, cancellationToken);

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


    private async Task<VacancyAnalysisResult?> PerformMultiStageAnalysisAsync(Vacancy vacancy, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting multi-stage analysis for vacancy: {Title} (Description length: {Length} chars)",
                vacancy.Title, vacancy.Description?.Length ?? 0);

            // Stage 1: Category Analysis
            var categoryResult = await PerformCategoryAnalysisAsync(vacancy, cancellationToken);

            // Stage 2: Technology Analysis
            var technologyResult = await PerformTechnologyAnalysisAsync(vacancy, cancellationToken);

            // Stage 3: Experience Analysis
            var experienceResult = await PerformExperienceAnalysisAsync(vacancy, cancellationToken);

            // Stage 4: English Analysis
            var englishResult = await PerformEnglishAnalysisAsync(vacancy, cancellationToken);

            // Stage 5: Suitability Analysis
            var suitabilityResult = await PerformSuitabilityAnalysisAsync(vacancy, cancellationToken);

            // Combine all results
            return CombineAnalysisResults(categoryResult, technologyResult, experienceResult, englishResult, suitabilityResult);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Multi-stage AI analysis failed for vacancy {Title}", vacancy.Title);
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

    private string CleanJsonString(string jsonString)
    {
        // Replace unescaped newlines and other control characters within JSON strings
        // This is a simple approach - we'll use regex to find string values and escape them
        return System.Text.RegularExpressions.Regex.Replace(
            jsonString,
            @":\s*""([^""]*?)""",
            m => {
                var value = m.Groups[1].Value;
                // Escape newlines, carriage returns, tabs
                value = value.Replace("\n", "\\n")
                            .Replace("\r", "\\r")
                            .Replace("\t", "\\t");
                return $": \"{value}\"";
            },
            System.Text.RegularExpressions.RegexOptions.Singleline
        );
    }

    private async Task<CategoryAnalysisResult?> PerformCategoryAnalysisAsync(Vacancy vacancy, CancellationToken cancellationToken)
    {
        try
        {
            var userPrompt = _prompts.CategoryAnalysis.UserPromptTemplate
                .Replace("{title}", vacancy.Title)
                .Replace("{description}", vacancy.Description);

            var responseText = await _aiClient.CompleteChatAsync(
                _prompts.CategoryAnalysis.SystemPrompt,
                userPrompt,
                cancellationToken);

            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                jsonString = CleanJsonString(jsonString);

                try
                {
                    return JsonSerializer.Deserialize<CategoryAnalysisResult>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                    });
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize CategoryAnalysisResult JSON: {JsonString}", jsonString);

                    // Try to parse manually with fallback values
                    return ParseCategoryAnalysisManually(jsonString);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Category analysis failed for vacancy {Title}", vacancy.Title);
        }

        return null;
    }

    private async Task<TechnologyAnalysisResult?> PerformTechnologyAnalysisAsync(Vacancy vacancy, CancellationToken cancellationToken)
    {
        try
        {
            var userPrompt = _prompts.TechnologyAnalysis.UserPromptTemplate
                .Replace("{title}", vacancy.Title)
                .Replace("{description}", vacancy.Description);

            var responseText = await _aiClient.CompleteChatAsync(
                _prompts.TechnologyAnalysis.SystemPrompt,
                userPrompt,
                cancellationToken);

            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                jsonString = CleanJsonString(jsonString);

                return JsonSerializer.Deserialize<TechnologyAnalysisResult>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Technology analysis failed for vacancy {Title}", vacancy.Title);
        }

        return null;
    }

    private async Task<ExperienceAnalysisResult?> PerformExperienceAnalysisAsync(Vacancy vacancy, CancellationToken cancellationToken)
    {
        try
        {
            var userPrompt = _prompts.ExperienceAnalysis.UserPromptTemplate
                .Replace("{title}", vacancy.Title)
                .Replace("{experience}", "") // No longer parsing experience from Vacancy
                .Replace("{description}", vacancy.Description);

            var responseText = await _aiClient.CompleteChatAsync(
                _prompts.ExperienceAnalysis.SystemPrompt,
                userPrompt,
                cancellationToken);

            _logger.LogDebug("🤖 Experience AI Response for '{Title}': {Response}",
                vacancy.Title.Length > 50 ? vacancy.Title[..50] + "..." : vacancy.Title,
                responseText);

            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                jsonString = CleanJsonString(jsonString);

                try
                {
                    return JsonSerializer.Deserialize<ExperienceAnalysisResult>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                    });
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize ExperienceAnalysisResult JSON: {JsonString}", jsonString);
                    return ParseExperienceAnalysisManually(jsonString);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Experience analysis failed for vacancy {Title}", vacancy.Title);
        }

        return null;
    }

    private async Task<EnglishAnalysisResult?> PerformEnglishAnalysisAsync(Vacancy vacancy, CancellationToken cancellationToken)
    {
        try
        {
            var userPrompt = _prompts.EnglishAnalysis.UserPromptTemplate
                .Replace("{title}", vacancy.Title)
                .Replace("{englishLevel}", "") // No longer parsing English level from Vacancy
                .Replace("{description}", vacancy.Description);

            var responseText = await _aiClient.CompleteChatAsync(
                _prompts.EnglishAnalysis.SystemPrompt,
                userPrompt,
                cancellationToken);

            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                jsonString = CleanJsonString(jsonString);

                try
                {
                    return JsonSerializer.Deserialize<EnglishAnalysisResult>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                    });
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize EnglishAnalysisResult JSON: {JsonString}", jsonString);
                    return ParseEnglishAnalysisManually(jsonString);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "English analysis failed for vacancy {Title}", vacancy.Title);
        }

        return null;
    }

    private async Task<SuitabilityAnalysisResult?> PerformSuitabilityAnalysisAsync(Vacancy vacancy, CancellationToken cancellationToken)
    {
        try
        {
            var userPrompt = _prompts.SuitabilityAnalysis.UserPromptTemplate
                .Replace("{title}", vacancy.Title)
                .Replace("{company}", vacancy.Company)
                .Replace("{description}", vacancy.Description)
                .Replace("{location}", vacancy.Location);

            var responseText = await _aiClient.CompleteChatAsync(
                _prompts.SuitabilityAnalysis.SystemPrompt,
                userPrompt,
                cancellationToken);

            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                jsonString = CleanJsonString(jsonString);

                try
                {
                    return JsonSerializer.Deserialize<SuitabilityAnalysisResult>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize SuitabilityAnalysisResult JSON: {JsonString}", jsonString);
                    return ParseSuitabilityAnalysisManually(jsonString);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Suitability analysis failed for vacancy {Title}", vacancy.Title);
        }

        return null;
    }

    private VacancyAnalysisResult? CombineAnalysisResults(
        CategoryAnalysisResult? category,
        TechnologyAnalysisResult? technology,
        ExperienceAnalysisResult? experience,
        EnglishAnalysisResult? english,
        SuitabilityAnalysisResult? suitability)
    {
        if (category == null || technology == null || experience == null ||
            english == null || suitability == null)
        {
            return null;
        }

        var combinedReasons = new List<string>();

        if (!string.IsNullOrEmpty(category.Reasoning))
            combinedReasons.Add($"Category: {category.Reasoning}");
        if (!string.IsNullOrEmpty(technology.Reasoning))
            combinedReasons.Add($"Technology: {technology.Reasoning}");
        if (!string.IsNullOrEmpty(experience.Reasoning))
            combinedReasons.Add($"Experience: {experience.Reasoning}");
        if (!string.IsNullOrEmpty(english.Reasoning))
            combinedReasons.Add($"English: {english.Reasoning}");
        if (!string.IsNullOrEmpty(suitability.AnalysisReason))
            combinedReasons.Add($"Suitability: {suitability.AnalysisReason}");

        return new VacancyAnalysisResult
        {
            VacancyCategory = category.VacancyCategory,
            DetectedExperienceLevel = experience.DetectedExperienceLevel,
            DetectedYearsOfExperience = experience.DetectedYearsOfExperience,
            DetectedEnglishLevel = english.DetectedEnglishLevel,
            IsModernStack = technology.IsModernStack,
            IsMiddleLevel = experience.IsMiddleLevel,
            HasAcceptableEnglish = english.HasAcceptableEnglish,
            HasNoTimeTracker = suitability.HasNoTimeTracker,
            IsBackendSuitable = suitability.IsBackendSuitable,
            AnalysisReason = string.Join(" | ", combinedReasons),
            MatchScore = suitability.MatchScore,
            DetectedTechnologies = technology.DetectedTechnologies ?? new List<string>()
        };
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

    public async Task<(AnalysisReport report, List<VacancyAnalysisResult> allAnalyses, List<VacancyMatch> allMatches)> AnalyzeVacanciesAsync(
        List<Vacancy> vacancies,
        Func<string, int, Task> progressCallback,
        CancellationToken cancellationToken = default)
    {
        var totalVacancies = vacancies.Count;
        _logger.LogInformation("🚀 Starting sequential analysis of {Count} vacancies", totalVacancies);

        var allMatches = new List<VacancyMatch>();
        var allAnalyses = new List<VacancyAnalysisResult>();
        var matches = new List<VacancyMatch>();
        var processedCount = 0;

        await progressCallback("🔄 Starting sequential analysis...", 30);

        // Process vacancies sequentially
        for (int index = 0; index < vacancies.Count; index++)
        {
            var vacancy = vacancies[index];
            var truncatedTitle = vacancy.Title.Length > 50
                ? vacancy.Title.Substring(0, 47) + "..."
                : vacancy.Title;

            _logger.LogInformation("🤖 Analyzing vacancy {Index}/{Total}: {Title}",
                index + 1, totalVacancies, truncatedTitle);

            try
            {
                var analysis = await AnalyzeVacancyAsync(vacancy, cancellationToken);

                // Save to database using a new scope
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<VacancyDbContext>();

                    var dbVacancy = await dbContext.Vacancies
                        .FirstOrDefaultAsync(v => v.Url == vacancy.Url, cancellationToken);

                    if (dbVacancy != null)
                    {
                        await UpdateVacancyAnalysisInDbWithContext(dbContext, dbVacancy, analysis);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save analysis for {Title}", vacancy.Title);
                }

                var match = new VacancyMatch
                {
                    Vacancy = vacancy,
                    Analysis = analysis
                };

                allAnalyses.Add(analysis);
                allMatches.Add(match);

                if (IsVacancyMatch(analysis))
                {
                    matches.Add(match);
                }

                processedCount++;
                var progressPercent = 30 + (int)((processedCount / (double)totalVacancies) * 60);

                await progressCallback($"🤖 Analyzed {processedCount}/{totalVacancies}: {truncatedTitle}", progressPercent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to analyze vacancy {Index}/{Total}: {Title}",
                    index + 1, totalVacancies, vacancy.Title);

                var fallbackAnalysis = new VacancyAnalysisResult
                {
                    VacancyCategory = VacancyCategory.Other,
                    DetectedExperienceLevel = ExperienceLevel.Unspecified,
                    DetectedEnglishLevel = EnglishLevel.Unspecified,
                    IsModernStack = false,
                    IsMiddleLevel = false,
                    HasAcceptableEnglish = false,
                    HasNoTimeTracker = true,
                    IsBackendSuitable = false,
                    AnalysisReason = $"Analysis failed: {ex.Message}",
                    MatchScore = 0,
                    DetectedTechnologies = new List<string>()
                };

                var fallbackMatch = new VacancyMatch
                {
                    Vacancy = vacancy,
                    Analysis = fallbackAnalysis
                };

                allAnalyses.Add(fallbackAnalysis);
                allMatches.Add(fallbackMatch);
                processedCount++;
            }
        }

        _logger.LogInformation("✅ Sequential analysis completed: {Processed}/{Total} vacancies processed, {Matches} matches found",
            processedCount, totalVacancies, matches.Count);

        var report = new AnalysisReport
        {
            TotalVacancies = totalVacancies,
            MatchingVacancies = matches.Count,
            MatchPercentage = totalVacancies > 0 ? (matches.Count * 100.0) / totalVacancies : 0,
            Matches = matches.OrderByDescending(m => m.Analysis.MatchScore).ToList()
        };

        return (report, allAnalyses, allMatches);
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


    private bool IsVacancyMatch(VacancyAnalysisResult analysis)
    {
        var reasons = new List<string>();

        // CRITICAL REQUIREMENTS - ALL must be met

        // 1. Must be Backend suitable
        var isBackendSuitable = analysis.IsBackendSuitable ?? false;
        if (!isBackendSuitable)
        {
            reasons.Add("❌ NOT Backend suitable - REJECTED");
            _logger.LogInformation("Match result: FALSE. Critical requirement failed: Not backend suitable. Details: {Reasons}",
                string.Join(", ", reasons));
            return false;
        }
        reasons.Add("✅ Backend suitable");

        // 2. Check if it's Fullstack but needs strong Backend focus
        if (analysis.VacancyCategory == VacancyCategory.Fullstack)
        {
            // For Fullstack, we need high match score and explicit backend mention
            var matchScore = analysis.MatchScore;
            if (matchScore < 70)
            {
                reasons.Add("❌ Fullstack with low backend focus - REJECTED");
                _logger.LogInformation("Match result: FALSE. Fullstack with insufficient backend focus ({Score}). Details: {Reasons}",
                    matchScore, string.Join(", ", reasons));
                return false;
            }
            reasons.Add($"✅ Fullstack with strong backend focus (score: {matchScore})");
        }

        // 3. Experience level check - NO Senior/Lead positions
        var experienceLevel = analysis.DetectedExperienceLevel;
        if (experienceLevel == ExperienceLevel.Senior || experienceLevel == ExperienceLevel.Lead)
        {
            reasons.Add($"❌ Position is {experienceLevel} level - REJECTED (looking for Middle level)");
            _logger.LogInformation("Match result: FALSE. Experience level too high: {Level}. Details: {Reasons}",
                experienceLevel, string.Join(", ", reasons));
            return false;
        }

        // 4. Must be suitable for Middle level (3+ years experience)
        var isMiddleLevel = analysis.IsMiddleLevel ?? false;
        if (!isMiddleLevel && experienceLevel != ExperienceLevel.Middle && experienceLevel != ExperienceLevel.Unspecified)
        {
            reasons.Add("❌ Not suitable for Middle level - REJECTED");
            _logger.LogInformation("Match result: FALSE. Not suitable for Middle level. Experience: {Level}, IsMiddleLevel: {IsMiddle}. Details: {Reasons}",
                experienceLevel, isMiddleLevel, string.Join(", ", reasons));
            return false;
        }
        reasons.Add("✅ Suitable for Middle level");

        // PREFERRED REQUIREMENTS - Good to have but not critical
        var bonusScore = 0;

        if (analysis.IsModernStack ?? false)
        {
            bonusScore += 2;
            reasons.Add("✅ Modern stack (+2)");
        }
        else
        {
            reasons.Add("⚠️ Not modern stack");
        }

        if (analysis.HasAcceptableEnglish ?? false)
        {
            bonusScore += 1;
            reasons.Add("✅ Acceptable English (+1)");
        }
        else
        {
            reasons.Add("⚠️ English level may be insufficient");
        }

        if (analysis.HasNoTimeTracker ?? true)
        {
            bonusScore += 1;
            reasons.Add("✅ No time tracker (+1)");
        }
        else
        {
            reasons.Add("⚠️ Has time tracker requirement");
        }

        // Final decision
        var totalScore = bonusScore;
        var isMatch = true; // If we got here, critical requirements are met

        _logger.LogInformation("Match result: TRUE. Bonus score: {Score}/4. Details: {Reasons}",
            totalScore, string.Join(", ", reasons));

        return isMatch;
    }


    private async Task UpdateVacancyAnalysisInDb(VacancyEntity dbVacancy, VacancyAnalysisResult analysis)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VacancyDbContext>();

        await UpdateVacancyAnalysisInDbWithContext(dbContext, dbVacancy, analysis);
    }

    private async Task UpdateVacancyAnalysisInDbWithContext(VacancyDbContext dbContext, VacancyEntity dbVacancy, VacancyAnalysisResult analysis)
    {
        dbVacancy.VacancyCategory = analysis.VacancyCategory;
        dbVacancy.DetectedExperienceLevel = analysis.DetectedExperienceLevel;
        dbVacancy.DetectedYearsOfExperience = analysis.DetectedYearsOfExperience;
        dbVacancy.DetectedEnglishLevel = analysis.DetectedEnglishLevel;
        dbVacancy.IsModernStack = analysis.IsModernStack;
        dbVacancy.IsMiddleLevel = analysis.IsMiddleLevel;
        dbVacancy.HasAcceptableEnglish = analysis.HasAcceptableEnglish;
        dbVacancy.HasNoTimeTracker = analysis.HasNoTimeTracker;
        dbVacancy.IsBackendSuitable = analysis.IsBackendSuitable;
        dbVacancy.AnalysisReason = analysis.AnalysisReason;
        dbVacancy.MatchScore = (int?)analysis.MatchScore;
        dbVacancy.DetectedTechnologies = JsonSerializer.Serialize(analysis.DetectedTechnologies ?? new List<string>());
        dbVacancy.LastAnalyzedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
    }

    private CategoryAnalysisResult ParseCategoryAnalysisManually(string jsonString)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var categoryStr = root.TryGetProperty("VacancyCategory", out var categoryProp)
                ? categoryProp.GetString() ?? "Other"
                : "Other";

            // Map common AI responses to valid enum values
            var category = categoryStr.ToLower() switch
            {
                "backend" or "back-end" or "server-side" => VacancyCategory.Backend,
                "frontend" or "front-end" or "client-side" => VacancyCategory.Frontend,
                "fullstack" or "full-stack" or "full stack" => VacancyCategory.Fullstack,
                "desktop" or "windows" or "wpf" or "winforms" => VacancyCategory.Desktop,
                "devops" or "dev-ops" or "infrastructure" => VacancyCategory.DevOps,
                "qa" or "testing" or "quality assurance" => VacancyCategory.QA,
                "mobile" or "android" or "ios" => VacancyCategory.Mobile,
                "game" or "gamedev" or "game development" => VacancyCategory.GameDev,
                "data" or "datascience" or "data science" => VacancyCategory.DataScience,
                "security" or "cybersecurity" => VacancyCategory.Security,
                _ => VacancyCategory.Other
            };

            var confidence = root.TryGetProperty("Confidence", out var confProp) ? confProp.GetInt32() : 50;
            var reasoning = root.TryGetProperty("Reasoning", out var reasonProp) ? reasonProp.GetString() ?? "" : "";

            return new CategoryAnalysisResult
            {
                VacancyCategory = category,
                Confidence = confidence,
                Reasoning = reasoning
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Manual parsing failed for CategoryAnalysis: {JsonString}", jsonString);

            return new CategoryAnalysisResult
            {
                VacancyCategory = VacancyCategory.Other,
                Confidence = 0,
                Reasoning = "Failed to parse AI response"
            };
        }
    }

    private ExperienceAnalysisResult ParseExperienceAnalysisManually(string jsonString)
    {
        try
        {
            _logger.LogDebug("🔍 Parsing Experience Analysis JSON: {Json}", jsonString);

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var levelStr = root.TryGetProperty("DetectedExperienceLevel", out var levelProp)
                ? levelProp.GetString() ?? "Unspecified"
                : "Unspecified";

            var level = levelStr.ToLower() switch
            {
                "junior" or "intern" or "trainee" => ExperienceLevel.Junior,
                "middle" or "mid" or "intermediate" => ExperienceLevel.Middle,
                "senior" or "sr" => ExperienceLevel.Senior,
                "lead" or "team lead" or "principal" => ExperienceLevel.Lead,
                _ => ExperienceLevel.Unspecified
            };

            var yearsOfExperience = root.TryGetProperty("DetectedYearsOfExperience", out var yearsProp)
                ? yearsProp.GetString()
                : null;

            _logger.LogDebug("📅 Years detected: '{Years}' (Raw prop: {HasProp}, Value: '{Value}')",
                yearsOfExperience,
                root.TryGetProperty("DetectedYearsOfExperience", out _),
                yearsProp.ValueKind != JsonValueKind.Null && yearsProp.ValueKind != JsonValueKind.Undefined ? yearsProp.ToString() : "null");
            var isMiddle = root.TryGetProperty("IsMiddleLevel", out var middleProp) && middleProp.GetBoolean();
            var score = root.TryGetProperty("ExperienceScore", out var scoreProp) ? scoreProp.GetInt32() : 50;
            var reasoning = root.TryGetProperty("Reasoning", out var reasonProp) ? reasonProp.GetString() ?? "" : "";

            return new ExperienceAnalysisResult
            {
                DetectedExperienceLevel = level,
                DetectedYearsOfExperience = yearsOfExperience,
                IsMiddleLevel = isMiddle,
                ExperienceScore = score,
                Reasoning = reasoning
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Manual parsing failed for ExperienceAnalysis: {JsonString}", jsonString);

            return new ExperienceAnalysisResult
            {
                DetectedExperienceLevel = ExperienceLevel.Unspecified,
                IsMiddleLevel = false,
                ExperienceScore = 0,
                Reasoning = "Failed to parse AI response"
            };
        }
    }

    private EnglishAnalysisResult ParseEnglishAnalysisManually(string jsonString)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var levelStr = root.TryGetProperty("DetectedEnglishLevel", out var levelProp)
                ? levelProp.GetString() ?? "Unspecified"
                : "Unspecified";

            var level = levelStr.ToLower() switch
            {
                "beginner" or "a1" => EnglishLevel.Beginner,
                "elementary" or "a2" => EnglishLevel.Elementary,
                "preintermediate" or "pre-intermediate" or "b1-" => EnglishLevel.PreIntermediate,
                "intermediate" or "b1" => EnglishLevel.Intermediate,
                "upperintermediate" or "upper-intermediate" or "b2" => EnglishLevel.UpperIntermediate,
                "advanced" or "c1" => EnglishLevel.Advanced,
                "proficient" or "c2" or "native" => EnglishLevel.Proficient,
                _ => EnglishLevel.Unspecified
            };

            var hasAcceptable = root.TryGetProperty("HasAcceptableEnglish", out var acceptableProp) && acceptableProp.GetBoolean();
            var score = root.TryGetProperty("EnglishScore", out var scoreProp) ? scoreProp.GetInt32() : 50;
            var reasoning = root.TryGetProperty("Reasoning", out var reasonProp) ? reasonProp.GetString() ?? "" : "";

            return new EnglishAnalysisResult
            {
                DetectedEnglishLevel = level,
                HasAcceptableEnglish = hasAcceptable,
                EnglishScore = score,
                Reasoning = reasoning
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Manual parsing failed for EnglishAnalysis: {JsonString}", jsonString);

            return new EnglishAnalysisResult
            {
                DetectedEnglishLevel = EnglishLevel.Unspecified,
                HasAcceptableEnglish = false,
                EnglishScore = 0,
                Reasoning = "Failed to parse AI response"
            };
        }
    }

    private SuitabilityAnalysisResult ParseSuitabilityAnalysisManually(string jsonString)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var isBackendSuitable = root.TryGetProperty("IsBackendSuitable", out var backendProp) && backendProp.GetBoolean();
            var hasNoTimeTracker = !root.TryGetProperty("HasNoTimeTracker", out var trackerProp) || trackerProp.GetBoolean();
            var matchScore = root.TryGetProperty("MatchScore", out var scoreProp) ? scoreProp.GetInt32() : 50;
            var analysisReason = root.TryGetProperty("AnalysisReason", out var reasonProp) ? reasonProp.GetString() ?? "" : "";

            return new SuitabilityAnalysisResult
            {
                IsBackendSuitable = isBackendSuitable,
                HasNoTimeTracker = hasNoTimeTracker,
                MatchScore = matchScore,
                AnalysisReason = analysisReason
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Manual parsing failed for SuitabilityAnalysis: {JsonString}", jsonString);

            return new SuitabilityAnalysisResult
            {
                IsBackendSuitable = false,
                HasNoTimeTracker = true,
                MatchScore = 0,
                AnalysisReason = "Failed to parse AI response"
            };
        }
    }
}