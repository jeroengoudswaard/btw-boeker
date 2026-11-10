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
    public void Alleen_boekhouding_heeft_een_sneltoets()
    {
        var nummers = ModuleCatalogus.Alle.Where(m => m.Sneltoetsnummer is not null).Select(m => m.Sneltoetsnummer!.Value).ToList();

        Assert.Equal([1], nummers);
    }

    [Fact]
    public void Boekhouding_hoort_bij_een_administratie()
    {
        Assert.True(ModuleCatalogus.Geef(ModuleSoort.Boekhouding).PerAdministratie);
    }

    [Theory]
    [InlineData(ModuleSoort.Overzicht)]
    [InlineData(ModuleSoort.Instellingen)]
    public void Gedeelde_modules_horen_niet_bij_een_administratie(ModuleSoort soort)
    {
        Assert.False(ModuleCatalogus.Geef(soort).PerAdministratie);
    }

    [Fact]
    public void Boekhouding_is_niet_beperkt_tot_een_soort_administratie()
    {
        Assert.Null(ModuleCatalogus.Geef(ModuleSoort.Boekhouding).AlleenVoor);
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
