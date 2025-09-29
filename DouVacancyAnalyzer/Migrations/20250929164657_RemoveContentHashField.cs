using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DouVacancyAnalyzer.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContentHashField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "Vacancies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "Vacancies",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }
    }
}
