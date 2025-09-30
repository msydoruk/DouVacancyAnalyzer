namespace DouVacancyAnalyzer.Core.Application.DTOs;

public class CategoryAnalysisPrompts
{
    public string SystemPrompt { get; set; } = "You are an expert at categorizing IT job vacancies. Determine the job category based on the description and requirements.";
    public string UserPromptTemplate { get; set; } = "Categorize the vacancy:\n\nTitle: {title}\nDescription: {description}\n\nDetermine the category (Backend/Frontend/Fullstack/Desktop/DevOps/QA/Mobile/GameDev/DataScience/Security/Other) and return JSON: {{\"VacancyCategory\": \"category\", \"Confidence\": number_0_100, \"Reasoning\": \"explanation\"}}";
}
