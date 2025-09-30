using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DouVacancyAnalyzer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveParsingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnglishLevel",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "Experience",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "Technologies",
                table: "Vacancies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnglishLevel",
                table: "Vacancies",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Experience",
                table: "Vacancies",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Technologies",
                table: "Vacancies",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
