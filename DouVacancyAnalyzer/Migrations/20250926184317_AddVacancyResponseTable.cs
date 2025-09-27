using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DouVacancyAnalyzer.Migrations
{
    /// <inheritdoc />
    public partial class AddVacancyResponseTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VacancyResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VacancyUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    VacancyTitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    HasResponded = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResponseDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VacancyResponses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VacancyResponses_HasResponded",
                table: "VacancyResponses",
                column: "HasResponded");

            migrationBuilder.CreateIndex(
                name: "IX_VacancyResponses_VacancyUrl",
                table: "VacancyResponses",
                column: "VacancyUrl",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VacancyResponses");
        }
    }
}
