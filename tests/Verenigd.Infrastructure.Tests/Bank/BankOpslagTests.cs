using Microsoft.EntityFrameworkCore;
using Verenigd.Application.Bank;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Application.Tests.Bank;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Bank;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Infrastructure.Database;

namespace Verenigd.Infrastructure.Tests.Bank;

/// <summary>Bankimport op een echte, versleutelde database, met synthetische CAMT.053-bestanden.</summary>
public sealed class BankOpslagTests : IAsyncLifetime, IDisposable
{
    private const string Wachtwoord = "scouting is fantastisch";
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private readonly TijdelijkeMap _map = new();
    private readonly DatabaseBeheer _beheer = new();
    private IDatabaseSessie _sessie = null!;
    private BoekhoudingDienst _boekhouding = null!;
    private BankDienst _bank = null!;

    public async Task InitializeAsync()
    {
        var pad = _map.Bestand("test.vdb");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        _sessie = await _beheer.OpenAsync(pad, Wachtwoord);
        var tijd = TimeProvider.System;
        _boekhouding = new BoekhoudingDienst(_sessie, tijd);
        _bank = new BankDienst(_sessie, tijd);
        await _boekhouding.RichtInAsync(Ver);
        if ((await _boekhouding.LaadAsync(Ver)).Boekjaar(2026) is null)
        {
            await _boekhouding.OpenBoekjaarAsync(Ver, 2026);
        }

        foreach (var (nummer, naam, soort, rol) in new[]
        {
            (1100, "Rabobank betaal", RekeningSoort.Balans, RekeningRol.Geen),
            (1110, "Rabobank spaar", RekeningSoort.Balans, RekeningRol.Geen),
            (1298, "Kruisposten", RekeningSoort.Balans, RekeningRol.Kruisposten),
            (4115, "Energie", RekeningSoort.Resultaat, RekeningRol.Geen),
        })
        {
            await _boekhouding.BewaarRekeningAsync(Ver, new RekeningInvoer(null, nummer, naam, null, soort, rol, true));
        }
    }

    public async Task DisposeAsync() => await _sessie.DisposeAsync();

    public void Dispose() => _map.Dispose();

    private async Task<int> Rekening(int nummer) => (await _boekhouding.LaadAsync(Ver)).Rekeningen.Single(r => r.Nummer == nummer).Id;

    private static CamtBestand Bestand(string iban, params VbTransactie[] t) =>
        Camt053Lezer.Lees(CamtVoorbeeld.Stroom(CamtVoorbeeld.Xml(iban, "08", new VbAfschrift(new DateOnly(2026, 3, 2), 0, t))));

    private async Task<BankimportResultaat> ImporteerAsync()
    {
        var bestanden = new[]
        {
            Bestand(CamtVoorbeeld.Betaal,
                new VbTransactie(-4500, "ICDT", "ESCT", "520", Ref: "E1", Naam: "Energie BV"),
                new VbTransactie(-10000, "ICDT", "ESCT", "544", Ref: "S1", TegenIban: CamtVoorbeeld.Spaar, Naam: "Eigen spaar", NtryRef: "2")),
            Bestand(CamtVoorbeeld.Spaar, new VbTransactie(10000, Ref: "S2", TegenIban: CamtVoorbeeld.Betaal, Naam: "Eigen betaal")),
        };
        var voorstel = BankimportPlanner.Plan(await _boekhouding.LaadAsync(Ver), bestanden);
        var nieuw = new Dictionary<string, NieuweBankrekening>
        {
            [CamtVoorbeeld.Betaal] = new(await Rekening(1100), "RABO", "Rabobank betaalrekening"),
            [CamtVoorbeeld.Spaar] = new(await Rekening(1110), "RSPA", "Rabobank spaarrekening"),
        };
        return await _bank.ImporteerAsync(Ver, voorstel, nieuw);
    }

    [Fact]
    public async Task Import_herkenning_en_boeken_worden_bewaard_en_sluiten_aan()
    {
        await _bank.BewaarRegelAsync(Ver, new BoekingsregelInvoer(null, "Energie", await Rekening(4115), new BoekingsregelVoorwaarden(NaamBevat: "energie"), null, true));

        var resultaat = await ImporteerAsync();

        Assert.Equal(new BankimportResultaat(3, 3, 0), resultaat);
        var stand = await _boekhouding.LaadAsync(Ver);
        var rabo = stand.Dagboeken.Single(d => d.Code == "RABO");
        Assert.Equal(CamtVoorbeeld.Betaal, rabo.Iban?.Waarde);
        Assert.Equal(2, stand.Bankafschriften.Count);
        Assert.Equal(3, stand.Bankmutaties.Count);
        Assert.All(stand.Bankmutaties, m => Assert.Equal(MutatieStatus.Concept, Bankoverzicht.Status(stand, m)));
        Assert.Equal("PMNT-ICDT-ESCT", stand.Bankmutaties.Single(m => m.Tegennaam == "Energie BV").Code.Iso);
        Assert.Single(stand.Boekingsregels);

        var geboekt = await _bank.BoekConceptenAsync(Ver, null);
        Assert.Equal(3, geboekt.Aantal);
        stand = await _boekhouding.LaadAsync(Ver);
        Assert.True(Bankoverzicht.Saldi(stand, rabo).IsAangesloten);
        Assert.Equal(0, Grootboek.Saldo(stand, stand.Rekeningen.Single(r => r.Nummer == 1298), 2026));

        Assert.Equal(new BankimportResultaat(0, 0, 0), await ImporteerAsync());
    }

    [Fact]
    public async Task Dezelfde_sleutel_kan_de_database_niet_twee_keer_in()
    {
        await ImporteerAsync();

        var fout = await Assert.ThrowsAnyAsync<Exception>(() => _sessie.Boekhouding.PasToeAsync(Ver, stand =>
        {
            var bestaand = stand.Bankmutaties[0];
            var wijziging = new BoekhoudingWijziging<bool>(true);
            wijziging.Toevoegen.Add(new Bankmutatie(Ver, bestaand.Dagboek, bestaand.Sleutel, bestaand.Boekdatum, bestaand.Bedrag, bestaand.IsBij,
                bestaand.Code, null, null, null, null, null, null));
            return wijziging;
        }));
        Assert.IsType<DbUpdateException>(fout);
    }

    [Fact]
    public async Task Concept_verwijderen_zet_de_transactie_terug_in_de_werkvoorraad()
    {
        await ImporteerAsync();
        var stand = await _boekhouding.LaadAsync(Ver);
        var mutatie = stand.Bankmutaties.Single(m => m.Tegennaam == "Eigen spaar");

        await _boekhouding.VerwijderConceptAsync(Ver, mutatie.PostId!.Value);

        stand = await _boekhouding.LaadAsync(Ver);
        Assert.Null(stand.Bankmutaties.Single(m => m.Id == mutatie.Id).PostId);
    }
}
