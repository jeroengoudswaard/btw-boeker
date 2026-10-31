using System.Text;
using Verenigd.Application.Database;
using Verenigd.Application.Import;
using Verenigd.Domain.Leden;
using Verenigd.Infrastructure.Database;
using static Verenigd.Application.Tests.Import.Sol3Voorbeeld;

namespace Verenigd.Infrastructure.Tests.Leden;

/// <summary>De SOL3-import op een echte, versleutelde database.</summary>
public sealed class LedenImportTests : IAsyncLifetime, IDisposable
{
    private const string Wachtwoord = "scouting is fantastisch";
    private const string Anna = "100000001";
    private const string Bo = "100000002";
    private readonly TijdelijkeMap _map = new();
    private readonly DatabaseBeheer _beheer = new();
    private readonly VasteTijd _tijd = new(new DateTimeOffset(2026, 10, 27, 10, 15, 0, TimeSpan.FromHours(1)));
    private IDatabaseSessie _sessie = null!;

    private string DbPad => _map.Bestand("leden.vdb");

    public async Task InitializeAsync()
    {
        await _beheer.MaakAanAsync(DbPad, Wachtwoord);
        _sessie = await _beheer.OpenAsync(DbPad, Wachtwoord);
    }

    public async Task DisposeAsync() => await _sessie.DisposeAsync();

    public void Dispose() => _map.Dispose();

    private Dictionary<Sol3Export, string> Exports(
        string[][]? leden = null, string[][]? huishouden = null, string[][]? voogden = null, string[][]? functies = null)
    {
        var map = Directory.CreateDirectory(_map.Bestand(Guid.NewGuid().ToString("N"))).FullName;
        string Schrijf(string naam, byte[] inhoud)
        {
            var pad = Path.Combine(map, naam);
            File.WriteAllBytes(pad, inhoud);
            return pad;
        }

        return new Dictionary<Sol3Export, string>
        {
            [Sol3Export.LedenBasis] = Schrijf("1.csv", Csv(KopLeden, leden ?? [Lid(Anna), Lid(Bo, voornaam: "Bo", contactpersoonId: "003Ab00000AbCdE")])),
            [Sol3Export.Huishouden] = Schrijf("2.csv", Csv(KopHuishouden, huishouden ??
                [Huishouden(Anna, iban: "NL91ABNA0417164300", rekeninghouder: "P. de Vries"),
                 Huishouden(Bo, iban: "NL91ABNA0417164300", contactpersoonId: "003Ab00000AbCdE")])),
            [Sol3Export.Voogden] = Schrijf("3.csv", Csv(KopVoogden, voogden ?? [Voogden(Anna, naam2: "Voogd 2", email2: "q@example.org")])),
            [Sol3Export.Functies] = Schrijf("4.csv", Csv(KopFuncties, functies ??
            [
                Functie(Anna, rol: "Lid", groep: "Welpen", start: "19-09-2025"),
                Functie(Bo, rol: "Leider", groep: "Welpen", start: "28-01-2022", type: "Kaderlid"),
                Functie(Bo, rol: "Lid", groep: "Vrijwilligers (Migratie)", start: "07-11-2018", type: "Kaderlid"),
            ])),
        };
    }

    private async Task<ImportResultaat> ImporteerAsync(Dictionary<Sol3Export, string> exports, Func<ImportVoorstel, ImportKeuzes>? keuzes = null)
    {
        var dienst = new Sol3ImportDienst(_sessie, _tijd);
        var analyse = await dienst.AnalyseerAsync(exports);
        return await dienst.VoerUitAsync(analyse, (keuzes ?? ImportKeuzes.Standaard)(analyse.Voorstel));
    }

    [Fact]
    public async Task Import_slaat_personen_functies_voogden_en_groepen_op()
    {
        var resultaat = await ImporteerAsync(Exports());

        Assert.Equal(2, resultaat.Nieuw);
        var personen = await _sessie.Leden.HaalAlleAsync();
        var anna = personen.Single(p => p.Lidnummer.Waarde == Anna);
        Assert.Equal("Anna de Vries", anna.VolledigeNaam);
        Assert.Equal(new DateOnly(2015, 3, 1), anna.Geboortedatum);
        Assert.Equal("+31612345678", anna.Telefoon);
        Assert.Equal("1234 AB", anna.Postcode);
        Assert.Equal("Ergens", anna.Plaats);
        Assert.Equal("Welpen", Assert.Single(anna.Functies).Groep.Naam);
        Assert.Equal(2, anna.Voogden.Count);
        Assert.True(anna.Voogden.Single(v => v.Volgnummer == 2).NaamOnbekend);
        Assert.Contains(anna.Herkomst, h => h.Veld == "Voornaam" && h.Bron == Veldbron.Sol3);

        var bo = personen.Single(p => p.Lidnummer.Waarde == Bo);
        Assert.Equal("003Ab00000AbCdE", bo.ContactpersoonId);
        Assert.Equal(new DateOnly(2018, 11, 7), bo.LidSinds);

        var groepen = await _sessie.Leden.HaalGroepenAsync();
        Assert.Equal(GroepSoort.Technisch, groepen.Single(g => g.Naam == "Vrijwilligers (Migratie)").Soort);
        Assert.Equal(GroepSoort.Speltak, groepen.Single(g => g.Naam == "Welpen").Soort);
    }

    [Fact]
    public async Task Groepsindeling_is_ook_na_de_import_te_wijzigen()
    {
        await ImporteerAsync(Exports());

        await _sessie.Leden.BewaarGroepsoortenAsync(new Dictionary<string, GroepSoort> { ["Welpen"] = GroepSoort.Overig }); // besluit 215

        var groepen = await _sessie.Leden.HaalGroepenAsync();
        Assert.Equal(GroepSoort.Overig, groepen.Single(g => g.Naam == "Welpen").Soort);
        Assert.Equal(GroepSoort.Technisch, groepen.Single(g => g.Naam == "Vrijwilligers (Migratie)").Soort);
    }

    [Fact]
    public async Task Zelfde_iban_wordt_een_betaler_gekoppeld_via_lidnummer()
    {
        await ImporteerAsync(Exports());

        var stand = await _sessie.LedenImport.LaadAsync();
        var betaler = Assert.Single(stand.Betalers);
        Assert.Equal("NL91ABNA0417164300", betaler.Iban.Waarde);
        Assert.Equal("P. de Vries", betaler.Rekeninghouder);
        Assert.Equal(2, stand.Koppelingen.Count);
        Assert.All(stand.Koppelingen, k => Assert.Equal(betaler.Id, k.BetalerId));
    }

    [Fact]
    public async Task Dezelfde_import_opnieuw_geeft_geen_wijzigingen()
    {
        await ImporteerAsync(Exports());

        var analyse = await new Sol3ImportDienst(_sessie, _tijd).AnalyseerAsync(Exports());

        Assert.True(analyse.Voorstel.IsLeeg, string.Join("; ", analyse.Voorstel.Gewijzigd.SelectMany(r => r.Wijzigingen)));
    }

    [Fact]
    public async Task Wijzigingen_uitschrijven_en_verwijderde_voogd_worden_opgeslagen()
    {
        await ImporteerAsync(Exports());

        var resultaat = await ImporteerAsync(Exports(
            leden: [Lid(Anna, email: "nieuw@example.org")],
            huishouden: [Huishouden(Anna, iban: "NL91ABNA0417164300", rekeninghouder: "P. de Vries")],
            voogden: [Voogden(Anna)],
            functies: [Functie(Anna, rol: "Lid", groep: "Welpen", start: "19-09-2025")]));

        Assert.Equal(1, resultaat.Gewijzigd);
        Assert.Equal(1, resultaat.Uitgeschreven);
        var personen = await _sessie.Leden.HaalAlleAsync();
        var anna = personen.Single(p => p.Lidnummer.Waarde == Anna);
        Assert.Equal("nieuw@example.org", anna.Email);
        Assert.Single(anna.Voogden);
        var bo = personen.Single(p => p.Lidnummer.Waarde == Bo);
        Assert.Equal(new DateOnly(2026, 10, 27), bo.UitgeschrevenOp);
        Assert.All(bo.Functies, f => Assert.Equal(new DateOnly(2026, 10, 27), f.Einddatum));
    }

    [Fact]
    public async Task Niet_bevestigde_uitschrijving_wordt_niet_uitgevoerd()
    {
        await ImporteerAsync(Exports());

        await ImporteerAsync(
            Exports(leden: [Lid(Anna)], huishouden: [Huishouden(Anna)], voogden: [], functies: [Functie(Anna)]),
            v => ImportKeuzes.Standaard(v) with { Uitschrijven = new HashSet<Domain.Gedeeld.Lidnummer>() });

        Assert.All(await _sessie.Leden.HaalAlleAsync(), p => Assert.False(p.IsUitgeschreven));
    }

    [Fact]
    public async Task Fout_tijdens_toepassen_laat_database_ongewijzigd()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sessie.LedenImport.PasToeAsync(stand =>
        {
            throw new InvalidOperationException("test");
        }));

        await Assert.ThrowsAnyAsync<Exception>(() => _sessie.LedenImport.PasToeAsync(_ => new ImportWijzigingen(
            [], [new Groep("Dubbel", GroepSoort.Overig), new Groep("Dubbel", GroepSoort.Overig)], [], [], 0, 0)));

        Assert.Empty(await _sessie.Leden.HaalGroepenAsync());
    }

    [Fact]
    public async Task Import_maakt_eerst_een_backup_die_kan_worden_teruggezet()
    {
        await ImporteerAsync(Exports());
        var resultaat = await ImporteerAsync(Exports(leden: [Lid(Anna, voornaam: "Anne")]));

        Assert.True(File.Exists(resultaat.Backup));
        var hersteld = _map.Bestand("hersteld.vdb");
        await _beheer.HerstelBackupAsync(resultaat.Backup, hersteld);
        await using var sessie = await _beheer.OpenAsync(hersteld, Wachtwoord);
        Assert.Contains(await sessie.Leden.HaalAlleAsync(), p => p.Voornaam == "Anna");
    }

    [Fact]
    public async Task Notities_worden_opgeslagen_met_herkomst_verenigd()
    {
        await ImporteerAsync(Exports());
        var anna = (await _sessie.Leden.HaalAlleAsync()).Single(p => p.Lidnummer.Waarde == Anna);

        await _sessie.Leden.BewaarNotitiesAsync(anna.Id, "Zwemdiploma A", new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc));

        var opnieuw = (await _sessie.Leden.HaalAlleAsync()).Single(p => p.Id == anna.Id);
        Assert.Equal("Zwemdiploma A", opnieuw.Notities);
        Assert.Equal(Veldbron.Verenigd, opnieuw.Herkomst.Single(h => h.Veld == "Notities").Bron);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sessie.Leden.BewaarNotitiesAsync(Guid.NewGuid(), "x", DateTime.UtcNow));
    }

    [Fact]
    public async Task Ledengegevens_staan_niet_leesbaar_in_het_databasebestand()
    {
        await ImporteerAsync(Exports());
        await _sessie.DisposeAsync();

        var inhoud = Encoding.Latin1.GetString(await File.ReadAllBytesAsync(DbPad));
        Assert.DoesNotContain(Anna, inhoud, StringComparison.Ordinal);
        Assert.DoesNotContain("NL91ABNA", inhoud, StringComparison.Ordinal);
        Assert.DoesNotContain("Vries", inhoud, StringComparison.Ordinal);

        _sessie = await _beheer.OpenAsync(DbPad, Wachtwoord);
    }

    private sealed class VasteTijd(DateTimeOffset nu) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nu.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone("test", nu.Offset, "test", "test");
    }
}
