using System.ComponentModel.DataAnnotations;

namespace DouVacancyAnalyzer.Models;

public class VacancyCountHistory
{
    [Key]
    public int Id { get; set; }

    public DateTime CheckDate { get; set; } = DateTime.UtcNow;

    public int TotalVacancies { get; set; }

    public int ActiveVacancies { get; set; }

    public int NewVacancies { get; set; }

    public int DeactivatedVacancies { get; set; }

    public int MatchingVacancies { get; set; }

    public decimal MatchPercentage { get; set; }

    public string? Notes { get; set; }
}