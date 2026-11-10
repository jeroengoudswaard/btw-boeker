using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BtwBoeker.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class VerwijderExcelHistorie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorischeRegels");

            migrationBuilder.DropTable(
                name: "HistorischeTotalen");

            // Een historisch boekjaar (uit de vervallen Excel-overname) heeft geen journaalposten; het verdwijnt met de historie.
            migrationBuilder.Sql("DELETE FROM Boekjaren WHERE IsHistorisch = 1;");

            migrationBuilder.DropColumn(
                name: "IsHistorisch",
                table: "Boekjaren");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHistorisch",
                table: "Boekjaren",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "HistorischeRegels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: false),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Credit = table.Column<long>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Debet = table.Column<long>(type: "INTEGER", nullable: false),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorischeRegels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorischeRegels_Grootboekrekeningen_RekeningId",
                        column: x => x.RekeningId,
                        principalTable: "Grootboekrekeningen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistorischeTotalen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: false),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Credit = table.Column<long>(type: "INTEGER", nullable: false),
                    Debet = table.Column<long>(type: "INTEGER", nullable: false),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorischeTotalen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorischeTotalen_Grootboekrekeningen_RekeningId",
                        column: x => x.RekeningId,
                        principalTable: "Grootboekrekeningen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistorischeRegels_Administratie_Jaar",
                table: "HistorischeRegels",
                columns: new[] { "Administratie", "Jaar" });

            migrationBuilder.CreateIndex(
                name: "IX_HistorischeRegels_RekeningId",
                table: "HistorischeRegels",
                column: "RekeningId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorischeTotalen_Administratie_Jaar_RekeningId",
                table: "HistorischeTotalen",
                columns: new[] { "Administratie", "Jaar", "RekeningId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistorischeTotalen_RekeningId",
                table: "HistorischeTotalen",
                column: "RekeningId");
        }
    }
}
