using Verenigd.App.ViewModels;
using Verenigd.Application.Inrichting;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;

namespace Verenigd.App.Tests;

/// <summary>Start in de nieuwe stijl (besluiten 216, 217 en 220): kerncijfers en tellers in de navigatiebalk.</summary>
public sealed class StartTests
{
    private static Startkaart Kaart(Startkaartsoort soort, AdministratieSoort? administratie, int aantal) =>
        new(soort, administratie, aantal, "titel", "tekst", "knop");

    [Fact]
    public void Tellers_horen_bij_het_onderdeel_en_tellen_op_bij_de_module_van_de_gekozen_administratie()
    {
        Startkaart[] kaarten =
        [
            Kaart(Startkaartsoort.Bank, AdministratieSoort.Vereniging, 3),
            Kaart(Startkaartsoort.Postvak, AdministratieSoort.Vereniging, 2),
            Kaart(Startkaartsoort.Bank, AdministratieSoort.Stichting, 7),
            Kaart(Startkaartsoort.Inrichting, null, 4),
        ];

        var tellers = Navigatietellers.Bepaal(kaarten, AdministratieSoort.Vereniging);

        Assert.Equal(3, tellers["Boekhouding.Bank"]);
        Assert.Equal(2, tellers["Boekhouding.Postvak"]);
        Assert.Equal(5, tellers["Boekhouding"]);
        Assert.False(tellers.ContainsKey("Start"));
        Assert.Equal(7, Navigatietellers.Bepaal(kaarten, AdministratieSoort.Stichting)["Boekhouding.Bank"]);
    }

    [Fact]
    public void Kerncijfers_worden_kaarten_met_titel_bedrag_en_toelichting()
    {
        var cijfers = new Kerncijfers(
            [new Rekeningstand("Betaalrekening", new Bedrag(123456), new DateOnly(2026, 10, 30)), new Rekeningstand("Spaarrekening", new Bedrag(500000), null)],
            new Bedrag(-2500), new Bedrag(1000), HeeftBoekhouding: true);

        var regels = OverzichtViewModel.Kerncijferregels(cijfers, 2026);

        Assert.Equal(["Betaalrekening", "Spaarrekening", "Resultaat 2026 t/m vandaag"], regels.Select(r => r.Titel));
        Assert.Equal("€ 1.234,56", regels[0].Waarde);
        Assert.Equal("afschrift van 30-10-2026", regels[0].Toelichting);
        Assert.Equal("volgens het grootboek", regels[1].Toelichting);
        Assert.Equal("-€ 25,00", regels[2].Waarde);
        Assert.Equal("begroot naar rato € 10,00", regels[2].Toelichting);
        Assert.Empty(OverzichtViewModel.Kerncijferregels(cijfers with { HeeftBoekhouding = false }, 2026));
    }
}
