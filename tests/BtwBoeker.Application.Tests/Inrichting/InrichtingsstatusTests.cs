using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Inrichting;
using BtwBoeker.Application.Tests.Boekhouding;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.Application.Tests.Inrichting;

/// <summary>Het ✓ per stap van de startwizard volgt uit de gegevens zelf (Welkom → Database → Boekhouding → Bank → Klaar).</summary>
public sealed class InrichtingsstatusTests
{
    private static BoekhoudingStand Leeg() => new(AdministratieSoort.Vereniging, [], [], [], [], []);

    [Fact]
    public void Zonder_database_is_niets_klaar()
    {
        var status = Inrichtingsstatus.Leeg;

        Assert.All(Enum.GetValues<Startstap>().Where(s => s is not (Startstap.Welkom or Startstap.Klaar)), s => Assert.False(status.IsKlaar(s)));
        Assert.Equal(Startstap.Database, status.EersteOpenStap());
    }

    [Fact]
    public void Een_nieuwe_database_heeft_alleen_de_database_klaar()
    {
        var status = Inrichtingsstatus.Bepaal(Leeg());

        Assert.True(status.IsKlaar(Startstap.Database));
        Assert.False(status.IsKlaar(Startstap.Boekhouding));
        Assert.False(status.IsKlaar(Startstap.Bank));
        Assert.Equal(Startstap.Boekhouding, status.EersteOpenStap());
    }

    [Fact]
    public void Ingerichte_administratie_met_bank_heeft_alle_stappen_klaar()
    {
        var boekhouding = new BoekhoudingTestdata();
        boekhouding.Bankdagboek("BNK", "NL91ABNA0417164300");

        var status = Inrichtingsstatus.Bepaal(boekhouding.Stand);

        Assert.True(status.IsKlaar(Startstap.Boekhouding));
        Assert.True(status.IsKlaar(Startstap.Bank));
        Assert.Null(status.EersteOpenStap());
    }

    [Fact]
    public void Een_administratie_zonder_bankdagboek_is_niet_ingesteld()
    {
        var status = Inrichtingsstatus.Bepaal(new BoekhoudingTestdata().Stand);

        Assert.False(status.IsKlaar(Startstap.Bank));
        Assert.Equal(Startstap.Bank, status.EersteOpenStap());
    }

    [Fact]
    public void De_wizard_heeft_vijf_stappen_zonder_administraties()
    {
        Assert.Equal([Startstap.Welkom, Startstap.Database, Startstap.Boekhouding, Startstap.Bank, Startstap.Klaar], Enum.GetValues<Startstap>());
    }
}
