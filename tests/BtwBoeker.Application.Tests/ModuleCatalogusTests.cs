using BtwBoeker.Application.Modules;

namespace BtwBoeker.Application.Tests;

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
    public void Sneltoetsnummers_zijn_uniek_en_lopen_van_1_tot_2()
    {
        var nummers = ModuleCatalogus.Alle.Where(m => m.Sneltoetsnummer is not null).Select(m => m.Sneltoetsnummer!.Value).ToList();

        Assert.Equal([1, 2], nummers.Order());
    }

    [Theory]
    [InlineData(ModuleSoort.Boekhouding)]
    [InlineData(ModuleSoort.Begroting)]
    public void Financiele_modules_horen_bij_een_administratie(ModuleSoort soort)
    {
        Assert.True(ModuleCatalogus.Geef(soort).PerAdministratie);
    }

    [Theory]
    [InlineData(ModuleSoort.Overzicht)]
    [InlineData(ModuleSoort.Instellingen)]
    public void Gedeelde_modules_horen_niet_bij_een_administratie(ModuleSoort soort)
    {
        Assert.False(ModuleCatalogus.Geef(soort).PerAdministratie);
    }

    [Theory]
    [InlineData(ModuleSoort.Boekhouding)]
    [InlineData(ModuleSoort.Begroting)]
    public void Boekhouding_en_begroting_zijn_voor_beide_administraties(ModuleSoort soort)
    {
        Assert.Null(ModuleCatalogus.Geef(soort).AlleenVoor);
    }

    [Fact]
    public void Alleen_overzicht_kan_niet_gesloten_worden()
    {
        Assert.Equal([ModuleSoort.Overzicht], ModuleCatalogus.Alle.Where(m => !m.KanSluiten).Select(m => m.Soort));
    }

    /// <summary>Besluit 242: elke pagina heeft een kop met één zin uitleg.</summary>
    [Fact]
    public void Elke_module_heeft_een_omschrijving_van_een_korte_zin()
    {
        foreach (var module in ModuleCatalogus.Alle)
        {
            Assert.False(string.IsNullOrWhiteSpace(module.Omschrijving), module.Titel);
            Assert.EndsWith(".", module.Omschrijving, StringComparison.Ordinal);
            Assert.True(module.Omschrijving.Length <= 90, $"{module.Titel}: {module.Omschrijving.Length} tekens");
            Assert.Equal(1, module.Omschrijving.Count(t => t == '.'));
        }
    }
}
