using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BtwBoeker.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class VerwijderBegrotingEnMeerjarenplan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Begrotingsregels");

            migrationBuilder.DropTable(
                name: "Meerjarenbedragen");

            migrationBuilder.DropTable(
                name: "Begrotingen");

            migrationBuilder.DropTable(
                name: "Meerjarenposten");

            migrationBuilder.DropTable(
                name: "Meerjarenplannen");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Begrotingen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AangemaaktUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    VastgesteldOp = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Versie = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Begrotingen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Meerjarenplannen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AangemaaktUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AantalJaren = table.Column<int>(type: "INTEGER", nullable: false),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Startjaar = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    VastgesteldOp = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Versie = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meerjarenplannen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Begrotingsregels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: false),
                    Bedrag = table.Column<long>(type: "INTEGER", nullable: false),
                    BegrotingId = table.Column<int>(type: "INTEGER", nullable: false),
                    Toelichting = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Begrotingsregels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Begrotingsregels_Begrotingen_BegrotingId",
                        column: x => x.BegrotingId,
                        principalTable: "Begrotingen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Begrotingsregels_Grootboekrekeningen_RekeningId",
                        column: x => x.RekeningId,
                        principalTable: "Grootboekrekeningen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Meerjarenposten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    Toelichting = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meerjarenposten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meerjarenposten_Grootboekrekeningen_RekeningId",
                        column: x => x.RekeningId,
                        principalTable: "Grootboekrekeningen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Meerjarenposten_Meerjarenplannen_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Meerjarenplannen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Meerjarenbedragen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Bedrag = table.Column<long>(type: "INTEGER", nullable: false),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    PostId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meerjarenbedragen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meerjarenbedragen_Meerjarenposten_PostId",
                        column: x => x.PostId,
                        principalTable: "Meerjarenposten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Begrotingen_Administratie_Jaar_Versie",
                table: "Begrotingen",
                columns: new[] { "Administratie", "Jaar", "Versie" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Begrotingsregels_BegrotingId_RekeningId",
                table: "Begrotingsregels",
                columns: new[] { "BegrotingId", "RekeningId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Begrotingsregels_RekeningId",
                table: "Begrotingsregels",
                column: "RekeningId");

            migrationBuilder.CreateIndex(
                name: "IX_Meerjarenbedragen_PostId_Jaar",
                table: "Meerjarenbedragen",
                columns: new[] { "PostId", "Jaar" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meerjarenplannen_Administratie_Startjaar_Versie",
                table: "Meerjarenplannen",
                columns: new[] { "Administratie", "Startjaar", "Versie" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meerjarenposten_PlanId_RekeningId",
                table: "Meerjarenposten",
                columns: new[] { "PlanId", "RekeningId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meerjarenposten_RekeningId",
                table: "Meerjarenposten",
                column: "RekeningId");
        }
    }
}
