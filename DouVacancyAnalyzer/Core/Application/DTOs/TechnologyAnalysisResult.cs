namespace DouVacancyAnalyzer.Core.Application.DTOs;

public class TechnologyAnalysisResult
{
    public bool IsModernStack { get; set; }
    public List<string> DetectedTechnologies { get; set; } = new();
    public int TechnologyScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}
