using BtwBoeker.Application.Bijlagen;
using BtwBoeker.Domain.Financien;

namespace BtwBoeker.Application.Tests.Bijlagen;

/// <summary>Datum en bedrag uit de tekst van een PDF. Alle teksten zijn verzonnen.</summary>
public sealed class BonHerkenningTests
{
    [Fact]
    public void Kassabon_van_een_supermarkt()
    {
        const string tekst = """
            SUPERMARKT DE HOEK
            Kassabon
            12-10-2026 14:32
            Brood            2,49
            Melk             1,19
            SUBTOTAAL        3,68
            TOTAAL           3,68
            PIN              3,68
            """;

        Assert.Equal(new BonGegevens(new DateOnly(2026, 10, 12), new Bedrag(368)), BonHerkenning.Herken(tekst));
    }

    [Fact]
    public void Bouwmarkt_met_te_betalen_en_duizendtallen()
    {
        const string tekst = """
            Bouwmarkt Voorbeeld B.V.
            Datum: 03/09/2026
            Hout 2x4         1.199,00
            Schroeven           35,50
            Te betalen     € 1.234,50
            """;

        Assert.Equal(new BonGegevens(new DateOnly(2026, 9, 3), new Bedrag(123450)), BonHerkenning.Herken(tekst));
    }

    [Fact]
    public void Factuur_met_btw_kiest_het_totaal_te_betalen_en_de_eerste_datum()
    {
        const string tekst = """
            FACTUUR 2026-0417
            Factuurdatum 1 oktober 2026
            Vervaldatum 15 oktober 2026
            Huur materiaal     100,00
            Totaal excl. btw   100,00
            BTW 21%             21,00
            Totaal te betalen € 121,00
            """;

        Assert.Equal(new BonGegevens(new DateOnly(2026, 10, 1), new Bedrag(12100)), BonHerkenning.Herken(tekst));
    }

    [Fact]
    public void Creditnota_houdt_het_minteken()
    {
        const string tekst = """
            Creditnota
            Datum 05.10.26
            Totaal -15,00
            """;

        Assert.Equal(new BonGegevens(new DateOnly(2026, 10, 5), new Bedrag(-1500)), BonHerkenning.Herken(tekst));
    }

    [Theory]
    [InlineData("Totaal € -15,00", -1500)]
    [InlineData("Totaal -€ 15,00", -1500)]
    [InlineData("Totaal - 15,00", -1500)]
    [InlineData("Totaal 15,00-", -1500)]
    [InlineData("Te betalen -1.234,50", -123450)]
    [InlineData("Totaal 15,00", 1500)]
    public void Minteken_voor_of_achter_het_bedrag(string tekst, long centen)
    {
        Assert.Equal(new Bedrag(centen), BonHerkenning.Herken(tekst).Bedrag);
    }

    [Fact]
    public void Retourbon_zonder_totaalregel_kiest_het_grootste_bedrag_met_minteken()
    {
        Assert.Equal(new Bedrag(-2000), BonHerkenning.Herken("Retour\nArtikel A -5,00\nArtikel B -20,00").Bedrag);
    }

    [Theory]
    [InlineData("-15,00", true, -1500)]
    [InlineData("€ -15,00", true, -1500)]
    [InlineData("15,00-", true, -1500)]
    [InlineData("23,45", true, 2345)]
    [InlineData("abc", false, 0)]
    [InlineData("", false, 0)]
    public void Bonbedrag_invoer_mag_negatief_zijn(string invoer, bool geldig, long centen)
    {
        Assert.Equal(geldig, BonHerkenning.TryParseBedrag(invoer, out var bedrag));
        Assert.Equal(new Bedrag(centen), bedrag);
    }

    [Fact]
    public void Zonder_totaalregel_wordt_het_hoogste_bedrag_gekozen()
    {
        const string tekst = """
            Webshop order 2026-10-07
            Artikel A 4,50
            Artikel B 12,00
            Verzending 7.25
            """;

        Assert.Equal(new BonGegevens(new DateOnly(2026, 10, 7), new Bedrag(1200)), BonHerkenning.Herken(tekst));
    }

    [Theory]
    [InlineData("Bedankt voor uw bezoek")]
    [InlineData("")]
    [InlineData(null)]
    public void Zonder_datum_en_bedrag_blijft_alles_leeg(string? tekst)
    {
        Assert.Equal(new BonGegevens(null, null), BonHerkenning.Herken(tekst));
    }

    [Fact]
    public void Een_ongeldige_datum_wordt_overgeslagen()
    {
        Assert.Equal(new DateOnly(2026, 2, 2), BonHerkenning.Herken("31-02-2026 en 02-02-2026 totaal 1,00").Datum);
    }
}
