using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verenigd.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class Contributie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "MachtigingIngetrokkenOp",
                table: "Betalers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MachtigingOndertekendOp",
                table: "Betalers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mandaatkenmerk",
                table: "Betalers",
                type: "TEXT",
                maxLength: 35,
                nullable: true,
                collation: "BINARY");

            migrationBuilder.CreateTable(
                name: "Contributiecategorieen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Speltak = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contributiecategorieen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Incassoinstellingen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    IncassantId = table.Column<string>(type: "TEXT", maxLength: 35, nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 70, nullable: false),
                    Iban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: false),
                    Incassodag = table.Column<int>(type: "INTEGER", nullable: false),
                    EersteIncassomaand = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Omschrijvingssjabloon = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incassoinstellingen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Incassoruns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Maand = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Uitvoerdatum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DefinitiefUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    GeannuleerdUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BestandHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incassoruns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LidCategorieen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Lidnummer = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CategorieId = table.Column<int>(type: "INTEGER", nullable: false),
                    Ingangsdatum = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LidCategorieen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LidCategorieen_Contributiecategorieen_CategorieId",
                        column: x => x.CategorieId,
                        principalTable: "Contributiecategorieen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tarieven",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategorieId = table.Column<int>(type: "INTEGER", nullable: false),
                    Boekjaar = table.Column<int>(type: "INTEGER", nullable: false),
                    Maandbedrag = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tarieven", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tarieven_Contributiecategorieen_CategorieId",
                        column: x => x.CategorieId,
                        principalTable: "Contributiecategorieen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Incassoposten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EndToEndId = table.Column<string>(type: "TEXT", maxLength: 35, nullable: false),
                    BetalerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Iban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: false),
                    Rekeninghouder = table.Column<string>(type: "TEXT", maxLength: 70, nullable: false),
                    Mandaatkenmerk = table.Column<string>(type: "TEXT", maxLength: 35, nullable: false),
                    OndertekendOp = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 140, nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incassoposten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incassoposten_Betalers_BetalerId",
                        column: x => x.BetalerId,
                        principalTable: "Betalers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incassoposten_Incassoruns_RunId",
                        column: x => x.RunId,
                        principalTable: "Incassoruns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Postregels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Lidnummer = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Maand = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Categorie = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Bedrag = table.Column<long>(type: "INTEGER", nullable: false),
                    IsGeannuleerd = table.Column<bool>(type: "INTEGER", nullable: false),
                    PostId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Postregels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Postregels_Incassoposten_PostId",
                        column: x => x.PostId,
                        principalTable: "Incassoposten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contributiecategorieen_Naam",
                table: "Contributiecategorieen",
                column: "Naam",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incassoposten_BetalerId",
                table: "Incassoposten",
                column: "BetalerId");

            migrationBuilder.CreateIndex(
                name: "IX_Incassoposten_EndToEndId",
                table: "Incassoposten",
                column: "EndToEndId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incassoposten_RunId",
                table: "Incassoposten",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_LidCategorieen_CategorieId",
                table: "LidCategorieen",
                column: "CategorieId");

            migrationBuilder.CreateIndex(
                name: "IX_LidCategorieen_Lidnummer_Ingangsdatum",
                table: "LidCategorieen",
                columns: new[] { "Lidnummer", "Ingangsdatum" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Postregels_Lidnummer_Maand",
                table: "Postregels",
                columns: new[] { "Lidnummer", "Maand" },
                unique: true,
                filter: "\"IsGeannuleerd\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Postregels_PostId",
                table: "Postregels",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Tarieven_CategorieId_Boekjaar",
                table: "Tarieven",
                columns: new[] { "CategorieId", "Boekjaar" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Incassoinstellingen");

            migrationBuilder.DropTable(
                name: "LidCategorieen");

            migrationBuilder.DropTable(
                name: "Postregels");

            migrationBuilder.DropTable(
                name: "Tarieven");

            migrationBuilder.DropTable(
                name: "Incassoposten");

            migrationBuilder.DropTable(
                name: "Contributiecategorieen");

            migrationBuilder.DropTable(
                name: "Incassoruns");

            migrationBuilder.DropColumn(
                name: "MachtigingIngetrokkenOp",
                table: "Betalers");

            migrationBuilder.DropColumn(
                name: "MachtigingOndertekendOp",
                table: "Betalers");

            migrationBuilder.DropColumn(
                name: "Mandaatkenmerk",
                table: "Betalers");
        }
    }
}
