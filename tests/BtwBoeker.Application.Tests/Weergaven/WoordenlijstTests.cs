using BtwBoeker.Application.Weergaven;

namespace BtwBoeker.Application.Tests.Weergaven;

/// <summary>De woordenlijst voor de weergaven Expert en Eenvoudig.</summary>
public sealed class WoordenlijstTests
{
    [Fact]
    public void Elke_term_heeft_een_unieke_sleutel_en_beide_teksten()
    {
        Assert.Equal(Woordenlijst.Termen.Count, Woordenlijst.Termen.Select(t => t.Sleutel).Distinct(StringComparer.Ordinal).Count());
        Assert.All(Woordenlijst.Termen, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Expert), t.Sleutel);
            Assert.False(string.IsNullOrWhiteSpace(t.Eenvoudig), t.Sleutel);
        });
    }

    [Theory]
    [InlineData("Boekhouding", "Boekhouding", "Financiën")]
    [InlineData("Journaal", "Journaal", "Alle boekingen")]
    [InlineData("Bank", "Bank", "Banktransacties")]
    [InlineData("Rekeningschema", "Rekeningschema", "Potjes")]
    [InlineData("Grootboekkaart", "Grootboekkaart", "Inhoud van een potje")]
    [InlineData("Saldibalans", "Saldibalans", "Stand van alle potjes")]
    [InlineData("Balans", "Balans", "Bezittingen en schulden")]
    [InlineData("WinstEnVerlies", "Staat van baten en lasten", "Inkomsten en uitgaven")]
    [InlineData("Concept", "Concept", "Nog niet definitief")]
    [InlineData("Geboekt", "Geboekt", "Definitief")]
    public void Woordenlijst_volgt_besluit_128(string sleutel, string expert, string eenvoudig)
    {
        var term = Woordenlijst.Zoek(sleutel);

        Assert.Equal(expert, Zonder(term.Expert));
        Assert.Equal(eenvoudig, Zonder(term.Eenvoudig));
    }

    [Fact]
    public void Onbekende_sleutel_geeft_een_duidelijke_fout()
    {
        var fout = Assert.Throws<KeyNotFoundException>(() => Woordenlijst.Zoek("Bestaat niet"));
        Assert.Contains("Bestaat niet", fout.Message, StringComparison.Ordinal);
    }

    private static string Zonder(string tekst) => tekst.Replace("_", string.Empty, StringComparison.Ordinal);
}
