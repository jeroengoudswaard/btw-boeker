using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BtwBoeker.Infrastructure.Database.Migraties
{
    /// <inheritdoc />
    public partial class BtwOpBoekingsregel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BtwPercentage",
                table: "Boekingsregels",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BtwPercentage",
                table: "Boekingsregels");
        }
    }
}
