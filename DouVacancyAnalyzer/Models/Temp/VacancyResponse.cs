using System;
using System.Collections.Generic;

namespace DouVacancyAnalyzer.Models.Temp;

public partial class VacancyResponse
{
    public int Id { get; set; }

    public string VacancyUrl { get; set; } = null!;

    public string VacancyTitle { get; set; } = null!;

    public string CompanyName { get; set; } = null!;

    public int HasResponded { get; set; }

    public string? ResponseDate { get; set; }

    public string CreatedAt { get; set; } = null!;

    public string UpdatedAt { get; set; } = null!;

    public string? Notes { get; set; }
}
