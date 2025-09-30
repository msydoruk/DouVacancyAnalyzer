namespace DouVacancyAnalyzer.Core.Application.DTOs;

public class AnalysisReport
{
    public int TotalVacancies { get; set; }
    public int MatchingVacancies { get; set; }
    public double MatchPercentage { get; set; }
    public List<VacancyMatch> Matches { get; set; } = new();
}