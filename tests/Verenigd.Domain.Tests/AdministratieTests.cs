using Verenigd.Domain.Administraties;

namespace Verenigd.Domain.Tests;

public class AdministratieTests
{
    [Fact]
    public void Nieuwe_administratie_heeft_opgegeven_eigenschappen()
    {
        var a = new Administratie(AdministratieSoort.Stichting, "Stichting Scouting", Kleur.Parse("#1565C0"));

        Assert.Equal(AdministratieSoort.Stichting, a.Soort);
        Assert.Equal("Stichting Scouting", a.Naam);
        Assert.Equal(Kleur.Parse("#1565C0"), a.Accentkleur);
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

        a.WijzigNaam("  Scouting Vereniging  ");

        Assert.Equal("Scouting Vereniging", a.Naam);
    }

    [Fact]
    public void WijzigAccentkleur_past_kleur_aan()
    {
        var a = Standaard();

        a.WijzigAccentkleur(Kleur.Parse("#6A1B9A"));

        Assert.Equal(Kleur.Parse("#6A1B9A"), a.Accentkleur);
    }

    [Fact]
    public void Standaardkleuren_zijn_groen_voor_vereniging_en_blauw_voor_stichting()
    {
        Assert.Equal(Kleur.Parse("#2E7D32"), Administratie.Standaardkleur(AdministratieSoort.Vereniging));
        Assert.Equal(Kleur.Parse("#1565C0"), Administratie.Standaardkleur(AdministratieSoort.Stichting));
    }

    private static Administratie Standaard() =>
        new(AdministratieSoort.Vereniging, "Vereniging", Administratie.Standaardkleur(AdministratieSoort.Vereniging));
}
