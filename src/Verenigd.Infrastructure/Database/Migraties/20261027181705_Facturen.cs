using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verenigd.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class Facturen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Factuurinstellingen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Straat = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Postcode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Plaats = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Kvk = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Iban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    Telefoon = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Termijn = table.Column<int>(type: "INTEGER", nullable: false),
                    Voettekst = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Logo = table.Column<byte[]>(type: "BLOB", nullable: true),
                    LogoType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factuurinstellingen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Relaties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Contactpersoon = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Straat = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Postcode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Plaats = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    Telefoon = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Iban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: true),
                    IsDebiteur = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCrediteur = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notitie = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsActief = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Relaties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Verkoopfacturen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RelatieId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Factuurdatum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Termijn = table.Column<int>(type: "INTEGER", nullable: false),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: true),
                    Volgnummer = table.Column<int>(type: "INTEGER", nullable: true),
                    Nummer = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Ontvanger = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreditVan = table.Column<Guid>(type: "TEXT", nullable: true),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Verkoopfacturen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Verkoopfacturen_Journaalposten_PostId",
                        column: x => x.PostId,
                        principalTable: "Journaalposten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Verkoopfacturen_Relaties_RelatieId",
                        column: x => x.RelatieId,
                        principalTable: "Relaties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Verkoopfacturen_Verkoopfacturen_CreditVan",
                        column: x => x.CreditVan,
                        principalTable: "Verkoopfacturen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Factuurregels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Aantal = table.Column<decimal>(type: "TEXT", nullable: false),
                    Prijs = table.Column<long>(type: "INTEGER", nullable: false),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: false),
                    FactuurId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factuurregels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Factuurregels_Grootboekrekeningen_RekeningId",
                        column: x => x.RekeningId,
                        principalTable: "Grootboekrekeningen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Factuurregels_Verkoopfacturen_FactuurId",
                        column: x => x.FactuurId,
                        principalTable: "Verkoopfacturen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Factuurinstellingen_Administratie",
                table: "Factuurinstellingen",
                column: "Administratie",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Factuurregels_FactuurId",
                table: "Factuurregels",
                column: "FactuurId");

            migrationBuilder.CreateIndex(
                name: "IX_Factuurregels_RekeningId",
                table: "Factuurregels",
                column: "RekeningId");

            migrationBuilder.CreateIndex(
                name: "IX_Relaties_Naam",
                table: "Relaties",
                column: "Naam");

            migrationBuilder.CreateIndex(
                name: "IX_Verkoopfacturen_Administratie_Jaar_Volgnummer",
                table: "Verkoopfacturen",
                columns: new[] { "Administratie", "Jaar", "Volgnummer" },
                unique: true,
                filter: "\"Volgnummer\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Verkoopfacturen_CreditVan",
                table: "Verkoopfacturen",
                column: "CreditVan");

            migrationBuilder.CreateIndex(
                name: "IX_Verkoopfacturen_PostId",
                table: "Verkoopfacturen",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Verkoopfacturen_RelatieId",
                table: "Verkoopfacturen",
                column: "RelatieId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Factuurinstellingen");

            migrationBuilder.DropTable(
                name: "Factuurregels");

            migrationBuilder.DropTable(
                name: "Verkoopfacturen");

            migrationBuilder.DropTable(
                name: "Relaties");
        }
    }
}
