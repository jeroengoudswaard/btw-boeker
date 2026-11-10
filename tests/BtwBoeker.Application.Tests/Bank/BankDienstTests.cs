using BtwBoeker.Application.Bank;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Tests.Boekhouding;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Bank;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Tests.Bank;

public sealed class BankDienstTests : IAsyncDisposable
{
    private const Domain.Administraties.AdministratieSoort Ver = BoekhoudingTestdata.Ver;
    private readonly BoekhoudingTestdata _data = new();
    private readonly BoekhoudingDienstTests.FakeSessie _sessie;
    private readonly BankDienst _bank;
    private readonly BoekhoudingDienst _boekhouding;

    public BankDienstTests()
    {
        _sessie = new BoekhoudingDienstTests.FakeSessie(_data);
        var tijd = new BoekhoudingDienstTests.VasteTijd(new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero));
        _bank = new BankDienst(_sessie, tijd);
        _boekhouding = new BoekhoudingDienst(_sessie, tijd);
    }

    public ValueTask DisposeAsync() => _sessie.DisposeAsync();

    private static CamtBestand Bestand(params VbTransactie[] transacties) => Camt053Lezer.Lees(CamtVoorbeeld.Stroom(
        CamtVoorbeeld.Xml(CamtVoorbeeld.Betaal, "08", new VbAfschrift(new DateOnly(2026, 2, 3), 0, transacties))));

    private static readonly VbTransactie Energie = new(-4500, "ICDT", "ESCT", "520", Ref: "E1", Naam: "Energie BV");
    private static readonly VbTransactie Onbekend = new(2000, Ref: "O1", Naam: "Iemand", NtryRef: "2");

    private Task<BankimportResultaat> ImporteerAsync(CamtBestand bestand)
    {
        var voorstel = BankimportPlanner.Plan(_data.Stand, [bestand]);
        var nieuw = voorstel.Rekeningen.Where(r => r.Dagboek is null)
            .ToDictionary(r => r.Iban.Waarde, _ => new NieuweBankrekening(_data.Bank.Id, "RABO", "Rabobank betaalrekening"));
        return _bank.ImporteerAsync(Ver, voorstel, nieuw);
    }

    private void RegelEnergie() =>
        _data.Boekingsregels.Add(new Boekingsregel(Ver, "Energie", _data.Energie, new BoekingsregelVoorwaarden(NaamBevat: "energie"), "Energie", 1));

    [Fact]
    public async Task Import_maakt_bankdagboek_bewaart_mutaties_en_herkent_met_regels()
    {
        RegelEnergie();

        var resultaat = await ImporteerAsync(Bestand(Energie, Onbekend));

        Assert.Equal(new BankimportResultaat(2, 1, 1), resultaat);
        Assert.Single(_sessie.Backups);
        var rabo = _data.Dagboeken.Single(d => d.Soort == DagboekSoort.Bank);
        Assert.Equal(CamtVoorbeeld.Betaal, rabo.Iban?.Waarde);
        Assert.Same(_data.Bank, rabo.Rekening);
        Assert.Single(_data.Bankafschriften);
        var concept = Assert.Single(_data.Posten);
        Assert.False(concept.IsGeboekt);
        Assert.Equal("Energie", concept.Omschrijving);
        Assert.Equal(concept.Id, _data.Bankmutaties.Single(m => m.Tegennaam == "Energie BV").PostId);
        Assert.Null(_data.Bankmutaties.Single(m => m.Tegennaam == "Iemand").PostId);

        var opnieuw = await ImporteerAsync(Bestand(Energie, Onbekend));
        Assert.Equal(new BankimportResultaat(0, 0, 0), opnieuw);
        Assert.Equal(2, _data.Bankmutaties.Count);
    }

    [Fact]
    public async Task Toewijzen_en_concepten_boeken_in_volgorde_van_datum()
    {
        await ImporteerAsync(Bestand(Energie, Onbekend));
        var onbekend = _data.Bankmutaties.Single(m => m.Tegennaam == "Iemand");

        await _bank.WijsToeAsync(Ver, onbekend.Id, _data.Welpen.Id, "Contributie");
        var energie = _data.Bankmutaties.Single(m => m.Tegennaam == "Energie BV");
        await _bank.WijsToeAsync(Ver, energie.Id, _data.Energie.Id, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _bank.WijsToeAsync(Ver, energie.Id, _data.Energie.Id, null));

        var geboekt = await _bank.BoekConceptenAsync(Ver, null);

        Assert.Equal(2, geboekt.Aantal);
        Assert.Empty(geboekt.Fouten);
        Assert.All(_data.Posten, p => Assert.True(p.IsGeboekt));
        Assert.Equal(["RABO-2026-0001", "RABO-2026-0002"], _data.Posten.OrderBy(p => p.Volgnummer).Select(p => p.Nummer));
        var saldi = Bankoverzicht.Saldi(_data.Stand, _data.Dagboeken.Single(d => d.Soort == DagboekSoort.Bank));
        Assert.Equal(-2500, saldi.Bank);
        Assert.Equal(-2500, saldi.Grootboek);
        Assert.True(saldi.IsAangesloten);
    }

    [Fact]
    public async Task Concept_verwijderen_en_tegenboeken_zetten_de_mutatie_terug_in_de_werkvoorraad()
    {
        RegelEnergie();
        await ImporteerAsync(Bestand(Energie));
        var mutatie = _data.Bankmutaties.Single();

        await _boekhouding.VerwijderConceptAsync(Ver, mutatie.PostId!.Value);
        Assert.Null(mutatie.PostId);
        Assert.Equal(MutatieStatus.TeVerwerken, Bankoverzicht.Status(_data.Stand, mutatie));

        await _bank.WijsToeAsync(Ver, mutatie.Id, _data.Energie.Id, null);
        await _bank.BoekConceptenAsync(Ver, null);
        Assert.Equal(MutatieStatus.Geboekt, Bankoverzicht.Status(_data.Stand, mutatie));

        await _boekhouding.TegenboekAsync(Ver, mutatie.PostId!.Value, new DateOnly(2026, 2, 10));
        Assert.Null(mutatie.PostId);
        Assert.Equal(0, Grootboek.Saldo(_data.Stand, _data.Bank, 2026));
    }

    [Fact]
    public async Task Bankregel_van_een_concept_staat_vast_en_handmatige_bankpost_wordt_geweigerd()
    {
        RegelEnergie();
        await ImporteerAsync(Bestand(Energie));
        var mutatie = _data.Bankmutaties.Single();
        var rabo = mutatie.Dagboek;

        var gewijzigd = new PostInvoer(mutatie.PostId, rabo.Id, new DateOnly(2026, 2, 3), "Energie",
            [new RegelInvoerId(_data.Bank.Id, null, Bedrag.Nul, new Bedrag(4000)), new RegelInvoerId(_data.Energie.Id, null, new Bedrag(4000), Bedrag.Nul)]);
        var fout = await Assert.ThrowsAsync<InvalidOperationException>(() => _boekhouding.BewaarConceptAsync(Ver, gewijzigd));
        Assert.Contains("bankregel", fout.Message, StringComparison.Ordinal);

        var gesplitst = gewijzigd with
        {
            Regels =
            [
                new RegelInvoerId(_data.Bank.Id, null, Bedrag.Nul, new Bedrag(4500)),
                new RegelInvoerId(_data.Energie.Id, null, new Bedrag(3000), Bedrag.Nul),
                new RegelInvoerId(_data.Welpen.Id, null, new Bedrag(1500), Bedrag.Nul),
            ],
        };
        await _boekhouding.BewaarConceptAsync(Ver, gesplitst);
        Assert.Equal(3, _data.Posten.Single().Regels.Count);

        var handmatig = gesplitst with { Id = null };
        await Assert.ThrowsAsync<InvalidOperationException>(() => _boekhouding.BewaarConceptAsync(Ver, handmatig));
    }

    [Fact]
    public async Task Regels_bewaren_verplaatsen_en_toepassen_op_de_werkvoorraad()
    {
        await ImporteerAsync(Bestand(Energie, Onbekend));

        var eerste = await _bank.BewaarRegelAsync(Ver, new BoekingsregelInvoer(null, "Iemand", _data.Welpen.Id, new BoekingsregelVoorwaarden(NaamBevat: "iemand"), null, true));
        var tweede = await _bank.BewaarRegelAsync(Ver, new BoekingsregelInvoer(null, "Energie", _data.Energie.Id, new BoekingsregelVoorwaarden(NaamBevat: "energie"), null, true));
        Assert.Equal([eerste, tweede], _data.Stand.Boekingsregels.Select(r => r.Id));

        await _bank.VerplaatsRegelAsync(Ver, tweede, -1);
        Assert.Equal([tweede, eerste], _data.Stand.Boekingsregels.Select(r => r.Id));

        Assert.Equal(2, await _bank.PasRegelsToeAsync(Ver));
        Assert.All(_data.Bankmutaties, m => Assert.NotNull(m.PostId));

        await _bank.VerwijderRegelAsync(Ver, eerste);
        Assert.Single(_data.Boekingsregels);
    }

    [Fact]
    public async Task Opnieuw_inlezen_vult_batchkenmerk_en_retourreden_aan()
    {
        var rabo = _data.Bankdagboek("RABO", CamtVoorbeeld.Betaal);
        var storno = new VbTransactie(-1000, "IDDT", "UPDD", "631", Ref: "S9", Reden: "MD06");
        var bestand = Bestand(storno);
        var sleutel = BankimportPlanner.Sleutel(bestand.Afschriften[0].Iban, bestand.Afschriften[0].Transacties[0]);
        var oud = new Bankmutatie(Ver, rabo, sleutel, new DateOnly(2026, 2, 3), new Bedrag(1000), false, default, null, null, null, null, null, null);
        _data.Bankmutaties.Add(oud);

        await _bank.ImporteerAsync(Ver, BankimportPlanner.Plan(_data.Stand, [bestand]), new Dictionary<string, NieuweBankrekening>());

        Assert.Equal("MD06", oud.Retourreden);
        Assert.Single(_data.Bankmutaties);
    }
}
