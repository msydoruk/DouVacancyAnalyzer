namespace DouVacancyAnalyzer.Models;

public class AnalysisPrompts
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPromptTemplate { get; set; } = string.Empty;
    public CategoryAnalysisPrompts CategoryAnalysis { get; set; } = new();
    public TechnologyAnalysisPrompts TechnologyAnalysis { get; set; } = new();
    public ExperienceAnalysisPrompts ExperienceAnalysis { get; set; } = new();
    public EnglishAnalysisPrompts EnglishAnalysis { get; set; } = new();
    public SuitabilityAnalysisPrompts SuitabilityAnalysis { get; set; } = new();
}

public class CategoryAnalysisPrompts
{
    public string SystemPrompt { get; set; } = "Ви - експерт з категоризації IT вакансій. Визначте категорію вакансії на основі опису та вимог.";
    public string UserPromptTemplate { get; set; } = "Категоризуйте вакансію:\n\nНазва: {title}\nОпис: {description}\n\nВизначте категорію (Backend/Frontend/Fullstack/Desktop/DevOps/QA/Mobile/GameDev/DataScience/Security/Other) та поверніть JSON: {{\"VacancyCategory\": \"категорія\", \"Confidence\": число_0_100, \"Reasoning\": \"пояснення\"}}";
}

public class TechnologyAnalysisPrompts
{
    public string SystemPrompt { get; set; } = "Ви - експерт з аналізу технологій в IT вакансіях. Визначте сучасність технологічного стеку.";
    public string UserPromptTemplate { get; set; } = "Проаналізуйте технології:\n\nВакансія: {title}\nОпис: {description}\n\nВизначте:\n- IsModernStack (чи сучасний стек .NET 6+, Core, новітні фреймворки)\n- DetectedTechnologies (список технологій)\n- TechnologyScore (0-100)\n\nJSON: {{\"IsModernStack\": boolean, \"DetectedTechnologies\": [\"tech1\", \"tech2\"], \"TechnologyScore\": число, \"Reasoning\": \"пояснення\"}}";
}

public class ExperienceAnalysisPrompts
{
    public string SystemPrompt { get; set; } = "Ви - експерт з аналізу вимог до досвіду в IT вакансіях. Визначте рівень досвіду та відповідність Middle рівню.";
    public string UserPromptTemplate { get; set; } = "Проаналізуйте вимоги до досвіду:\n\nВакансія: {title}\nДосвід: {experience}\nОпис: {description}\n\nВизначте:\n- DetectedExperienceLevel (Junior/Middle/Senior/Lead/Unspecified)\n- IsMiddleLevel (чи підходить для Middle розробника з 3+ роками)\n\nJSON: {{\"DetectedExperienceLevel\": \"рівень\", \"IsMiddleLevel\": boolean, \"ExperienceScore\": число_0_100, \"Reasoning\": \"пояснення\"}}";
}

public class EnglishAnalysisPrompts
{
    public string SystemPrompt { get; set; } = "Ви - експерт з аналізу вимог до англійської мови в IT вакансіях. Оцініть відповідність рівню B1.";
    public string UserPromptTemplate { get; set; } = "Проаналізуйте вимоги до англійської:\n\nВакансія: {title}\nАнглійська: {englishLevel}\nОпис: {description}\n\nВизначте:\n- DetectedEnglishLevel (Beginner/Elementary/PreIntermediate/Intermediate/UpperIntermediate/Advanced/Proficient/Unspecified)\n- HasAcceptableEnglish (чи підходить B1 рівень)\n\nJSON: {{\"DetectedEnglishLevel\": \"рівень\", \"HasAcceptableEnglish\": boolean, \"EnglishScore\": число_0_100, \"Reasoning\": \"пояснення\"}}";
}

public class SuitabilityAnalysisPrompts
{
    public string SystemPrompt { get; set; } = "Ви - експерт з оцінки відповідності вакансій профілю .NET Backend розробника. Оцініть загальну придатність.";
    public string UserPromptTemplate { get; set; } = "Оцініть придатність для .NET Backend розробника:\n\nВакансія: {title}\nКомпанія: {company}\nОпис: {description}\nМісце: {location}\n\nВизначте:\n- IsBackendSuitable (чи підходить для backend розробки)\n- HasNoTimeTracker (чи немає вимог щодо time tracking)\n- MatchScore (загальна оцінка 0-100)\n\nJSON: {{\"IsBackendSuitable\": boolean, \"HasNoTimeTracker\": boolean, \"MatchScore\": число_0_100, \"AnalysisReason\": \"детальне пояснення\"}}";
}