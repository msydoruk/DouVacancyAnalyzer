using System.ComponentModel.DataAnnotations;

namespace DouVacancyAnalyzer.Models;

public class VacancyResponse
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string VacancyUrl { get; set; } = string.Empty;

    [Required]
    public string VacancyTitle { get; set; } = string.Empty;

    [Required]
    public string CompanyName { get; set; } = string.Empty;

    public bool HasResponded { get; set; } = false;

    public DateTime? ResponseDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? Notes { get; set; }
}