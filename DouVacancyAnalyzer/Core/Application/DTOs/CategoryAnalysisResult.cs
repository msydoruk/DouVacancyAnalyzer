namespace DouVacancyAnalyzer.Core.Application.DTOs;

using DouVacancyAnalyzer.Core.Domain.Enums;

public class CategoryAnalysisResult
{
    public VacancyCategory VacancyCategory { get; set; }
    public int Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}
