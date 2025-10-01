using System;
using System.Collections.Generic;

namespace DouVacancyAnalyzer.Models.Temp;

public partial class Vacancy
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string Company { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string Url { get; set; } = null!;

    public DateTime PublishedDate { get; set; }

    public string Salary { get; set; } = null!;

    public int IsRemote { get; set; }

    public string Location { get; set; } = null!;

    public int? VacancyCategory { get; set; }

    public int? DetectedExperienceLevel { get; set; }

    public string? DetectedYearsOfExperience { get; set; }

    public int? DetectedEnglishLevel { get; set; }

    public int? IsModernStack { get; set; }

    public int? IsMiddleLevel { get; set; }

    public int? HasAcceptableEnglish { get; set; }

    public int? HasNoTimeTracker { get; set; }

    public int? IsBackendSuitable { get; set; }

    public string? AnalysisReason { get; set; }

    public int? MatchScore { get; set; }

    public string? DetectedTechnologies { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastAnalyzedAt { get; set; }

    public int IsNew { get; set; }

    public int IsActive { get; set; }
}
