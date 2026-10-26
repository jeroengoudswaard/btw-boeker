using Verenigd.Application.Modules;

namespace Verenigd.Application.Tests;

public class ModuleCatalogusTests
{
    [Fact]
    public void Alle_modules_hebben_een_definitie()
    {
        foreach (var soort in Enum.GetValues<ModuleSoort>())
        {
            Assert.Equal(soort, ModuleCatalogus.Geef(soort).Soort);
        }
    }

    [Fact]
    public void Sneltoetsnummers_zijn_uniek_en_lopen_van_1_tot_6()
    {
        var nummers = ModuleCatalogus.Alle.Where(m => m.Sneltoetsnummer is not null).Select(m => m.Sneltoetsnummer!.Value).ToList();

        Assert.Equal([1, 2, 3, 4, 5, 6], nummers.Order());
    }

    [Theory]
    [InlineData(ModuleSoort.Contributie)]
    [InlineData(ModuleSoort.Boekhouding)]
    [InlineData(ModuleSoort.Begroting)]
    public void Financiele_modules_horen_bij_een_administratie(ModuleSoort soort)
    {
        Assert.True(ModuleCatalogus.Geef(soort).PerAdministratie);
    }

    [Theory]
    [InlineData(ModuleSoort.Overzicht)]
    [InlineData(ModuleSoort.Leden)]
    [InlineData(ModuleSoort.Email)]
    [InlineData(ModuleSoort.Instellingen)]
    public void Gedeelde_modules_horen_niet_bij_een_administratie(ModuleSoort soort)
    {
        Assert.False(ModuleCatalogus.Geef(soort).PerAdministratie);
    }

    [Fact]
    public void Alleen_overzicht_kan_niet_gesloten_worden()
    {
        Assert.Equal([ModuleSoort.Overzicht], ModuleCatalogus.Alle.Where(m => !m.KanSluiten).Select(m => m.Soort));
    }
}
