using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Verenigd.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class Webapp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Webappinstellingen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Projectadres = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    PubliekeSleutel = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    BeheerderEmail = table.Column<string>(type: "TEXT", maxLength: 320, nullable: true),
                    Vernieuwingssleutel = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LaatsteSynchronisatieUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LaatsteGelukt = table.Column<bool>(type: "INTEGER", nullable: false),
                    LaatsteMelding = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Webappinstellingen", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Webappinstellingen");
        }
    }
}
