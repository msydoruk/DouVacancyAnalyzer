namespace DouVacancyAnalyzer.Core.Application.DTOs;

public class TechnologyAnalysisPrompts
{
    public string SystemPrompt { get; set; } = "You are an expert at analyzing technologies in IT job vacancies. Determine if the technology stack is modern.";
    public string UserPromptTemplate { get; set; } = "Analyze technologies:\n\nVacancy: {title}\nDescription: {description}\n\nDetermine:\n- IsModernStack (is it modern stack with .NET 6+, Core, latest frameworks)\n- DetectedTechnologies (list of technologies)\n- TechnologyScore (0-100)\n\nJSON: {{\"IsModernStack\": boolean, \"DetectedTechnologies\": [\"tech1\", \"tech2\"], \"TechnologyScore\": number, \"Reasoning\": \"explanation\"}}";
}
