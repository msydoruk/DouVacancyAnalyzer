using System;
using System.Collections.Generic;

namespace DouVacancyAnalyzer.Models.Temp;

public partial class VacancyCountHistory
{
    public int Id { get; set; }

    public DateTime CheckDate { get; set; }

    public int TotalVacancies { get; set; }

    public int ActiveVacancies { get; set; }

    public int NewVacancies { get; set; }

    public int DeactivatedVacancies { get; set; }

    public int MatchingVacancies { get; set; }

    public decimal MatchPercentage { get; set; }

    public string? Notes { get; set; }
}
