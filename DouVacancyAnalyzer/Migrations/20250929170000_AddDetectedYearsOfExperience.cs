using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DouVacancyAnalyzer.Migrations
{
    /// <inheritdoc />
    public partial class AddDetectedYearsOfExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetectedYearsOfExperience",
                table: "Vacancies",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetectedYearsOfExperience",
                table: "Vacancies");
        }
    }
}