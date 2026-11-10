using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BtwBoeker.Infrastructure.Database;

namespace BtwBoeker.Infrastructure.Tests;

/// <summary>
/// Een bestaande database wordt alleen via een geteste migratie bijgewerkt (privacyregel in CLAUDE.md): vervallen
/// onderdelen verdwijnen, de rest van de administratie blijft ongewijzigd.
/// </summary>
public sealed class MigratieTests : IDisposable
{
    private const string Basis = "20261109103812_BtwBoekerBasis";
    private readonly TijdelijkeMap _map = new();

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _map.Dispose();
    }

    private BtwBoekerDbContext Context() => new(new DbContextOptionsBuilder<BtwBoekerDbContext>()
        .UseSqlite($"Data Source={_map.Bestand("oud.btwdb")};Pooling=False").Options);

    private static async Task<bool> TabelBestaatAsync(BtwBoekerDbContext context, string naam)
    {
        var verbinding = context.Database.GetDbConnection();
        await verbinding.OpenAsync();
        try
        {
            await using var opdracht = verbinding.CreateCommand();
            opdracht.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $naam";
            opdracht.Parameters.Add(new SqliteParameter("$naam", naam));
            return Convert.ToInt64(await opdracht.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) > 0;
        }
        finally
        {
            await verbinding.CloseAsync();
        }
    }

    [Fact]
    public async Task Begroting_en_meerjarenplan_verdwijnen_en_het_rekeningschema_blijft()
    {
        await using (var oud = Context())
        {
            await oud.GetService<IMigrator>().MigrateAsync(Basis);
            await oud.Database.ExecuteSqlRawAsync(
                "INSERT INTO Rubrieken (Administratie, Naam, Volgorde) VALUES (1, 'Verhuur', 1)");
            Assert.True(await TabelBestaatAsync(oud, "Begrotingen"));
        }

        await using var nieuw = Context();
        await nieuw.Database.MigrateAsync();

        foreach (var tabel in new[] { "Begrotingen", "Begrotingsregels", "Meerjarenplannen", "Meerjarenposten", "Meerjarenbedragen" })
        {
            Assert.False(await TabelBestaatAsync(nieuw, tabel), tabel);
        }

        Assert.True(await TabelBestaatAsync(nieuw, "Jaarrekeningindelingen"));
        Assert.Equal("Verhuur", Assert.Single(await nieuw.Rubrieken.ToListAsync()).Naam);
    }

    [Fact]
    public async Task Historie_uit_excel_verdwijnt_en_gewone_boekjaren_blijven()
    {
        await using (var oud = Context())
        {
            await oud.GetService<IMigrator>().MigrateAsync(Basis);
            // Status 1 = open, 2 = afgesloten; 2025 is een historisch boekjaar uit de vervallen Excel-overname, 2026 een gewoon jaar.
            await oud.Database.ExecuteSqlRawAsync(
                "INSERT INTO Boekjaren (Administratie, Jaar, Status, AfgeslotenUtc, IsHistorisch) VALUES (1, 2025, 2, '2026-01-01', 1), (1, 2026, 1, NULL, 0)");
        }

        await using var nieuw = Context();
        await nieuw.Database.MigrateAsync();

        Assert.False(await TabelBestaatAsync(nieuw, "HistorischeRegels"));
        Assert.False(await TabelBestaatAsync(nieuw, "HistorischeTotalen"));
        var jaar = Assert.Single(await nieuw.Boekjaren.ToListAsync());
        Assert.Equal(2026, jaar.Jaar);
        Assert.True(jaar.IsOpen);
    }
}
