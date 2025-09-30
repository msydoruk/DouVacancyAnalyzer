namespace DouVacancyAnalyzer.Core.Application.DTOs;

public class SuitabilityAnalysisResult
{
    public bool IsBackendSuitable { get; set; }
    public bool HasNoTimeTracker { get; set; }
    public int MatchScore { get; set; }
    public string AnalysisReason { get; set; } = string.Empty;
}
