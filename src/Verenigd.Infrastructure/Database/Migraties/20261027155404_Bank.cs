using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verenigd.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class Bank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Iban",
                table: "Dagboeken",
                type: "TEXT",
                maxLength: 34,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Bankafschriften",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    DagboekId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nummer = table.Column<string>(type: "TEXT", maxLength: 35, nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Beginsaldo = table.Column<long>(type: "INTEGER", nullable: false),
                    Eindsaldo = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bankafschriften", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bankafschriften_Dagboeken_DagboekId",
                        column: x => x.DagboekId,
                        principalTable: "Dagboeken",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bankmutaties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    DagboekId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sleutel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "BINARY"),
                    Boekdatum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Bedrag = table.Column<long>(type: "INTEGER", nullable: false),
                    IsBij = table.Column<bool>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TegenIban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: true),
                    Tegennaam = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    EndToEndId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    MandaatId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    BatchAantal = table.Column<int>(type: "INTEGER", nullable: true),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bankmutaties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bankmutaties_Dagboeken_DagboekId",
                        column: x => x.DagboekId,
                        principalTable: "Dagboeken",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bankmutaties_Journaalposten_PostId",
                        column: x => x.PostId,
                        principalTable: "Journaalposten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Boekingsregels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TegenIban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: true),
                    NaamBevat = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    OmschrijvingBevat = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Richting = table.Column<int>(type: "INTEGER", nullable: false),
                    BedragVan = table.Column<long>(type: "INTEGER", nullable: true),
                    BedragTot = table.Column<long>(type: "INTEGER", nullable: true),
                    Transactiecode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsActief = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boekingsregels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Boekingsregels_Grootboekrekeningen_RekeningId",
                        column: x => x.RekeningId,
                        principalTable: "Grootboekrekeningen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dagboeken_Administratie_Iban",
                table: "Dagboeken",
                columns: new[] { "Administratie", "Iban" },
                unique: true,
                filter: "\"Iban\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bankafschriften_DagboekId_Datum_Nummer",
                table: "Bankafschriften",
                columns: new[] { "DagboekId", "Datum", "Nummer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bankmutaties_Administratie_Sleutel",
                table: "Bankmutaties",
                columns: new[] { "Administratie", "Sleutel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bankmutaties_DagboekId",
                table: "Bankmutaties",
                column: "DagboekId");

            migrationBuilder.CreateIndex(
                name: "IX_Bankmutaties_PostId",
                table: "Bankmutaties",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Boekingsregels_Administratie_Volgorde",
                table: "Boekingsregels",
                columns: new[] { "Administratie", "Volgorde" });

            migrationBuilder.CreateIndex(
                name: "IX_Boekingsregels_RekeningId",
                table: "Boekingsregels",
                column: "RekeningId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bankafschriften");

            migrationBuilder.DropTable(
                name: "Bankmutaties");

            migrationBuilder.DropTable(
                name: "Boekingsregels");

            migrationBuilder.DropIndex(
                name: "IX_Dagboeken_Administratie_Iban",
                table: "Dagboeken");

            migrationBuilder.DropColumn(
                name: "Iban",
                table: "Dagboeken");
        }
    }
}
