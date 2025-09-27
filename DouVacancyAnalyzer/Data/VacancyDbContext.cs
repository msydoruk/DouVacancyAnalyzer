using DouVacancyAnalyzer.Models;
using Microsoft.EntityFrameworkCore;

namespace DouVacancyAnalyzer.Data;

public class VacancyDbContext : DbContext
{
    public VacancyDbContext(DbContextOptions<VacancyDbContext> options) : base(options)
    {
    }

    public DbSet<VacancyEntity> Vacancies { get; set; }
    public DbSet<VacancyResponse> VacancyResponses { get; set; }
    public DbSet<VacancyCountHistory> VacancyCountHistory { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VacancyEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Company).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ContentHash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Description).HasColumnType("TEXT");
            entity.Property(e => e.Technologies).HasColumnType("TEXT");
            entity.Property(e => e.DetectedTechnologies).HasColumnType("TEXT");
            entity.Property(e => e.AnalysisReason).HasColumnType("TEXT");

            // Create unique index for URL for fast duplicate detection
            entity.HasIndex(e => e.Url).IsUnique();

            // Create index for new vacancy detection
            entity.HasIndex(e => e.IsNew);

            // Create index for active vacancy filtering
            entity.HasIndex(e => e.IsActive);

            // Create index for search performance
            entity.HasIndex(e => new { e.CreatedAt, e.IsNew, e.IsActive });
        });

        modelBuilder.Entity<VacancyResponse>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VacancyUrl).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.VacancyTitle).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CompanyName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            // Create unique index for VacancyUrl to prevent duplicates
            entity.HasIndex(e => e.VacancyUrl).IsUnique();

            // Create index for fast lookup
            entity.HasIndex(e => e.HasResponded);
        });

        modelBuilder.Entity<VacancyCountHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MatchPercentage).HasPrecision(5, 2);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            // Create index for date ordering
            entity.HasIndex(e => e.CheckDate);
        });
    }
}