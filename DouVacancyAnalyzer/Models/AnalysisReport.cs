namespace DouVacancyAnalyzer.Models;

public class AnalysisReport
{
    public int TotalVacancies { get; set; }
    public int MatchingVacancies { get; set; }
    public double MatchPercentage => TotalVacancies > 0 ? (MatchingVacancies * 100.0) / TotalVacancies : 0;
    public List<VacancyMatch> Matches { get; set; } = new();
}