using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BtwBoeker.Infrastructure.Database.Migraties
{
    /// <summary>
    /// Besluit 26: de app toont één administratie. De tweede administratie (soort 2) verdwijnt alleen als er niets in staat;
    /// met gegevens blijft de rij bestaan en wordt hij alleen niet meer getoond, zodat er nooit gegevens verloren gaan.
    /// Een startwizard die op de vervallen stap "Administraties" stond, gaat verder bij "Boekhouding".
    /// </summary>
    public partial class EenAdministratie : Migration
    {
        private static readonly string[] TabellenPerAdministratie =
        [
            "Rubrieken", "Grootboekrekeningen", "Boekjaren", "Dagboeken", "Journaalposten", "Boekingsregels",
            "Bankafschriften", "Bankmutaties", "Bijlagen", "Jaarrekeningindelingen",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var leeg = string.Join(" AND ", TabellenPerAdministratie.Select(t => $"NOT EXISTS (SELECT 1 FROM \"{t}\" WHERE \"Administratie\" = 2)"));
            migrationBuilder.Sql($"DELETE FROM \"Administraties\" WHERE \"Id\" = 2 AND \"Soort\" = 2 AND {leeg};");
            migrationBuilder.Sql("UPDATE \"Startwizard\" SET \"Stap\" = 'Boekhouding' WHERE \"Stap\" = 'Administraties';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("INSERT OR IGNORE INTO \"Administraties\" (\"Id\", \"Accentkleur\", \"Naam\", \"Soort\") VALUES (2, '#1565C0', 'Stichting', 2);");
        }
    }
}
