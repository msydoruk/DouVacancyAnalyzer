namespace DouVacancyAnalyzer.Core.Application.DTOs;

using DouVacancyAnalyzer.Core.Domain.Enums;

public class ExperienceAnalysisResult
{
    public ExperienceLevel DetectedExperienceLevel { get; set; }
    public string? DetectedYearsOfExperience { get; set; }
    public bool IsMiddleLevel { get; set; }
    public int ExperienceScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}
