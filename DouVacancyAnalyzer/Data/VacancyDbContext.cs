using DouVacancyAnalyzer.Models;
using Microsoft.EntityFrameworkCore;

namespace DouVacancyAnalyzer.Data;

public class VacancyDbContext : DbContext
{
    public VacancyDbContext(DbContextOptions<VacancyDbContext> options) : base(options)
    {
    }

    public DbSet<VacancyEntity> Vacancies { get; set; }

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

            // Create index for content hash for fast duplicate detection
            entity.HasIndex(e => e.ContentHash).IsUnique();

            // Create index for new vacancy detection
            entity.HasIndex(e => e.IsNew);

            // Create index for search performance
            entity.HasIndex(e => new { e.CreatedAt, e.IsNew });
        });
    }
}