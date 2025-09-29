using System.ComponentModel.DataAnnotations;

namespace DouVacancyAnalyzer.Models;

public class VacancyEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Company { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string Url { get; set; } = string.Empty;

    public DateTime PublishedDate { get; set; }

    public string Experience { get; set; } = string.Empty;

    public string Salary { get; set; } = string.Empty;

    public bool IsRemote { get; set; }

    public string Location { get; set; } = string.Empty;

    public string Technologies { get; set; } = string.Empty; // JSON array stored as string

    public string EnglishLevel { get; set; } = string.Empty;

    // Analysis results
    public VacancyCategory? VacancyCategory { get; set; }

    public ExperienceLevel? DetectedExperienceLevel { get; set; }

    public EnglishLevel? DetectedEnglishLevel { get; set; }

    public bool? IsModernStack { get; set; }

    public bool? IsMiddleLevel { get; set; }

    public bool? HasAcceptableEnglish { get; set; }

    public bool? HasNoTimeTracker { get; set; }

    public bool? IsBackendSuitable { get; set; }

    public string? AnalysisReason { get; set; }

    public int? MatchScore { get; set; }

    public string? DetectedTechnologies { get; set; } // JSON array stored as string

    // Tracking fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastAnalyzedAt { get; set; }

    public bool IsNew { get; set; } = true;

    public bool IsActive { get; set; } = true;


    public Vacancy ToVacancy()
    {
        var technologies = new List<string>();
        if (!string.IsNullOrEmpty(Technologies))
        {
            try
            {
                technologies = System.Text.Json.JsonSerializer.Deserialize<List<string>>(Technologies) ?? new List<string>();
            }
            catch
            {
                // Ignore JSON parsing errors
            }
        }

        return new Vacancy
        {
            Title = Title,
            Company = Company,
            Description = Description,
            Url = Url,
            PublishedDate = PublishedDate,
            Experience = Experience,
            Salary = Salary,
            IsRemote = IsRemote,
            Location = Location,
            Technologies = technologies,
            EnglishLevel = EnglishLevel
        };
    }

    public static VacancyEntity FromVacancy(Vacancy vacancy)
    {
        var technologies = System.Text.Json.JsonSerializer.Serialize(vacancy.Technologies ?? new List<string>());
        return new VacancyEntity
        {
            Title = vacancy.Title,
            Company = vacancy.Company,
            Description = vacancy.Description,
            Url = vacancy.Url,
            PublishedDate = vacancy.PublishedDate,
            Experience = vacancy.Experience,
            Salary = vacancy.Salary,
            IsRemote = vacancy.IsRemote,
            Location = vacancy.Location,
            Technologies = technologies,
            EnglishLevel = vacancy.EnglishLevel,
            CreatedAt = DateTime.UtcNow,
            IsNew = true
        };
    }

}