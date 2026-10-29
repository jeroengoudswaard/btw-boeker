using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verenigd.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class Begroting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kant",
                table: "Grootboekrekeningen",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Besluit 152: baten of lasten voorstellen uit het saldo (zie MigratieSql.KantVoorstellen).
            migrationBuilder.Sql(MigratieSql.KantVoorstellen);

            migrationBuilder.CreateTable(
                name: "Begrotingen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    Versie = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    VastgesteldOp = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    AangemaaktUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Begrotingen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Begrotingsregels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: false),
                    Bedrag = table.Column<long>(type: "INTEGER", nullable: false),
                    Toelichting = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    BegrotingId = table.Column<int>(type: "INTEGER", nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Begrotingsregels");

            migrationBuilder.DropTable(
                name: "Begrotingen");

            migrationBuilder.DropColumn(
                name: "Kant",
                table: "Grootboekrekeningen");
        }
    }
}
