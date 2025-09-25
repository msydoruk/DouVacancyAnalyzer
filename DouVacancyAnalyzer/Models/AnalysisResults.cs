namespace DouVacancyAnalyzer.Models;

public class CategoryAnalysisResult
{
    public VacancyCategory VacancyCategory { get; set; }
    public int Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

public class TechnologyAnalysisResult
{
    public bool IsModernStack { get; set; }
    public List<string> DetectedTechnologies { get; set; } = new();
    public int TechnologyScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

public class ExperienceAnalysisResult
{
    public ExperienceLevel DetectedExperienceLevel { get; set; }
    public bool IsMiddleLevel { get; set; }
    public int ExperienceScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

public class EnglishAnalysisResult
{
    public EnglishLevel DetectedEnglishLevel { get; set; }
    public bool HasAcceptableEnglish { get; set; }
    public int EnglishScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

public class SuitabilityAnalysisResult
{
    public bool IsBackendSuitable { get; set; }
    public bool HasNoTimeTracker { get; set; }
    public int MatchScore { get; set; }
    public string AnalysisReason { get; set; } = string.Empty;
}