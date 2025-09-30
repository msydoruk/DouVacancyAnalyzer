namespace DouVacancyAnalyzer.Core.Application.DTOs;

using DouVacancyAnalyzer.Core.Domain.Enums;

public class EnglishAnalysisResult
{
    public EnglishLevel DetectedEnglishLevel { get; set; }
    public bool HasAcceptableEnglish { get; set; }
    public int EnglishScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}
