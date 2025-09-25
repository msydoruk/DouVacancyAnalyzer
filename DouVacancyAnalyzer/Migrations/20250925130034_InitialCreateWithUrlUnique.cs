using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DouVacancyAnalyzer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateWithUrlUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vacancies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Company = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    PublishedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Experience = table.Column<string>(type: "TEXT", nullable: false),
                    Salary = table.Column<string>(type: "TEXT", nullable: false),
                    IsRemote = table.Column<bool>(type: "INTEGER", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    Technologies = table.Column<string>(type: "TEXT", nullable: false),
                    EnglishLevel = table.Column<string>(type: "TEXT", nullable: false),
                    VacancyCategory = table.Column<int>(type: "INTEGER", nullable: true),
                    DetectedExperienceLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    DetectedEnglishLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    IsModernStack = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsMiddleLevel = table.Column<bool>(type: "INTEGER", nullable: true),
                    HasAcceptableEnglish = table.Column<bool>(type: "INTEGER", nullable: true),
                    HasNoTimeTracker = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsBackendSuitable = table.Column<bool>(type: "INTEGER", nullable: true),
                    AnalysisReason = table.Column<string>(type: "TEXT", nullable: true),
                    MatchScore = table.Column<int>(type: "INTEGER", nullable: true),
                    DetectedTechnologies = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastAnalyzedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsNew = table.Column<bool>(type: "INTEGER", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vacancies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_CreatedAt_IsNew",
                table: "Vacancies",
                columns: new[] { "CreatedAt", "IsNew" });

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_IsNew",
                table: "Vacancies",
                column: "IsNew");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_Url",
                table: "Vacancies",
                column: "Url",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Vacancies");
        }
    }
}
