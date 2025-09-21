using System.Text.Json.Serialization;

namespace DouVacancyAnalyzer.Models;

public class VacancyAnalysisResult
{
    public bool? IsModernStack { get; set; }
    public bool? IsMiddleLevel { get; set; }
    public bool? HasAcceptableEnglish { get; set; }
    public bool? HasNoTimeTracker { get; set; }
    public string AnalysisReason { get; set; } = string.Empty;
    public double MatchScore { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VacancyCategory VacancyCategory { get; set; } = VacancyCategory.Other;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ExperienceLevel DetectedExperienceLevel { get; set; } = ExperienceLevel.Unspecified;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EnglishLevel DetectedEnglishLevel { get; set; } = EnglishLevel.Unspecified;

    public bool? IsBackendSuitable { get; set; }
    public List<string> DetectedTechnologies { get; set; } = new();
    public Dictionary<MatchCriteria, bool> CriteriaMatch { get; set; } = new();
}


