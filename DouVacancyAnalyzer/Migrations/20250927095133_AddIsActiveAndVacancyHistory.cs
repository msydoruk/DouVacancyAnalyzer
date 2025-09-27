using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DouVacancyAnalyzer.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActiveAndVacancyHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vacancies_CreatedAt_IsNew",
                table: "Vacancies");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Vacancies",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "VacancyCountHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CheckDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalVacancies = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveVacancies = table.Column<int>(type: "INTEGER", nullable: false),
                    NewVacancies = table.Column<int>(type: "INTEGER", nullable: false),
                    DeactivatedVacancies = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchingVacancies = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchPercentage = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VacancyCountHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_CreatedAt_IsNew_IsActive",
                table: "Vacancies",
                columns: new[] { "CreatedAt", "IsNew", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_IsActive",
                table: "Vacancies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_VacancyCountHistory_CheckDate",
                table: "VacancyCountHistory",
                column: "CheckDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VacancyCountHistory");

            migrationBuilder.DropIndex(
                name: "IX_Vacancies_CreatedAt_IsNew_IsActive",
                table: "Vacancies");

            migrationBuilder.DropIndex(
                name: "IX_Vacancies_IsActive",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Vacancies");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_CreatedAt_IsNew",
                table: "Vacancies",
                columns: new[] { "CreatedAt", "IsNew" });
        }
    }
}
