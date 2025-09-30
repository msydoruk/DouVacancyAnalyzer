namespace DouVacancyAnalyzer.Core.Application.DTOs;

public class ScrapingSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public int DelayBetweenRequests { get; set; } = 1000;
    public int MaxPages { get; set; } = 10;
    public int TestModeLimit { get; set; } = 5;
}