namespace DouVacancyAnalyzer.Models;

public class OpenAiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public AnalysisPrompts Prompts { get; set; } = new();
}