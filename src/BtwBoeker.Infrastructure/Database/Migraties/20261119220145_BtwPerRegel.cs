using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BtwBoeker.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class BtwPerRegel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BtwPercentage",
                table: "Journaalregels",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BtwSoort",
                table: "Journaalregels",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BtwPercentage",
                table: "Journaalregels");

            migrationBuilder.DropColumn(
                name: "BtwSoort",
                table: "Journaalregels");
        }
    }
}
