using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verenigd.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class Jaarrekening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jaarrekeningindelingen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    Rapport = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jaarrekeningindelingen", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Indelingsregels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Volgorde = table.Column<int>(type: "INTEGER", nullable: false),
                    Afdeling = table.Column<int>(type: "INTEGER", nullable: false),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Tekst = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IndelingId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Indelingsregels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Indelingsregels_Jaarrekeningindelingen_IndelingId",
                        column: x => x.IndelingId,
                        principalTable: "Jaarrekeningindelingen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IndelingsregelRubrieken",
                columns: table => new
                {
                    RegelId = table.Column<int>(type: "INTEGER", nullable: false),
                    RubriekId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndelingsregelRubrieken", x => new { x.RegelId, x.RubriekId });
                    table.ForeignKey(
                        name: "FK_IndelingsregelRubrieken_Indelingsregels_RegelId",
                        column: x => x.RegelId,
                        principalTable: "Indelingsregels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IndelingsregelRubrieken_Rubrieken_RubriekId",
                        column: x => x.RubriekId,
                        principalTable: "Rubrieken",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndelingsregelRubrieken_RubriekId",
                table: "IndelingsregelRubrieken",
                column: "RubriekId");

            migrationBuilder.CreateIndex(
                name: "IX_Indelingsregels_IndelingId",
                table: "Indelingsregels",
                column: "IndelingId");

            migrationBuilder.CreateIndex(
                name: "IX_Jaarrekeningindelingen_Administratie_Rapport",
                table: "Jaarrekeningindelingen",
                columns: new[] { "Administratie", "Rapport" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IndelingsregelRubrieken");

            migrationBuilder.DropTable(
                name: "Indelingsregels");

            migrationBuilder.DropTable(
                name: "Jaarrekeningindelingen");
        }
    }
}
