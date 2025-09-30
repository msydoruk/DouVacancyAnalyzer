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

    public string Salary { get; set; } = string.Empty;

    public bool IsRemote { get; set; }

    public string Location { get; set; } = string.Empty;

    // Analysis results
    public VacancyCategory? VacancyCategory { get; set; }

    public ExperienceLevel? DetectedExperienceLevel { get; set; }

    public string? DetectedYearsOfExperience { get; set; }

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
        return new Vacancy
        {
            Title = Title,
            Company = Company,
            Description = Description,
            Url = Url,
            PublishedDate = PublishedDate,
            Salary = Salary,
            IsRemote = IsRemote,
            Location = Location
        };
    }

    public static VacancyEntity FromVacancy(Vacancy vacancy)
    {
        return new VacancyEntity
        {
            Title = vacancy.Title,
            Company = vacancy.Company,
            Description = vacancy.Description,
            Url = vacancy.Url,
            PublishedDate = vacancy.PublishedDate,
            Salary = vacancy.Salary,
            IsRemote = vacancy.IsRemote,
            Location = vacancy.Location,
            CreatedAt = DateTime.UtcNow,
            IsNew = true
        };
    }

}