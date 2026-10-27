using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verenigd.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class Grootboek : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Boekjaren",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AfgeslotenUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boekjaren", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rubrieken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rubrieken", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Grootboekrekeningen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Nummer = table.Column<int>(type: "INTEGER", nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RubriekId = table.Column<int>(type: "INTEGER", nullable: true),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Rol = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActief = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grootboekrekeningen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Grootboekrekeningen_Rubrieken_RubriekId",
                        column: x => x.RubriekId,
                        principalTable: "Rubrieken",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Dagboeken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    Naam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dagboeken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dagboeken_Grootboekrekeningen_RekeningId",
                        column: x => x.RekeningId,
                        principalTable: "Grootboekrekeningen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Journaalposten",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    DagboekId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Jaar = table.Column<int>(type: "INTEGER", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Volgnummer = table.Column<int>(type: "INTEGER", nullable: true),
                    GeboektUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TegenboekingVan = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journaalposten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Journaalposten_Dagboeken_DagboekId",
                        column: x => x.DagboekId,
                        principalTable: "Dagboeken",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Journaalregels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RekeningId = table.Column<int>(type: "INTEGER", nullable: false),
                    Omschrijving = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Debet = table.Column<long>(type: "INTEGER", nullable: false),
                    Credit = table.Column<long>(type: "INTEGER", nullable: false),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journaalregels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Journaalregels_Grootboekrekeningen_RekeningId",
                        column: x => x.RekeningId,
                        principalTable: "Grootboekrekeningen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Journaalregels_Journaalposten_PostId",
                        column: x => x.PostId,
                        principalTable: "Journaalposten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Boekjaren_Administratie_Jaar",
                table: "Boekjaren",
                columns: new[] { "Administratie", "Jaar" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dagboeken_Administratie_Code",
                table: "Dagboeken",
                columns: new[] { "Administratie", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Dagboeken_RekeningId",
                table: "Dagboeken",
                column: "RekeningId");

            migrationBuilder.CreateIndex(
                name: "IX_Grootboekrekeningen_Administratie_Nummer",
                table: "Grootboekrekeningen",
                columns: new[] { "Administratie", "Nummer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grootboekrekeningen_RubriekId",
                table: "Grootboekrekeningen",
                column: "RubriekId");

            migrationBuilder.CreateIndex(
                name: "IX_Journaalposten_Administratie_Jaar",
                table: "Journaalposten",
                columns: new[] { "Administratie", "Jaar" });

            migrationBuilder.CreateIndex(
                name: "IX_Journaalposten_DagboekId_Jaar_Volgnummer",
                table: "Journaalposten",
                columns: new[] { "DagboekId", "Jaar", "Volgnummer" },
                unique: true,
                filter: "\"Volgnummer\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Journaalregels_PostId",
                table: "Journaalregels",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Journaalregels_RekeningId",
                table: "Journaalregels",
                column: "RekeningId");

            migrationBuilder.CreateIndex(
                name: "IX_Rubrieken_Administratie_Naam",
                table: "Rubrieken",
                columns: new[] { "Administratie", "Naam" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Boekjaren");

            migrationBuilder.DropTable(
                name: "Journaalregels");

            migrationBuilder.DropTable(
                name: "Journaalposten");

            migrationBuilder.DropTable(
                name: "Dagboeken");

            migrationBuilder.DropTable(
                name: "Grootboekrekeningen");

            migrationBuilder.DropTable(
                name: "Rubrieken");
        }
    }
}
