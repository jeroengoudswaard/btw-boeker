using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verenigd.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class Leden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Betalers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Iban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: false),
                    Rekeninghouder = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Betalers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Groepen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "BINARY"),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groepen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Personen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Lidnummer = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Voornaam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Tussenvoegsel = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Achternaam = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Geboortedatum = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Gender = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    Telefoon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ContactpersoonId = table.Column<string>(type: "TEXT", maxLength: 18, nullable: true, collation: "BINARY"),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Straat = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Huisnummer = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Toevoeging = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Postcode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Plaats = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Land = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UitgeschrevenOp = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Notities = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    LaatstGewijzigdUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BetalerLeden",
                columns: table => new
                {
                    Lidnummer = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    BetalerId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BetalerLeden", x => x.Lidnummer);
                    table.ForeignKey(
                        name: "FK_BetalerLeden_Betalers_BetalerId",
                        column: x => x.BetalerId,
                        principalTable: "Betalers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Functies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroepId = table.Column<int>(type: "INTEGER", nullable: false),
                    Rol = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Lidtype = table.Column<int>(type: "INTEGER", nullable: false),
                    Startdatum = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Einddatum = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PersoonId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Functies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Functies_Groepen_GroepId",
                        column: x => x.GroepId,
                        principalTable: "Groepen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Functies_Personen_PersoonId",
                        column: x => x.PersoonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Veldherkomst",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Veld = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Bron = table.Column<int>(type: "INTEGER", nullable: false),
                    GewijzigdUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PersoonId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Veldherkomst", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Veldherkomst_Personen_PersoonId",
                        column: x => x.PersoonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Voogden",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Volgnummer = table.Column<int>(type: "INTEGER", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    NaamOnbekend = table.Column<bool>(type: "INTEGER", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    Telefoon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Mobiel = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PersoonId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Voogden", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Voogden_Personen_PersoonId",
                        column: x => x.PersoonId,
                        principalTable: "Personen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BetalerLeden_BetalerId",
                table: "BetalerLeden",
                column: "BetalerId");

            migrationBuilder.CreateIndex(
                name: "IX_Betalers_Iban",
                table: "Betalers",
                column: "Iban",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Functies_GroepId",
                table: "Functies",
                column: "GroepId");

            migrationBuilder.CreateIndex(
                name: "IX_Functies_PersoonId",
                table: "Functies",
                column: "PersoonId");

            migrationBuilder.CreateIndex(
                name: "IX_Groepen_Naam",
                table: "Groepen",
                column: "Naam",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Personen_Lidnummer",
                table: "Personen",
                column: "Lidnummer",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Veldherkomst_PersoonId_Veld",
                table: "Veldherkomst",
                columns: new[] { "PersoonId", "Veld" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Voogden_PersoonId_Volgnummer",
                table: "Voogden",
                columns: new[] { "PersoonId", "Volgnummer" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BetalerLeden");

            migrationBuilder.DropTable(
                name: "Functies");

            migrationBuilder.DropTable(
                name: "Veldherkomst");

            migrationBuilder.DropTable(
                name: "Voogden");

            migrationBuilder.DropTable(
                name: "Betalers");

            migrationBuilder.DropTable(
                name: "Groepen");

            migrationBuilder.DropTable(
                name: "Personen");
        }
    }
}
