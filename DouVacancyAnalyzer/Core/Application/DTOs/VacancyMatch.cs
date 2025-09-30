namespace DouVacancyAnalyzer.Core.Application.DTOs;

public class VacancyMatch
{
    public Vacancy Vacancy { get; set; } = new();
    public VacancyAnalysisResult Analysis { get; set; } = new();
}