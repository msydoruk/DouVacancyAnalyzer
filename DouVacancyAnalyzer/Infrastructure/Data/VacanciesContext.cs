using System;
using System.Collections.Generic;
using DouVacancyAnalyzer.Models.Temp;
using Microsoft.EntityFrameworkCore;

namespace DouVacancyAnalyzer.Infrastructure.Data;

public partial class VacanciesContext : DbContext
{
    public VacanciesContext()
    {
    }

    public VacanciesContext(DbContextOptions<VacanciesContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Vacancy> Vacancies { get; set; }

    public virtual DbSet<VacancyCountHistory> VacancyCountHistories { get; set; }

    public virtual DbSet<VacancyResponse> VacancyResponses { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlite("Data Source=vacancies.db");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vacancy>(entity =>
        {
            entity.HasIndex(e => new { e.CreatedAt, e.IsNew, e.IsActive }, "IX_Vacancies_CreatedAt_IsNew_IsActive");

            entity.HasIndex(e => e.IsActive, "IX_Vacancies_IsActive");

            entity.HasIndex(e => e.IsNew, "IX_Vacancies_IsNew");

            entity.HasIndex(e => e.Url, "IX_Vacancies_Url").IsUnique();
        });

        modelBuilder.Entity<VacancyCountHistory>(entity =>
        {
            entity.ToTable("VacancyCountHistory");

            entity.HasIndex(e => e.CheckDate, "IX_VacancyCountHistory_CheckDate");
        });

        modelBuilder.Entity<VacancyResponse>(entity =>
        {
            entity.HasIndex(e => e.HasResponded, "IX_VacancyResponses_HasResponded");

            entity.HasIndex(e => e.VacancyUrl, "IX_VacancyResponses_VacancyUrl").IsUnique();
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
