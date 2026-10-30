using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verenigd.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class Email : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Emailinstellingen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Afzendernaam = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Afzenderadres = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    ClientGeheim = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Vernieuwingssleutel = table.Column<string>(type: "TEXT", nullable: true),
                    GekoppeldOpUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emailinstellingen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Emailsjablonen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Onderwerp = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Tekst = table.Column<string>(type: "TEXT", nullable: false),
                    IsStandaard = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emailsjablonen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VerzondenBerichten",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TijdstipUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Adres = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    Onderwerp = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Sjabloon = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Fout = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerzondenBerichten", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VerzondenBerichten_TijdstipUtc",
                table: "VerzondenBerichten",
                column: "TijdstipUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Emailinstellingen");

            migrationBuilder.DropTable(
                name: "Emailsjablonen");

            migrationBuilder.DropTable(
                name: "VerzondenBerichten");
        }
    }
}
