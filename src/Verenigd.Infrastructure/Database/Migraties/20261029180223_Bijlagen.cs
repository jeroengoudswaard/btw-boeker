using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verenigd.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class Bijlagen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bijlagen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Administratie = table.Column<int>(type: "INTEGER", nullable: false),
                    PostId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Bestandsnaam = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Soort = table.Column<int>(type: "INTEGER", nullable: false),
                    Grootte = table.Column<int>(type: "INTEGER", nullable: false),
                    Controlecode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Bedrag = table.Column<long>(type: "INTEGER", nullable: true),
                    ToegevoegdUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bijlagen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bijlagen_Journaalposten_PostId",
                        column: x => x.PostId,
                        principalTable: "Journaalposten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BijlageInhoud",
                columns: table => new
                {
                    BijlageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Gegevens = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BijlageInhoud", x => x.BijlageId);
                    table.ForeignKey(
                        name: "FK_BijlageInhoud_Bijlagen_BijlageId",
                        column: x => x.BijlageId,
                        principalTable: "Bijlagen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bijlagen_Administratie",
                table: "Bijlagen",
                column: "Administratie");

            migrationBuilder.CreateIndex(
                name: "IX_Bijlagen_Controlecode",
                table: "Bijlagen",
                column: "Controlecode");

            migrationBuilder.CreateIndex(
                name: "IX_Bijlagen_PostId",
                table: "Bijlagen",
                column: "PostId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BijlageInhoud");

            migrationBuilder.DropTable(
                name: "Bijlagen");
        }
    }
}
