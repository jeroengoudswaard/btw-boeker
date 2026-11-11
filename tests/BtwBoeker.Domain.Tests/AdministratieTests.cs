using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.Domain.Tests;

public class AdministratieTests
{
    [Fact]
    public void Nieuwe_administratie_heeft_opgegeven_eigenschappen()
    {
        var a = new Administratie(AdministratieSoort.Standaard, "Vakantiehuisje");

        Assert.Equal(AdministratieSoort.Standaard, a.Soort);
        Assert.Equal("Vakantiehuisje", a.Naam);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Lege_naam_is_niet_toegestaan(string naam)
    {
        var a = Standaard();

        Assert.Throws<ArgumentException>(() => a.WijzigNaam(naam));
    }

    [Fact]
    public void Naam_langer_dan_maximum_is_niet_toegestaan()
    {
        var a = Standaard();

        Assert.Throws<ArgumentException>(() => a.WijzigNaam(new string('x', Administratie.MaximaleNaamLengte + 1)));
    }

    [Fact]
    public void WijzigNaam_verwijdert_omliggende_spaties()
    {
        var a = Standaard();

        a.WijzigNaam("  Vakantiehuis De Duinen  ");

        Assert.Equal("Vakantiehuis De Duinen", a.Naam);
    }

    [Fact]
    public void Drempel_zonder_bon_is_standaard_10_euro_en_instelbaar()
    {
        var a = Standaard();
        Assert.Equal(1000, a.ZonderBonDrempel);

        a.WijzigZonderBonDrempel(2500);
        Assert.Equal(2500, a.ZonderBonDrempel);
        a.WijzigZonderBonDrempel(0); // elke uitgave zonder bon tonen
        Assert.Equal(0, a.ZonderBonDrempel);
        Assert.Throws<ArgumentOutOfRangeException>(() => a.WijzigZonderBonDrempel(-1));
    }

    private static Administratie Standaard() => new(AdministratieSoort.Standaard, Administratie.Standaardnaam);
}
