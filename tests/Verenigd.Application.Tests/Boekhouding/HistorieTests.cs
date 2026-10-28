using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Tests.Boekhouding;

/// <summary>Historisch boekjaar 2025 uit de T-rekeningen van de Excel-boekhouding (besluiten 122–126). Alle gegevens zijn verzonnen.</summary>
public sealed class HistorieTests : IAsyncDisposable
{
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private readonly BoekhoudingTestdata _data = new(ingericht: false);
    private readonly BoekhoudingDienstTests.FakeSessie _sessie;
    private readonly BoekhoudingDienstTests.VasteTijd _tijd = new(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero));

    public HistorieTests() => _sessie = new BoekhoudingDienstTests.FakeSessie(_data);

    public ValueTask DisposeAsync() => _sessie.DisposeAsync();

    /// <summary>Voorblad 2025: 600 reserve, 1100 bank (balans), 4115 energie en 8020 contributie (W&amp;V), 4200 zonder T-rekening.</summary>
    public static Werkblad Voorblad()
    {
        var blad = new Werkblad();
        blad.Zet(1, 1, 2025.0);
        blad.Zet(3, 2, "Vermogen");
        blad.Zet(4, 1, 600.0); blad.Zet(4, 2, "Algemene Reserve"); blad.Zet(4, 10, 100.0);
        blad.Zet(5, 2, "Kas Bank");
        blad.Zet(6, 1, 1100.0); blad.Zet(6, 2, "Bank"); blad.Zet(6, 9, 150.0);
        blad.Zet(7, 2, "Kosten");
        blad.Zet(8, 1, 4115.0); blad.Zet(8, 2, "Energie"); blad.Zet(8, 7, 30.0);
        blad.Zet(9, 1, 4200.0); blad.Zet(9, 2, "Huur"); blad.Zet(9, 7, 20.0);
        blad.Zet(10, 2, "Contributie");
        blad.Zet(11, 1, 8020.0); blad.Zet(11, 2, "Contributie Welpen"); blad.Zet(11, 8, 80.0);
        blad.Zet(12, 3, 0.0, "=SUM(C3:C11)");
        return blad;
    }

    /// <summary>Twee tabbladen met T-rekeningen naast elkaar: per rekening datum, omschrijving, debet, credit.</summary>
    public static IReadOnlyList<GenoemdWerkblad> Bladen()
    {
        var kasbank = new Werkblad();
        // 1100 Bank in kolommen A-D: beginsaldo zonder datum, twee maandtotalen, een SUM-regel.
        kasbank.Zet(1, 2, "Kas Bank");
        kasbank.Zet(2, 1, 1100.0); kasbank.Zet(2, 2, "Bank"); kasbank.Zet(2, 3, "Debet"); kasbank.Zet(2, 4, "Credit");
        kasbank.Zet(3, 2, "Beginsaldo"); kasbank.Zet(3, 3, 100.0);
        kasbank.Zet(4, 1, new DateTime(2025, 1, 31)); kasbank.Zet(4, 2, "Januari"); kasbank.Zet(4, 3, 80.0);
        kasbank.Zet(5, 1, new DateTime(2025, 2, 28)); kasbank.Zet(5, 2, "Februari"); kasbank.Zet(5, 4, 30.0);
        kasbank.Zet(6, 3, 180.0, "=SUM(C3:C5)"); kasbank.Zet(6, 4, 30.0, "=SUM(D3:D5)");
        // 600 Reserve in kolommen G-J: een regel met een datum vóór 2025.
        kasbank.Zet(2, 7, 600.0); kasbank.Zet(2, 8, "Reserve"); kasbank.Zet(2, 9, "Debet"); kasbank.Zet(2, 10, "Credit");
        kasbank.Zet(3, 7, new DateTime(2019, 5, 1)); kasbank.Zet(3, 8, "Inbreng"); kasbank.Zet(3, 10, 100.0);

        var kosten = new Werkblad();
        kosten.Zet(2, 1, 4115.0); kosten.Zet(2, 2, "Energie"); kosten.Zet(2, 3, "Debet"); kosten.Zet(2, 4, "Credit");
        kosten.Zet(3, 1, new DateTime(2025, 3, 3)); kosten.Zet(3, 2, "Stroom"); kosten.Zet(3, 3, 25.0); // Voorblad zegt 30: afwijking
        kosten.Zet(2, 7, 8020.0); kosten.Zet(2, 8, "Contributie"); kosten.Zet(2, 9, "Debet"); kosten.Zet(2, 10, "Credit");
        kosten.Zet(3, 7, new DateTime(2025, 4, 1)); kosten.Zet(3, 8, "Incasso"); kosten.Zet(3, 10, 80.0);
        kosten.Zet(4, 7, new DateTime(2025, 4, 2)); kosten.Zet(4, 8, "Leeg"); // geen bedrag: overslaan
        kosten.Zet(2, 13, 9999.0); kosten.Zet(2, 14, "Diversen"); kosten.Zet(2, 15, "Debet"); kosten.Zet(2, 16, "Credit");
        kosten.Zet(3, 13, new DateTime(2025, 6, 1)); kosten.Zet(3, 14, "Iets"); kosten.Zet(3, 15, 5.0);

        return [new("Voorblad", Voorblad()), new("KasBank", kasbank), new("Kosten", kosten)];
    }

    [Fact]
    public void Inlezer_neemt_regels_over_zonder_sum_regels_en_vergelijkt_met_het_voorblad()
    {
        var voorstel = HistorieInlezer.Lees(Bladen());

        Assert.Equal(2025, voorstel.Jaar);
        var bank = voorstel.Rekeningen.Single(r => r.Nummer == 1100);
        Assert.Equal(3, bank.Regels.Count);
        Assert.Equal(HistorischeRegelSoort.Saldoregel, bank.Regels[0].Soort);
        Assert.Equal(new DateOnly(2025, 1, 1), bank.Regels[0].Datum);
        Assert.Equal(15000, bank.SaldoRegels);
        Assert.Equal(15000, bank.SaldoVoorblad);
        Assert.True(bank.Klopt);

        var reserve = voorstel.Rekeningen.Single(r => r.Nummer == 600);
        var inbreng = Assert.Single(reserve.Regels);
        Assert.Equal(new DateOnly(2025, 1, 1), inbreng.Datum);
        Assert.Equal("(01-05-2019) Inbreng", inbreng.Omschrijving);
        Assert.True(reserve.Klopt);

        var energie = voorstel.Rekeningen.Single(r => r.Nummer == 4115);
        Assert.False(energie.Klopt);
        Assert.Equal(2500, energie.SaldoRegels);
        Assert.Equal(3000, energie.SaldoVoorblad);
        Assert.Single(voorstel.Rekeningen.Single(r => r.Nummer == 8020).Regels);

        var huur = voorstel.Rekeningen.Single(r => r.Nummer == 4200);
        Assert.Empty(huur.Regels);
        Assert.False(huur.Klopt);
        var onbekend = voorstel.Rekeningen.Single(r => r.Nummer == 9999);
        Assert.False(onbekend.OpVoorblad);
        Assert.Equal("Diversen", onbekend.Naam);
        Assert.Contains(voorstel.Meldingen, m => m.Contains("9999", StringComparison.Ordinal));
    }

    [Fact]
    public void Inlezer_weigert_een_bestand_zonder_voorblad()
    {
        Assert.Throws<InvalidOperationException>(() => HistorieInlezer.Lees([new("Blad1", new Werkblad())]));
    }

    private async Task RichtInAsync() =>
        await new BoekhoudingDienst(_sessie, _tijd).ImporteerAsync(Ver, VoorbladInlezer.Lees(Voorblad()) with { }, new VerschilKeuze(1299, "Te onderzoeken verschillen"));

    [Fact]
    public async Task Import_maakt_een_historisch_boekjaar_dat_niet_meetelt_in_het_grootboek()
    {
        await RichtInAsync();
        var dienst = new HistorieDienst(_sessie, _tijd);
        var opening2026 = Grootboek.Saldo(_data.Stand, _data.Stand.Rekeningen.Single(r => r.Nummer == 1100), 2026);

        var aantal = await dienst.ImporteerAsync(Ver, HistorieInlezer.Lees(Bladen()), OnbekendeRekening.Overslaan);

        Assert.Equal(6, aantal); // 3 bank, 1 reserve, 1 energie, 1 contributie; 9999 overgeslagen
        var stand = _data.Stand;
        var jaar = stand.Boekjaar(2025)!;
        Assert.True(jaar.IsHistorisch);
        Assert.False(jaar.IsOpen);
        Assert.DoesNotContain(stand.Rekeningen, r => r.Nummer == 9999);
        Assert.Equal(5, stand.HistorischeTotalen.Count); // 600, 1100, 4115, 4200, 8020
        Assert.Equal(opening2026, Grootboek.Saldo(stand, stand.Rekeningen.Single(r => r.Nummer == 1100), 2026));
        Assert.Empty(Grootboek.GeboektePosten(stand, 2025));
        Assert.Equal(2, _sessie.Backups.Count); // inrichten én historie: elk vooraf een back-up
    }

    [Fact]
    public async Task Rapporten_van_2025_komen_uit_het_voorblad_en_de_kaart_uit_de_regels()
    {
        await RichtInAsync();
        await new HistorieDienst(_sessie, _tijd).ImporteerAsync(Ver, HistorieInlezer.Lees(Bladen()), OnbekendeRekening.Toevoegen);
        var stand = _data.Stand;
        var energie = stand.Rekeningen.Single(r => r.Nummer == 4115);

        Assert.Equal(3000, Grootboek.Saldo(stand, energie, 2025));
        var saldi = Grootboek.Saldibalans(stand, 2025);
        Assert.Equal(new Bedrag(3000), saldi.Regels.Single(r => r.Rekening.Nummer == 4115).Debet);
        var wenv = Grootboek.WinstEnVerlies(stand, 2025);
        Assert.Equal(new Bedrag(8000), wenv.TotaalBaten);
        Assert.Equal(new Bedrag(5000), wenv.TotaalLasten);
        var balans = Grootboek.Balans(stand, 2025);
        Assert.Equal(new Bedrag(15000), balans.TotaalActiva);

        var kaart = Grootboek.Kaart(stand, energie, 2025, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        Assert.True(kaart.IsHistorisch);
        Assert.Equal("Stroom", Assert.Single(kaart.Mutaties).Omschrijving);
        Assert.Equal(2500, kaart.Eindsaldo);
        Assert.Equal(3000, kaart.TotaalVolgensVoorblad);

        var nieuw = stand.Rekeningen.Single(r => r.Nummer == 9999);
        Assert.False(nieuw.IsActief);
        Assert.Null(Grootboek.Kaart(stand, nieuw, 2025, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)).TotaalVolgensVoorblad);
    }

    [Fact]
    public async Task Een_historisch_jaar_kan_maar_een_keer_en_niet_heropend_of_beboekt()
    {
        await RichtInAsync();
        var dienst = new HistorieDienst(_sessie, _tijd);
        await dienst.ImporteerAsync(Ver, HistorieInlezer.Lees(Bladen()), OnbekendeRekening.Overslaan);

        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.ImporteerAsync(Ver, HistorieInlezer.Lees(Bladen()), OnbekendeRekening.Overslaan));
        var boekhouding = new BoekhoudingDienst(_sessie, _tijd);
        await Assert.ThrowsAsync<InvalidOperationException>(() => boekhouding.HeropenBoekjaarAsync(Ver, 2025));
        var stand = _data.Stand;
        var memoriaal = stand.Dagboeken.Single(d => d.Code == "MEM");
        await Assert.ThrowsAsync<InvalidOperationException>(() => boekhouding.BewaarConceptAsync(Ver, new PostInvoer(null, memoriaal.Id, new DateOnly(2025, 5, 1), "x",
            [new RegelInvoerId(stand.Rekeningen.Single(r => r.Nummer == 4115).Id, null, new Bedrag(100), Bedrag.Nul),
             new RegelInvoerId(stand.Rekeningen.Single(r => r.Nummer == 1100).Id, null, Bedrag.Nul, new Bedrag(100))])));
    }

    [Fact]
    public async Task Een_boekhouding_zonder_rekeningschema_kan_geen_historie_krijgen()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new HistorieDienst(_sessie, _tijd).ImporteerAsync(Ver, HistorieInlezer.Lees(Bladen()), OnbekendeRekening.Overslaan));
    }
}
