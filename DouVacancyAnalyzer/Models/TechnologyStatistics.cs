namespace DouVacancyAnalyzer.Models;

public class TechnologyStatistics
{
    public int Total { get; set; }
    public int WithModernTech { get; set; }
    public int WithOutdatedTech { get; set; }
    public int WithDesktopApps { get; set; }
    public int WithFrontend { get; set; }
    public int WithTimeTracker { get; set; }

    public Dictionary<string, int> ModernTechCount { get; set; } = new();
    public Dictionary<string, int> OutdatedTechCount { get; set; } = new();
    public Dictionary<string, int> DesktopKeywordCount { get; set; } = new();
    public Dictionary<string, int> FrontendKeywordCount { get; set; } = new();

    public int JuniorLevel { get; set; }
    public int MiddleLevel { get; set; }
    public int SeniorLevel { get; set; }
    public int UnspecifiedLevel { get; set; }

    public Dictionary<string, int> YearsRequirements { get; set; } = new();

    public Dictionary<string, int> VacancyCategories { get; set; } = new();

    public List<VacancyMatch> ModernVacancies { get; set; } = new();
}