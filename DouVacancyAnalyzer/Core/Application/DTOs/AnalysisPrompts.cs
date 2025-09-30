namespace DouVacancyAnalyzer.Core.Application.DTOs;

public class AnalysisPrompts
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPromptTemplate { get; set; } = string.Empty;
    public CategoryAnalysisPrompts CategoryAnalysis { get; set; } = new();
    public TechnologyAnalysisPrompts TechnologyAnalysis { get; set; } = new();
    public ExperienceAnalysisPrompts ExperienceAnalysis { get; set; } = new();
    public EnglishAnalysisPrompts EnglishAnalysis { get; set; } = new();
    public SuitabilityAnalysisPrompts SuitabilityAnalysis { get; set; } = new();
}
