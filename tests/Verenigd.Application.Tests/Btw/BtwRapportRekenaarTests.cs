using Verenigd.Application.Btw;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Btw;

namespace Verenigd.Application.Tests.Btw;

public sealed class BtwRapportRekenaarTests
{
    [Fact]
    public void Maandrapport_groepeert_omzet_en_voorbelasting_per_tarief()
    {
        var boekingen = new[]
        {
            new BtwBoeking(new DateOnly(2026, 3, 2), BtwBoekingSoort.Omzet, 21, new Bedrag(10000), new Bedrag(2100)),
            new BtwBoeking(new DateOnly(2026, 3, 4), BtwBoekingSoort.Omzet, 9, new Bedrag(5000), new Bedrag(450)),
            new BtwBoeking(new DateOnly(2026, 3, 8), BtwBoekingSoort.Voorbelasting, 21, new Bedrag(2000), new Bedrag(420)),
        };

        var rapport = BtwRapportRekenaar.Bereken(2026, 3, boekingen);

        Assert.Equal(2, rapport.Tarieven.Count);
        Assert.Equal(new Bedrag(15000), rapport.OmzetGrondslag);
        Assert.Equal(new Bedrag(2550), rapport.OmzetBtw);
        Assert.Equal(new Bedrag(420), rapport.Voorbelasting);
        Assert.Equal(new Bedrag(2130), rapport.TeBetalen);
    }

    [Fact]
    public void Rapport_neemt_correctie_privegebruik_mee_zonder_zelf_een_correctie_te_berekenen()
    {
        var boekingen = new[]
        {
            new BtwBoeking(new DateOnly(2026, 4, 5), BtwBoekingSoort.Omzet, 21, new Bedrag(10000), new Bedrag(2100)),
            new BtwBoeking(new DateOnly(2026, 4, 30), BtwBoekingSoort.PrivegebruikCorrectie, null, Bedrag.Nul, new Bedrag(125)),
        };

        var rapport = BtwRapportRekenaar.Bereken(2026, 4, boekingen);

        Assert.Equal(new Bedrag(125), rapport.PrivegebruikCorrectie);
        Assert.Equal(new Bedrag(2225), rapport.TeBetalen);
    }

    [Fact]
    public void Andere_maanden_tellen_niet_mee()
    {
        var boekingen = new[]
        {
            new BtwBoeking(new DateOnly(2026, 2, 28), BtwBoekingSoort.Omzet, 21, new Bedrag(10000), new Bedrag(2100)),
            new BtwBoeking(new DateOnly(2026, 4, 1), BtwBoekingSoort.Voorbelasting, 21, new Bedrag(2000), new Bedrag(420)),
        };

        var rapport = BtwRapportRekenaar.Bereken(2026, 3, boekingen);

        Assert.Empty(rapport.Tarieven);
        Assert.Equal(Bedrag.Nul, rapport.TeBetalen);
    }
}
