using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Tests.Boekhouding;

/// <summary>Jaarrekeningindeling (besluiten 263–268). Alle bedragen zijn verzonnen.</summary>
public sealed class JaarrekeningTests
{
    private static readonly DateTime Nu = new(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc);
    private readonly BoekhoudingTestdata _data = new();
    private readonly Rubriek _acties;
    private readonly Grootboekrekening _kampbijdrage;
    private readonly Grootboekrekening _kampkosten;

    public JaarrekeningTests()
    {
        _acties = _data.Rubriek("Activiteiten");
        _kampbijdrage = _data.Rekening(8400, "Kampbijdragen", RekeningSoort.Resultaat, _acties);
        _kampkosten = _data.Rekening(4100, "Kampkosten", RekeningSoort.Resultaat, _acties);
        _data.Boekjaren.Add(new Boekjaar(BoekhoudingTestdata.Ver, 2025));
    }

    private void Boekingen()
    {
        _data.Geboekt(new DateOnly(2025, 1, 1), "Opening 2025", PostSoort.Openingsbalans, (_data.Bank, 500000, 0), (_data.Reserve, 0, 500000));
        _data.Geboekt(new DateOnly(2025, 3, 1), "Contributie 2025", (_data.Bank, 90000, 0), (_data.Welpen, 0, 90000));
        _data.Geboekt(new DateOnly(2026, 1, 1), "Opening 2026", PostSoort.Openingsbalans, (_data.Bank, 590000, 0), (_data.Reserve, 0, 590000));
        _data.Geboekt(new DateOnly(2026, 3, 1), "Contributie", (_data.Bank, 109900, 0), (_data.Welpen, 0, 109900));
        _data.Geboekt(new DateOnly(2026, 4, 1), "Energie", (_data.Energie, 30000, 0), (_data.Bank, 0, 30000));
        _data.Geboekt(new DateOnly(2026, 7, 1), "Kamp", (_data.Bank, 250000, 0), (_data.Kampbijdrage(), 0, 250000));
        _data.Geboekt(new DateOnly(2026, 7, 9), "Kamp", (_kampkosten, 180000, 0), (_data.Kas, 0, 180000));
    }

    [Fact]
    public void Automatische_indeling_geeft_hetzelfde_resultaat_als_de_staat_van_baten_en_lasten()
    {
        Boekingen();

        var jr = Jaarrekeningrekenaar.Bereken(_data.Stand, JaarrekeningRapport.BatenEnLasten, 2026);

        Assert.True(jr.IsAutomatisch);
        Assert.Equal(Grootboek.WinstEnVerlies(_data.Stand, 2026).Resultaat, jr.Resultaat(d => d.Werkelijk));
        Assert.Equal(["Contributie", "Activiteiten"], jr.Deel(JaarrekeningAfdeling.Baten).Regels.Select(r => r.Tekst));
        Assert.Equal(["Terreinkosten", "Activiteiten"], jr.Deel(JaarrekeningAfdeling.Lasten).Regels.Select(r => r.Tekst));
        Assert.Equal(new Bedrag(90000), jr.Deel(JaarrekeningAfdeling.Baten).VorigJaar);
        Assert.Empty(jr.NietIngedeeld);
    }

    [Fact]
    public void Automatische_balans_heeft_dezelfde_totalen_als_de_balans_met_het_resultaat_bij_de_passiva()
    {
        Boekingen();

        var jr = Jaarrekeningrekenaar.Bereken(_data.Stand, JaarrekeningRapport.Balans, 2026);
        var balans = Grootboek.Balans(_data.Stand, 2026);

        Assert.Equal(balans.TotaalActiva, jr.Deel(JaarrekeningAfdeling.Activa).Werkelijk);
        Assert.Equal(balans.TotaalPassiva, jr.Deel(JaarrekeningAfdeling.Passiva).Werkelijk);
        Assert.Equal(jr.Deel(JaarrekeningAfdeling.Activa).Werkelijk, jr.Deel(JaarrekeningAfdeling.Passiva).Werkelijk);
        var resultaat = jr.Deel(JaarrekeningAfdeling.Passiva).Regels.Single(r => r.Tekst == Jaarrekeningrekenaar.ResultaatLopendJaarTekst);
        Assert.Equal(new Bedrag(balans.ResultaatLopendJaar), resultaat.Werkelijk);
        Assert.Equal(new Bedrag(90000), resultaat.VorigJaar);
        Assert.Equal(Grootboek.Balans(_data.Stand, 2025).TotaalActiva, jr.Deel(JaarrekeningAfdeling.Activa).VorigJaar);
    }

    [Fact]
    public void Kas_met_creditsaldo_staat_bij_de_passiva()
    {
        Boekingen();

        var jr = Jaarrekeningrekenaar.Bereken(_data.Stand, JaarrekeningRapport.Balans, 2026);

        var passiva = jr.Deel(JaarrekeningAfdeling.Passiva).Regels.SelectMany(r => r.Rekeningen);
        Assert.Contains(passiva, r => r.Rekening == _data.Kas && r.Werkelijk == new Bedrag(180000));
        Assert.DoesNotContain(jr.Deel(JaarrekeningAfdeling.Activa).Regels.SelectMany(r => r.Rekeningen), r => r.Rekening == _data.Kas);
    }

    [Fact]
    public void Eigen_indeling_met_kop_regels_en_subtotaal()
    {
        Boekingen();
        var contributie = _data.Rubrieken.Single(r => r.Naam == "Contributie");
        var terrein = _data.Rubrieken.Single(r => r.Naam == "Terreinkosten");
        var indeling = new Jaarrekeningindeling(BoekhoudingTestdata.Ver, JaarrekeningRapport.BatenEnLasten);
        indeling.Vervang(
        [
            new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Regel, "Opbrengsten", [contributie, _acties]),
            new(JaarrekeningAfdeling.Lasten, IndelingsregelSoort.Kop, "Uitgaven", []),
            new(JaarrekeningAfdeling.Lasten, IndelingsregelSoort.Regel, "Terrein", [terrein]),
            new(JaarrekeningAfdeling.Lasten, IndelingsregelSoort.Regel, "Kampen", [_acties]),
            new(JaarrekeningAfdeling.Lasten, IndelingsregelSoort.Subtotaal, "Subtotaal uitgaven", []),
        ]);
        _data.Jaarrekeningindelingen.Add(indeling);

        var jr = Jaarrekeningrekenaar.Bereken(_data.Stand, JaarrekeningRapport.BatenEnLasten, 2026);

        Assert.False(jr.IsAutomatisch);
        var baten = Assert.Single(jr.Deel(JaarrekeningAfdeling.Baten).Regels);
        Assert.Equal(new Bedrag(109900 + 250000), baten.Werkelijk);
        Assert.Equal([_data.Welpen, _data.Kampbijdrage()], baten.Rekeningen.Select(r => r.Rekening));
        var lasten = jr.Deel(JaarrekeningAfdeling.Lasten).Regels;
        Assert.Equal([IndelingsregelSoort.Kop, IndelingsregelSoort.Regel, IndelingsregelSoort.Regel, IndelingsregelSoort.Subtotaal], lasten.Select(r => r.Soort));
        Assert.Equal(new Bedrag(30000 + 180000), lasten[^1].Werkelijk);
        Assert.Equal(Grootboek.WinstEnVerlies(_data.Stand, 2026).Resultaat, jr.Resultaat(d => d.Werkelijk));
    }

    [Fact]
    public void Niet_ingedeelde_rekeningen_worden_gemeld_en_tellen_mee_in_het_totaal()
    {
        Boekingen();
        var indeling = new Jaarrekeningindeling(BoekhoudingTestdata.Ver, JaarrekeningRapport.BatenEnLasten);
        indeling.Vervang([new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Regel, "Contributie", [_data.Rubrieken.Single(r => r.Naam == "Contributie")])]);
        _data.Jaarrekeningindelingen.Add(indeling);

        var jr = Jaarrekeningrekenaar.Bereken(_data.Stand, JaarrekeningRapport.BatenEnLasten, 2026);

        Assert.Equal([_kampkosten, _data.Energie, _data.Kampbijdrage()], jr.NietIngedeeld);
        var los = jr.Deel(JaarrekeningAfdeling.Baten).Regels.Single(r => r.IsNietIngedeeld);
        Assert.Equal(Jaarrekeningrekenaar.NietIngedeeldTekst, los.Tekst);
        Assert.Equal(Grootboek.WinstEnVerlies(_data.Stand, 2026).Resultaat, jr.Resultaat(d => d.Werkelijk));
    }

    [Fact]
    public void Concepten_tellen_niet_mee()
    {
        Boekingen();
        var concept = new Journaalpost(BoekhoudingTestdata.Ver, _data.Memoriaal, new DateOnly(2026, 5, 1), "concept");
        concept.Wijzig(_data.Memoriaal, concept.Datum, "concept",
            [new RegelInvoer(_data.Energie, null, new Bedrag(999), Bedrag.Nul), new RegelInvoer(_data.Bank, null, Bedrag.Nul, new Bedrag(999))]);
        _data.Posten.Add(concept);

        var jr = Jaarrekeningrekenaar.Bereken(_data.Stand, JaarrekeningRapport.BatenEnLasten, 2026);

        Assert.Equal(new Bedrag(30000), jr.Deel(JaarrekeningAfdeling.Lasten).Regels.Single(r => r.Tekst == "Terreinkosten").Werkelijk);
    }

    [Fact]
    public void Rapport_heeft_kolommen_dit_jaar_en_vorig_jaar_met_of_zonder_rekeningen()
    {
        Boekingen();
        var zonder = Rapporten.Jaarrekening(Jaarrekeningrekenaar.Bereken(_data.Stand, JaarrekeningRapport.BatenEnLasten, 2026), "Vereniging", metRekeningen: false);
        Assert.Equal(["Omschrijving", "Werkelijk 2026", "Werkelijk 2025"], zonder.Kolommen.Select(k => k.Naam));

        var met = Rapporten.Jaarrekening(Jaarrekeningrekenaar.Bereken(_data.Stand, JaarrekeningRapport.BatenEnLasten, 2026), "Vereniging", metRekeningen: true);
        Assert.Equal(["Omschrijving", "Werkelijk 2026", "Werkelijk 2025"], met.Kolommen.Select(k => k.Naam));
        Assert.True(met.Rijen.Count > zonder.Rijen.Count);
        Assert.Contains(met.Rijen, r => (string)r.Cellen[0]! == "Resultaat (baten min lasten)");
        Assert.All(met.Rijen, r => Assert.Equal(3, r.Cellen.Count));

        var balans = Rapporten.Jaarrekening(Jaarrekeningrekenaar.Bereken(_data.Stand, JaarrekeningRapport.Balans, 2026), "Vereniging", metRekeningen: true);
        Assert.Equal(["Omschrijving", "31-12-2026", "31-12-2025"], balans.Kolommen.Select(k => k.Naam));
        Assert.Equal("Balans per 31-12-2026", balans.Titel);
    }

    [Fact]
    public void Indeling_weigert_ongeldige_regels_en_blijft_dan_ongewijzigd()
    {
        var contributie = _data.Rubrieken.Single(r => r.Naam == "Contributie");
        var indeling = new Jaarrekeningindeling(BoekhoudingTestdata.Ver, JaarrekeningRapport.BatenEnLasten);
        indeling.Vervang([new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Regel, "Contributie", [contributie])]);

        Assert.Throws<ArgumentException>(() => indeling.Vervang(
            [new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Regel, "A", [contributie]), new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Regel, "B", [contributie])]));
        Assert.Throws<ArgumentException>(() => indeling.Vervang([new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Kop, "Kop", [contributie])]));
        Assert.Throws<ArgumentException>(() => indeling.Vervang([new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Regel, "Leeg", [])]));
        Assert.Throws<ArgumentException>(() => indeling.Vervang([new(JaarrekeningAfdeling.Activa, IndelingsregelSoort.Kop, "Activa", [])]));
        Assert.Throws<ArgumentException>(() => indeling.Vervang([new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Kop, "  ", [])]));
        Assert.Throws<ArgumentException>(() => indeling.Vervang(
            [new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Regel, "Ander", [new Rubriek((AdministratieSoort)2, "Gebouw", 1)])]));
        Assert.Equal("Contributie", Assert.Single(indeling.Regels).Tekst);

        // Dezelfde rubriek bij baten én lasten mag (bijvoorbeeld Activiteiten).
        indeling.Vervang([new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Regel, "Kamp", [_acties]), new(JaarrekeningAfdeling.Lasten, IndelingsregelSoort.Regel, "Kamp", [_acties])]);
        Assert.Equal(2, indeling.Regels.Count);
    }

    [Fact]
    public async Task Dienst_bewaart_zet_terug_en_neemt_een_sjabloon_over()
    {
        Boekingen();
        var dienst = new JaarrekeningDienst(new BoekhoudingDienstTests.FakeSessie(_data));
        var contributie = _data.Rubrieken.Single(r => r.Naam == "Contributie");

        await dienst.BewaarAsync(BoekhoudingTestdata.Ver, JaarrekeningRapport.BatenEnLasten,
            [new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Regel, "Contributies", [contributie.Id])]);
        Assert.False(Jaarrekeningrekenaar.Bereken(_data.Stand, JaarrekeningRapport.BatenEnLasten, 2026).IsAutomatisch);
        await Assert.ThrowsAsync<ArgumentException>(() => dienst.BewaarAsync(BoekhoudingTestdata.Ver, JaarrekeningRapport.BatenEnLasten,
            [new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Regel, "Onbekend", [9999])]));

        var sjabloon = JaarrekeningDienst.NaarSjabloon(_data.Stand);
        Assert.Equal("Contributie", Assert.Single(Assert.Single(sjabloon).Rubrieken));

        await dienst.BewaarAsync(BoekhoudingTestdata.Ver, JaarrekeningRapport.BatenEnLasten, []);
        Assert.True(Jaarrekeningrekenaar.Bereken(_data.Stand, JaarrekeningRapport.BatenEnLasten, 2026).IsAutomatisch);

        await dienst.PasSjabloonToeAsync(BoekhoudingTestdata.Ver,
        [
            .. sjabloon,
            new(JaarrekeningRapport.BatenEnLasten, JaarrekeningAfdeling.Lasten, IndelingsregelSoort.Regel, "Bestaat niet", ["Onbekende rubriek"]),
        ]);
        var regels = _data.Jaarrekeningindelingen.Single().Regels;
        Assert.Equal("Contributies", Assert.Single(regels).Tekst);
    }

    [Fact]
    public void Sjabloonbestand_neemt_de_indeling_mee_heen_en_terug()
    {
        var contributie = _data.Rubrieken.Single(r => r.Naam == "Contributie");
        var indeling = new Jaarrekeningindeling(BoekhoudingTestdata.Ver, JaarrekeningRapport.BatenEnLasten);
        indeling.Vervang(
        [
            new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Kop, "Opbrengsten", []),
            new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Regel, "Contributies", [contributie, _acties]),
        ]);
        _data.Jaarrekeningindelingen.Add(indeling);

        var tekst = Rekeningsjablonen.Schrijf(Rekeningsjablonen.UitStand(_data.Stand, "Test"));
        var terug = Rekeningsjablonen.Lees(tekst);

        Assert.Equal(2, terug.Jaarrekening.Count);
        Assert.Equal(["Contributie", "Activiteiten"], terug.Jaarrekening[1].Rubrieken);
        Assert.Contains("\"jaarrekening\"", tekst, StringComparison.Ordinal);
        var fout = terug with
        {
            Jaarrekening = [new(JaarrekeningRapport.BatenEnLasten, JaarrekeningAfdeling.Baten, IndelingsregelSoort.Regel, "X", ["Bestaat niet"])],
        };
        Assert.Throws<InvalidOperationException>(() => Rekeningsjablonen.Lees(Rekeningsjablonen.Schrijf(fout)));
    }

    [Fact]
    public void Automatische_indeling_als_begin_van_de_editor()
    {
        var regels = Jaarrekeningrekenaar.AutomatischeIndeling(_data.Stand, JaarrekeningRapport.Balans);

        Assert.All(regels, r => Assert.Equal(IndelingsregelSoort.Regel, r.Soort));
        Assert.Equal(["Vermogen", "Kas Bank", "Vermogen", "Kas Bank"], regels.Select(r => r.Tekst));
        var indeling = new Jaarrekeningindeling(BoekhoudingTestdata.Ver, JaarrekeningRapport.Balans);
        indeling.Vervang(regels);
        Assert.Equal(4, indeling.Regels.Count);
    }
}

internal static class JaarrekeningTestdata
{
    public static Grootboekrekening Kampbijdrage(this BoekhoudingTestdata data) => data.Rekeningen.Single(r => r.Nummer == 8400);
}
