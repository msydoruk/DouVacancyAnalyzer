namespace DouVacancyAnalyzer.Core.Application.DTOs;

public class SuitabilityAnalysisPrompts
{
    public string SystemPrompt { get; set; } = "You are an expert at evaluating job vacancy suitability for Middle .NET Backend developer with 3+ years of experience. IMPORTANT: candidate is NOT considering Senior/Lead positions and is NOT a Fullstack developer.";
    public string UserPromptTemplate { get; set; } = "Evaluate suitability for Middle .NET Backend developer:\n\nVacancy: {title}\nCompany: {company}\nDescription: {description}\nLocation: {location}\n\nDetermine:\n- IsBackendSuitable: true only if this is pure Backend position or Fullstack with strong Backend focus (NOT Frontend-focused)\n- HasNoTimeTracker: false if there are mentions of time tracking, work hours tracking\n- MatchScore: 0-100 (reduce for Senior/Lead requirements, Fullstack without Backend focus, Frontend tasks)\n\nJSON: {{\"IsBackendSuitable\": boolean, \"HasNoTimeTracker\": boolean, \"MatchScore\": number_0_100, \"AnalysisReason\": \"detailed explanation with justification of scores\"}}";
}
