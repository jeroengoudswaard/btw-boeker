using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Verenigd.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class Initieel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Administraties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Accentkleur = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Administraties", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Administraties",
                columns: new[] { "Id", "Accentkleur", "Naam", "Soort" },
                values: new object[,]
                {
                    { 1, "#2E7D32", "Vereniging", 1 },
                    { 2, "#1565C0", "Stichting", 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Administraties_Soort",
                table: "Administraties",
                column: "Soort",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Administraties");
        }
    }
}
