using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using BtwBoeker.Application.Beveiliging;
using BtwBoeker.Application.Database;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Infrastructure.Beveiliging;
using BtwBoeker.Infrastructure.Database;

namespace BtwBoeker.Infrastructure.Tests;

public sealed class DatabaseBeheerTests : IDisposable
{
    private const string Wachtwoord = "zonnig vakantiehuis 2026";
    private readonly TijdelijkeMap _map = new();
    private readonly DatabaseBeheer _beheer = new();

    private string DbPad => _map.Bestand("btw-boeker.btwdb");

    public void Dispose() => _map.Dispose();

    [Fact]
    public async Task MaakAan_maakt_database_en_sleutelbestand()
    {
        var herstel = await _beheer.MaakAanAsync(DbPad, Wachtwoord);

        Assert.NotNull(herstel);
        Assert.True(File.Exists(DbPad));
        Assert.True(File.Exists(DatabaseBeheer.SleutelPad(DbPad)));
    }

    [Fact]
    public async Task Nieuwe_database_krijgt_de_identificatie_van_BTW_Boeker()
    {
        await _beheer.MaakAanAsync(DbPad, Wachtwoord);
        var sleutel = SleutelKluis.OntsluitMetWachtwoord(Sleutelbestand.Lees(DatabaseBeheer.SleutelPad(DbPad)), Wachtwoord);
        try
        {
            await using var verbinding = new SqliteConnection(DatabaseBeheer.Verbindingstekst(DbPad, sleutel));
            await verbinding.OpenAsync();
            await using var opdracht = verbinding.CreateCommand();
            opdracht.CommandText = "PRAGMA application_id";

            Assert.Equal(DatabaseBeheer.BtwBoekerApplicatieId, Convert.ToInt32(await opdracht.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            DatabaseBeheer.LeegPool(DbPad, sleutel);
            CryptographicOperations.ZeroMemory(sleutel);
        }
    }

    [Fact]
    public async Task Openen_weigert_een_database_zonder_BTW_Boeker_identificatie()
    {
        await _beheer.MaakAanAsync(DbPad, Wachtwoord);
        var sleutel = SleutelKluis.OntsluitMetWachtwoord(Sleutelbestand.Lees(DatabaseBeheer.SleutelPad(DbPad)), Wachtwoord);
        try
        {
            await using var verbinding = new SqliteConnection(DatabaseBeheer.Verbindingstekst(DbPad, sleutel));
            await verbinding.OpenAsync();
            await using var opdracht = verbinding.CreateCommand();
            opdracht.CommandText = "PRAGMA application_id = 0";
            await opdracht.ExecuteNonQueryAsync();
        }
        finally
        {
            DatabaseBeheer.LeegPool(DbPad, sleutel);
            CryptographicOperations.ZeroMemory(sleutel);
        }

        await Assert.ThrowsAsync<DatabaseBestandException>(() => _beheer.OpenAsync(DbPad, Wachtwoord));
    }

    [Fact]
    public async Task MaakAan_overschrijft_nooit_een_bestaande_database()
    {
        await _beheer.MaakAanAsync(DbPad, Wachtwoord);

        await Assert.ThrowsAsync<DatabaseBestandException>(() => _beheer.MaakAanAsync(DbPad, Wachtwoord));
    }

    [Fact]
    public async Task MaakAan_weigert_zwak_wachtwoord()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _beheer.MaakAanAsync(DbPad, "kort"));
        Assert.False(File.Exists(DbPad));
    }

    [Fact]
    public async Task Nieuwe_database_bevat_vereniging_en_stichting()
    {
        await _beheer.MaakAanAsync(DbPad, Wachtwoord);

        await using var sessie = await _beheer.OpenAsync(DbPad, Wachtwoord);
        var administraties = await sessie.Administraties.HaalAlleAsync();

        Assert.Collection(
            administraties,
            a =>
            {
                Assert.Equal(AdministratieSoort.Vereniging, a.Soort);
                Assert.Equal("Mijn administratie", a.Naam); // keuze A (2026-11-10): een neutrale standaardnaam
                Assert.Equal(Administratie.Standaardkleur(AdministratieSoort.Vereniging), a.Accentkleur);
            });
    }

    [Fact]
    public async Task Openen_met_onjuist_wachtwoord_faalt()
    {
        await _beheer.MaakAanAsync(DbPad, Wachtwoord);

        await Assert.ThrowsAsync<OngeldigeSleutelException>(() => _beheer.OpenAsync(DbPad, "dit is niet het wachtwoord"));
    }

    [Fact]
    public async Task Openen_zonder_sleutelbestand_geeft_duidelijke_fout()
    {
        await _beheer.MaakAanAsync(DbPad, Wachtwoord);
        File.Delete(DatabaseBeheer.SleutelPad(DbPad));

        await Assert.ThrowsAsync<DatabaseBestandException>(() => _beheer.OpenAsync(DbPad, Wachtwoord));
    }

    [Fact]
    public async Task Openen_van_niet_bestaande_database_geeft_duidelijke_fout()
    {
        await Assert.ThrowsAsync<DatabaseBestandException>(() => _beheer.OpenAsync(DbPad, Wachtwoord));
    }

    [Fact]
    public async Task Databasebestand_is_versleuteld()
    {
        await _beheer.MaakAanAsync(DbPad, Wachtwoord);
        await using (var sessie = await _beheer.OpenAsync(DbPad, Wachtwoord))
        {
            var vereniging = (await sessie.Administraties.HaalAlleAsync())[0];
            vereniging.WijzigNaam("Vakantiehuis Geheimnaam");
            await sessie.Administraties.BewaarAsync(vereniging);
        }

        var ruw = Encoding.Latin1.GetString(await File.ReadAllBytesAsync(DbPad));

        Assert.DoesNotContain("SQLite format 3", ruw, StringComparison.Ordinal);
        Assert.DoesNotContain("Geheimnaam", ruw, StringComparison.Ordinal);
        Assert.DoesNotContain("Administraties", ruw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Herhaald_openen_is_idempotent_en_behoudt_gegevens()
    {
        await _beheer.MaakAanAsync(DbPad, Wachtwoord);
        await using (var sessie = await _beheer.OpenAsync(DbPad, Wachtwoord))
        {
            var administratie = Assert.Single(await sessie.Administraties.HaalAlleAsync());
            administratie.WijzigAccentkleur(Kleur.Parse("#6A1B9A"));
            await sessie.Administraties.BewaarAsync(administratie);
        }

        await using (var sessie = await _beheer.OpenAsync(DbPad, Wachtwoord))
        {
            var administratie = Assert.Single(await sessie.Administraties.HaalAlleAsync());
            Assert.Equal(Kleur.Parse("#6A1B9A"), administratie.Accentkleur);
        }
    }

    [Fact]
    public async Task Herstelsleutel_opent_database_en_stelt_nieuw_wachtwoord_in()
    {
        var herstel = await _beheer.MaakAanAsync(DbPad, Wachtwoord);

        await using (var sessie = await _beheer.OpenMetHerstelsleutelAsync(DbPad, herstel, "mijn nieuwe wachtwoord"))
        {
            Assert.Single(await sessie.Administraties.HaalAlleAsync());
        }

        await Assert.ThrowsAsync<OngeldigeSleutelException>(() => _beheer.OpenAsync(DbPad, Wachtwoord));
        await using var opnieuw = await _beheer.OpenAsync(DbPad, "mijn nieuwe wachtwoord");
        Assert.NotNull(opnieuw);
    }

    [Fact]
    public async Task Onjuiste_herstelsleutel_wijzigt_niets()
    {
        await _beheer.MaakAanAsync(DbPad, Wachtwoord);

        await Assert.ThrowsAsync<OngeldigeSleutelException>(
            () => _beheer.OpenMetHerstelsleutelAsync(DbPad, Herstelsleutel.Genereer(), "mijn nieuwe wachtwoord"));

        await using var sessie = await _beheer.OpenAsync(DbPad, Wachtwoord);
        Assert.NotNull(sessie);
    }

    [Fact]
    public async Task Backup_kan_teruggezet_en_geopend_worden()
    {
        var backup = _map.Bestand("backup.btwbackup");
        var hersteld = _map.Bestand("hersteld.btwdb");
        await _beheer.MaakAanAsync(DbPad, Wachtwoord);
        await using (var sessie = await _beheer.OpenAsync(DbPad, Wachtwoord))
        {
            var vereniging = (await sessie.Administraties.HaalAlleAsync())[0];
            vereniging.WijzigNaam("Vakantiehuis Teruggezet");
            await sessie.Administraties.BewaarAsync(vereniging);
            await sessie.MaakBackupAsync(backup);
        }

        await _beheer.HerstelBackupAsync(backup, hersteld);

        await using var herstelSessie = await _beheer.OpenAsync(hersteld, Wachtwoord);
        Assert.Equal("Vakantiehuis Teruggezet", (await herstelSessie.Administraties.HaalAlleAsync())[0].Naam);
    }

    [Fact]
    public async Task Backup_is_versleuteld()
    {
        var backup = _map.Bestand("backup.btwbackup");
        await _beheer.MaakAanAsync(DbPad, Wachtwoord);
        await using (var sessie = await _beheer.OpenAsync(DbPad, Wachtwoord))
        {
            await sessie.MaakBackupAsync(backup);
        }

        var hersteld = _map.Bestand("controle.btwdb");
        await _beheer.HerstelBackupAsync(backup, hersteld);
        var ruw = Encoding.Latin1.GetString(await File.ReadAllBytesAsync(hersteld));

        Assert.DoesNotContain("SQLite format 3", ruw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Terugzetten_overschrijft_nooit_een_bestaande_database()
    {
        var backup = _map.Bestand("backup.btwbackup");
        await _beheer.MaakAanAsync(DbPad, Wachtwoord);
        await using (var sessie = await _beheer.OpenAsync(DbPad, Wachtwoord))
        {
            await sessie.MaakBackupAsync(backup);
        }

        await Assert.ThrowsAsync<DatabaseBestandException>(() => _beheer.HerstelBackupAsync(backup, DbPad));
    }

    [Fact]
    public async Task Terugzetten_van_ongeldig_bestand_geeft_duidelijke_fout()
    {
        var nep = _map.Bestand("nep.btwbackup");
        await File.WriteAllTextAsync(nep, "geen back-up");

        await Assert.ThrowsAsync<DatabaseBestandException>(() => _beheer.HerstelBackupAsync(nep, _map.Bestand("x.btwdb")));
        Assert.False(File.Exists(_map.Bestand("x.btwdb")));
    }
}
