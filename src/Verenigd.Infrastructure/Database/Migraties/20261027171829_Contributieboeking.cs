using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verenigd.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class Contributieboeking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Postregels_Lidnummer_Maand",
                table: "Postregels");

            migrationBuilder.AddColumn<bool>(
                name: "IsHerincasso",
                table: "Postregels",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HerincassoVan",
                table: "Incassoposten",
                type: "TEXT",
                maxLength: 35,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpbrengstrekeningId",
                table: "Contributiecategorieen",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchKenmerk",
                table: "Bankmutaties",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Retourreden",
                table: "Bankmutaties",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Debiteurmutaties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Vordering = table.Column<string>(type: "TEXT", maxLength: 35, nullable: false, collation: "BINARY"),
                    BetalerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IncassorunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Saldo = table.Column<long>(type: "INTEGER", nullable: false),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Reden = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Debiteurmutaties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Debiteurmutaties_Journaalposten_PostId",
                        column: x => x.PostId,
                        principalTable: "Journaalposten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Herincassos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Vordering = table.Column<string>(type: "TEXT", maxLength: 35, nullable: false, collation: "BINARY"),
                    AangevraagdUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Herincassos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Postregels_Lidnummer_Maand",
                table: "Postregels",
                columns: new[] { "Lidnummer", "Maand" },
                unique: true,
                filter: "\"IsGeannuleerd\" = 0 AND \"IsHerincasso\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Debiteurmutaties_Administratie_Vordering",
                table: "Debiteurmutaties",
                columns: new[] { "Administratie", "Vordering" });

            migrationBuilder.CreateIndex(
                name: "IX_Debiteurmutaties_IncassorunId",
                table: "Debiteurmutaties",
                column: "IncassorunId");

            migrationBuilder.CreateIndex(
                name: "IX_Debiteurmutaties_PostId",
                table: "Debiteurmutaties",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Herincassos_Vordering",
                table: "Herincassos",
                column: "Vordering");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Debiteurmutaties");

            migrationBuilder.DropTable(
                name: "Herincassos");

            migrationBuilder.DropIndex(
                name: "IX_Postregels_Lidnummer_Maand",
                table: "Postregels");

            migrationBuilder.DropColumn(
                name: "IsHerincasso",
                table: "Postregels");

            migrationBuilder.DropColumn(
                name: "HerincassoVan",
                table: "Incassoposten");

            migrationBuilder.DropColumn(
                name: "OpbrengstrekeningId",
                table: "Contributiecategorieen");

            migrationBuilder.DropColumn(
                name: "BatchKenmerk",
                table: "Bankmutaties");

            migrationBuilder.DropColumn(
                name: "Retourreden",
                table: "Bankmutaties");

            migrationBuilder.CreateIndex(
                name: "IX_Postregels_Lidnummer_Maand",
                table: "Postregels",
                columns: new[] { "Lidnummer", "Maand" },
                unique: true,
                filter: "\"IsGeannuleerd\" = 0");
        }
    }
}
