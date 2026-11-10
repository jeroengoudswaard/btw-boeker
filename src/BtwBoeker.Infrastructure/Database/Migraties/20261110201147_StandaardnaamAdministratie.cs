using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BtwBoeker.Infrastructure.Database.Migraties
{
    /// <summary>
    /// Keuze A (2026-11-10): de standaardnaam van de administratie is "Mijn administratie". Alleen de oude standaardnaam
    /// wordt vervangen; een zelf gekozen naam blijft staan.
    /// </summary>
    public partial class StandaardnaamAdministratie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Administraties\" SET \"Naam\" = 'Mijn administratie' WHERE \"Id\" = 1 AND \"Naam\" = 'Vereniging';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Administraties\" SET \"Naam\" = 'Vereniging' WHERE \"Id\" = 1 AND \"Naam\" = 'Mijn administratie';");
        }
    }
}
