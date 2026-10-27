using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Tests.Boekhouding;

/// <summary>Standaardinrichting en sjablonen (besluiten 114–120).</summary>
public sealed class RekeningsjabloonTests
{
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private const AdministratieSoort Sti = AdministratieSoort.Stichting;
    private static readonly int[] VasteRekeningen = [1100, 1150, 1300, 1310, 1400, 1600, 1700, 4000, 8400];

    private static string Minimaal(string rekeningen, string formaat = "verenigd-rekeningschema", int versie = 1, string rubrieken = """[{"naam":"Vermogen"}]""") =>
        $$"""{"formaat":"{{formaat}}","versie":{{versie}},"naam":"Test","rubrieken":{{rubrieken}},"rekeningen":[{{rekeningen}}]}""";

    private const string Reserve = """{"nummer":600,"naam":"Algemene reserve","rubriek":"Vermogen","soort":"Balans","rol":"Reserve"}""";

    [Fact]
    public void Standaardschema_vereniging_bevat_de_vaste_rekeningen_en_speltakken()
    {
        var sjabloon = Rekeningsjablonen.Standaard(Ver);

        Assert.Equal(Ver, sjabloon.Administratie);
        Assert.Equal(RekeningRol.Reserve, sjabloon.Rekeningen.Single(r => r.Nummer == 600).Rol);
        Assert.Equal(RekeningRol.Kas, sjabloon.Rekeningen.Single(r => r.Nummer == 1000).Rol);
        Assert.Equal(RekeningRol.Kruisposten, sjabloon.Rekeningen.Single(r => r.Nummer == 1298).Rol);
        Assert.All(VasteRekeningen, n => Assert.Contains(sjabloon.Rekeningen, r => r.Nummer == n));
        Assert.Equal(["Bevers", "Welpen", "Scouts", "Explorers", "Roverscouts"], sjabloon.Speltakken!.Standaard);
        Assert.Equal("Vermogen", sjabloon.Rubrieken[0].Naam);
        Assert.InRange(sjabloon.Rekeningen.Count, 20, 35);
    }

    [Fact]
    public void Standaardschema_stichting_heeft_gebouw_en_afschrijvingen_maar_geen_kas_of_contributie()
    {
        var sjabloon = Rekeningsjablonen.Standaard(Sti);

        Assert.Equal(Sti, sjabloon.Administratie);
        Assert.Contains(sjabloon.Rekeningen, r => r.Nummer == 200 && r.Soort == RekeningSoort.Balans);
        Assert.Contains(sjabloon.Rekeningen, r => r.Nummer == 4400 && r.Soort == RekeningSoort.Resultaat);
        Assert.Contains(sjabloon.Rekeningen, r => r.Nummer == 8100);
        Assert.DoesNotContain(sjabloon.Rekeningen, r => r.Rol == RekeningRol.Kas);
        Assert.Null(sjabloon.Speltakken);
    }

    [Fact]
    public void Speltakken_worden_contributierekeningen_in_de_volgorde_van_het_sjabloon()
    {
        var rekeningen = Rekeningsjablonen.Rekeningen(Rekeningsjablonen.Standaard(Ver), ["Welpen", " Scouts ", "welpen", ""]);

        var contributie = rekeningen.Where(r => r.Rubriek == "Contributie").ToList();
        Assert.Equal([(8010, "Contributie Welpen"), (8020, "Contributie Scouts")], contributie.Select(r => (r.Nummer, r.Naam)));
        Assert.All(contributie, r => Assert.Equal(RekeningSoort.Resultaat, r.Soort));
        Assert.Equal("Vermogen", rekeningen[0].Rubriek);
        Assert.All(rekeningen, r => Assert.Equal(Bedrag.Nul, r.Debet));
        Assert.Equal(rekeningen.Count, rekeningen.Select(r => r.Nummer).Distinct().Count());
    }

    [Fact]
    public void Schrijven_en_lezen_levert_hetzelfde_sjabloon()
    {
        foreach (var soort in new[] { Ver, Sti })
        {
            var sjabloon = Rekeningsjablonen.Standaard(soort);
            var tekst = Rekeningsjablonen.Schrijf(sjabloon);

            Assert.Contains("\"formaat\": \"verenigd-rekeningschema\"", tekst, StringComparison.Ordinal);
            Assert.Equal(tekst, Rekeningsjablonen.Schrijf(Rekeningsjablonen.Lees(tekst)));
        }
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
        var rekeningen = Rekeningsjablonen.Rekeningen(Rekeningsjablonen.Standaard(Ver), ["Welpen"])
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
        Assert.Contains("standaardinrichting", opening.Omschrijving, StringComparison.Ordinal);
        Assert.Equal(120000, Grootboek.Saldo(stand, stand.Rekeningen.Single(r => r.Nummer == 1100), 2026));
        Assert.Equal("Vermogen", stand.Rubrieken.OrderBy(r => r.Volgorde).First().Naam);

        var export = Rekeningsjablonen.Lees(Rekeningsjablonen.Schrijf(Rekeningsjablonen.UitStand(stand, "Onze groep")));
        Assert.Equal("Onze groep", export.Naam);
        Assert.Equal(Ver, export.Administratie);
        Assert.Null(export.Speltakken);
        Assert.Equal(stand.Rekeningen.Select(r => r.Nummer).Order(), export.Rekeningen.Select(r => r.Nummer).Order());
        Assert.Equal("Contributie Welpen", export.Rekeningen.Single(r => r.Nummer == 8010).Naam);
        Assert.Equal(stand.Rubrieken.Count, export.Rubrieken.Count);
    }

    [Fact]
    public void Voorstel_zonder_beginsaldi_maakt_geen_openingspost_nodig()
    {
        var voorstel = Rekeningsjablonen.Voorstel(Rekeningsjablonen.Rekeningen(Rekeningsjablonen.Standaard(Sti), []), 2027);

        Assert.Equal(2027, voorstel.Beginjaar);
        Assert.True(voorstel.IsInEvenwicht);
        Assert.True(voorstel.MetVerkoopboek);
        Assert.Empty(voorstel.Meldingen);
    }
}
