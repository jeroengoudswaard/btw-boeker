using BtwBoeker.App.ViewModels;
using BtwBoeker.Application.Inrichting;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;

namespace BtwBoeker.App.Tests;

/// <summary>Start in de nieuwe stijl: kerncijfers en tellers in de navigatiebalk.</summary>
public sealed class StartTests
{
    private static Startkaart Kaart(Startkaartsoort soort, int aantal) =>
        new(soort, aantal, "titel", "tekst", "knop");

    [Fact]
    public void Tellers_horen_bij_het_onderdeel_en_tellen_op_bij_de_module()
    {
        Startkaart[] kaarten =
        [
            Kaart(Startkaartsoort.Bank, 3),
            Kaart(Startkaartsoort.Postvak, 2),
            Kaart(Startkaartsoort.Inrichting, 4),
        ];

        var tellers = Navigatietellers.Bepaal(kaarten);

        Assert.Equal(3, tellers["Boekhouding.Bank"]);
        Assert.Equal(2, tellers["Boekhouding.Postvak"]);
        Assert.Equal(5, tellers["Boekhouding"]);
        Assert.False(tellers.ContainsKey("Start"));
    }

    [Fact]
    public void Kerncijfers_worden_kaarten_met_titel_bedrag_en_toelichting()
    {
        var cijfers = new Kerncijfers(
            [new Rekeningstand("Betaalrekening", new Bedrag(123456), new DateOnly(2026, 10, 30)), new Rekeningstand("Spaarrekening", new Bedrag(500000), null)],
            new Bedrag(-2500), HeeftBoekhouding: true);

        var regels = OverzichtViewModel.Kerncijferregels(cijfers, 2026);

        Assert.Equal(["Betaalrekening", "Spaarrekening", "Resultaat 2026 t/m vandaag"], regels.Select(r => r.Titel));
        Assert.Equal("€ 1.234,56", regels[0].Waarde);
        Assert.Equal("afschrift van 30-10-2026", regels[0].Toelichting);
        Assert.Equal("volgens het grootboek", regels[1].Toelichting);
        Assert.Equal("-€ 25,00", regels[2].Waarde);
        Assert.Equal("alleen geboekte posten", regels[2].Toelichting);
        Assert.Empty(OverzichtViewModel.Kerncijferregels(cijfers with { HeeftBoekhouding = false }, 2026));
    }
}
