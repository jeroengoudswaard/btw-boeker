using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Tests.Boekhouding;

/// <summary>Rekeningschema-sjablonen: lezen, schrijven, inrichten en exporteren.</summary>
public sealed class RekeningsjabloonTests
{
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;

    private static string Minimaal(string rekeningen, string formaat = "btw-boeker-rekeningschema", int versie = 1, string rubrieken = """[{"naam":"Vermogen"}]""") =>
        $$"""{"formaat":"{{formaat}}","versie":{{versie}},"naam":"Test","rubrieken":{{rubrieken}},"rekeningen":[{{rekeningen}}]}""";

    private const string Reserve = """{"nummer":600,"naam":"Algemene reserve","rubriek":"Vermogen","soort":"Balans","rol":"Reserve"}""";

    [Fact]
    public void Rekeningen_volgen_de_volgorde_van_de_rubrieken_met_nul_bedragen()
    {
        var sjabloon = Testsjabloon.Verhuur() with { Rubrieken = [new("Opbrengsten"), new("Kosten"), new("Liquide middelen"), new("Vermogen")] };

        var rekeningen = Rekeningsjablonen.Rekeningen(sjabloon);

        Assert.Equal([8000, 4100, 4120, 1000, 1100, 600], rekeningen.Select(r => r.Nummer));
        Assert.All(rekeningen, r => Assert.Equal(Bedrag.Nul, r.Debet));
        Assert.Equal(ResultaatKant.Baten, rekeningen[0].VoorgesteldeKant);
    }

    [Fact]
    public void Schrijven_en_lezen_levert_hetzelfde_sjabloon()
    {
        var tekst = Rekeningsjablonen.Schrijf(Testsjabloon.Verhuur());

        Assert.Contains("\"formaat\": \"btw-boeker-rekeningschema\"", tekst, StringComparison.Ordinal);
        Assert.Equal(tekst, Rekeningsjablonen.Schrijf(Rekeningsjablonen.Lees(tekst)));
    }

    [Theory]
    [InlineData("geen json", "geen geldig")]
    [InlineData("""{"formaat":"iets anders","versie":1}""", "geen rekeningschema")]
    public void Lees_weigert_een_ander_bestand(string tekst, string melding)
    {
        var fout = Assert.Throws<InvalidOperationException>(() => Rekeningsjablonen.Lees(tekst));
        Assert.Contains(melding, fout.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Lees_weigert_ongeldige_inhoud()
    {
        string Fout(string json) => Assert.Throws<InvalidOperationException>(() => Rekeningsjablonen.Lees(json)).Message;

        Assert.Contains("nieuwere versie", Fout(Minimaal(Reserve, versie: 2)), StringComparison.Ordinal);
        Assert.Contains("600", Fout(Minimaal(Reserve + "," + Reserve.Replace("Algemene reserve", "Dubbel", StringComparison.Ordinal))), StringComparison.Ordinal);
        Assert.Contains("reserve", Fout(Minimaal("""{"nummer":1100,"naam":"Bank","rubriek":"Vermogen","soort":"Balans","rol":"Geen"}""")), StringComparison.Ordinal);
        Assert.Contains("Onbekend", Fout(Minimaal(Reserve.Replace("\"Vermogen\"", "\"Onbekend\"", StringComparison.Ordinal))), StringComparison.Ordinal);
        Assert.Contains("nummer", Fout(Minimaal(Reserve.Replace("600", "0", StringComparison.Ordinal))), StringComparison.Ordinal);
        Assert.Contains("naam", Fout(Minimaal(Reserve.Replace("Algemene reserve", " ", StringComparison.Ordinal))), StringComparison.Ordinal);
    }

    [Fact]
    public void Een_minimaal_sjabloon_zonder_rubriek_bij_een_rekening_is_geldig()
    {
        var sjabloon = Rekeningsjablonen.Lees(Minimaal(Reserve + "," + """{"nummer":1100,"naam":"Bank","soort":"Balans"}"""));

        Assert.Null(sjabloon.Administratie);
        Assert.Null(sjabloon.Rekeningen.Single(r => r.Nummer == 1100).Rubriek);
        Assert.Equal(RekeningRol.Geen, sjabloon.Rekeningen.Single(r => r.Nummer == 1100).Rol);
    }

    [Fact]
    public async Task Inrichten_met_beginsaldi_en_terug_exporteren_als_sjabloon()
    {
        var data = new BoekhoudingTestdata(ingericht: false);
        await using var sessie = new BoekhoudingDienstTests.FakeSessie(data);
        var dienst = new BoekhoudingDienst(sessie, new BoekhoudingDienstTests.VasteTijd(new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero)));
        var rekeningen = Rekeningsjablonen.Rekeningen(Testsjabloon.Verhuur())
            .Select(r => r.Nummer switch
            {
                1100 => r with { Debet = new Bedrag(120000) },
                600 => r with { Credit = new Bedrag(120000) },
                4120 => r with { Overslaan = true },
                _ => r,
            }).ToList();

        await dienst.ImporteerAsync(Ver, Rekeningsjablonen.Voorstel(rekeningen, 2026));

        var stand = await dienst.LaadAsync(Ver);
        Assert.Equal(rekeningen.Count - 1, stand.Rekeningen.Count);
        Assert.Equal(["MEM", "KAS", "VRK"], stand.Dagboeken.OrderBy(d => d.Soort).Select(d => d.Code));
        Assert.Equal(2026, Assert.Single(stand.Boekjaren).Jaar);
        var opening = Assert.Single(stand.Posten);
        Assert.Contains("sjabloon", opening.Omschrijving, StringComparison.Ordinal);
        Assert.Equal(120000, Grootboek.Saldo(stand, stand.Rekeningen.Single(r => r.Nummer == 1100), 2026));
        Assert.Equal("Vermogen", stand.Rubrieken.OrderBy(r => r.Volgorde).First().Naam);

        var export = Rekeningsjablonen.Lees(Rekeningsjablonen.Schrijf(Rekeningsjablonen.UitStand(stand, "Onze groep")));
        Assert.Equal("Onze groep", export.Naam);
        Assert.Equal(Ver, export.Administratie);
        Assert.Equal(stand.Rekeningen.Select(r => r.Nummer).Order(), export.Rekeningen.Select(r => r.Nummer).Order());
        Assert.Equal(ResultaatKant.Baten, export.Rekeningen.Single(r => r.Nummer == 8000).Kant);
        Assert.Equal(stand.Rubrieken.Count, export.Rubrieken.Count);
    }

    [Fact]
    public void Voorstel_zonder_beginsaldi_maakt_geen_openingspost_nodig()
    {
        var voorstel = Rekeningsjablonen.Voorstel(Rekeningsjablonen.Rekeningen(Testsjabloon.Verhuur()), 2027);

        Assert.Equal(2027, voorstel.Beginjaar);
        Assert.True(voorstel.IsInEvenwicht);
        Assert.True(voorstel.MetVerkoopboek);
        Assert.Empty(voorstel.Meldingen);
    }
}
