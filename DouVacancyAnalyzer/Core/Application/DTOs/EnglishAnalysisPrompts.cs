namespace DouVacancyAnalyzer.Core.Application.DTOs;

public class EnglishAnalysisPrompts
{
    public string SystemPrompt { get; set; } = "You are an expert at analyzing English language requirements in IT job vacancies. Evaluate if B1 level is acceptable.";
    public string UserPromptTemplate { get; set; } = "Analyze English requirements:\n\nVacancy: {title}\nEnglish: {englishLevel}\nDescription: {description}\n\nDetermine:\n- DetectedEnglishLevel: use EXACTLY one of these values: Beginner, Elementary, PreIntermediate, Intermediate, UpperIntermediate, Advanced, Proficient, Unspecified\n- HasAcceptableEnglish (is B1 level acceptable)\n\nJSON: {{\"DetectedEnglishLevel\": \"EXACT_VALUE\", \"HasAcceptableEnglish\": boolean, \"EnglishScore\": number_0_100, \"Reasoning\": \"explanation\"}}";
}
