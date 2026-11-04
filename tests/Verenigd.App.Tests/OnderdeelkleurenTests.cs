using Verenigd.App.ViewModels;
using Verenigd.Application.Modules;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.Tests;

/// <summary>Besluit 239: elk onderdeel een eigen kleur, leesbaar in licht én donker.</summary>
public class OnderdeelkleurenTests
{
    private static readonly Kleur LichteGrond = new(0xFF, 0xFF, 0xFF);
    private static readonly Kleur DonkereGrond = new(0x20, 0x20, 0x20);
    private static readonly Kleur DonkereKaart = new(0x2B, 0x2B, 0x2B);
    private static readonly string[] Speltakken = ["Bevers", "Welpen", "Scouts", "Explorers", "Roverscouts", "Leiding"];

    [Fact]
    public void Elk_onderdeel_heeft_een_kleur_en_de_echte_onderdelen_verschillen()
    {
        var kleuren = Enum.GetValues<ModuleSoort>().ToDictionary(m => m, Onderdeelkleuren.Voor);
        var eigen = kleuren.Where(k => k.Key is not (ModuleSoort.Overzicht or ModuleSoort.Instellingen)).Select(k => k.Value).ToList();

        Assert.Equal(eigen.Count, eigen.Distinct().Count());
        Assert.Equal(Onderdeelkleuren.Grijs, kleuren[ModuleSoort.Overzicht]);
        Assert.Equal(Onderdeelkleuren.Grijs, kleuren[ModuleSoort.Instellingen]);
    }

    [Fact]
    public void Elke_paletkleur_haalt_3_op_1_op_de_tint_en_de_grond_in_licht_en_donker()
    {
        foreach (var kleur in Onderdeelkleuren.Palet)
        {
            Assert.True(kleur.Contrastverhouding(Onderdeelkleuren.Tint(kleur, LichteGrond)) >= 3, $"{kleur} op lichte tint");
            Assert.True(kleur.Contrastverhouding(Onderdeelkleuren.Tint(kleur, DonkereGrond)) >= 3, $"{kleur} op donkere tint");
            Assert.True(kleur.Contrastverhouding(LichteGrond) >= 3, $"{kleur} op wit");
            Assert.True(kleur.Contrastverhouding(DonkereKaart) >= 3, $"{kleur} op een donkere kaart");
        }
    }

    [Fact]
    public void De_tint_is_de_kleur_voor_16_procent_over_de_grond()
    {
        var tint = Onderdeelkleuren.Tint(new Kleur(0xFF, 0x00, 0x00), LichteGrond);

        Assert.Equal(new Kleur(0xFF, 0xD6, 0xD6), tint);
    }

    [Fact]
    public void Elke_startkaart_hoort_bij_een_onderdeel()
    {
        foreach (var soort in Enum.GetValues<Verenigd.Application.Inrichting.Startkaartsoort>())
        {
            Assert.True(Enum.IsDefined(Onderdeelkleuren.OnderdeelVan(soort)));
        }

        Assert.Equal(ModuleSoort.Contributie, Onderdeelkleuren.OnderdeelVan(Verenigd.Application.Inrichting.Startkaartsoort.Incassorun));
        Assert.Equal(ModuleSoort.Boekhouding, Onderdeelkleuren.OnderdeelVan(Verenigd.Application.Inrichting.Startkaartsoort.Bank));
    }

    /// <summary>Gekleurde initialen per groep, zoals in de webapp: dezelfde groep altijd dezelfde kleur.</summary>
    [Fact]
    public void Een_groep_heeft_altijd_dezelfde_kleur_uit_het_palet()
    {
        Assert.Equal(Onderdeelkleuren.VoorGroep("Welpen"), Onderdeelkleuren.VoorGroep("Welpen"));
        Assert.Contains(Onderdeelkleuren.VoorGroep("Welpen"), Onderdeelkleuren.Palet);
        Assert.Equal(Onderdeelkleuren.Grijs, Onderdeelkleuren.VoorGroep(null));
        Assert.Equal(Onderdeelkleuren.Grijs, Onderdeelkleuren.VoorGroep(""));
        var kleuren = Speltakken.Select(Onderdeelkleuren.VoorGroep).Distinct().Count();
        Assert.True(kleuren >= 4, $"Maar {kleuren} kleuren voor zes groepen");
    }

    [Theory]
    [InlineData("Anna", "Vries", "AV")]
    [InlineData("bo", "jansen", "BJ")]
    [InlineData("", "Bakker", "B")]
    public void Initialen_zijn_de_eerste_letters_van_voor_en_achternaam(string voornaam, string achternaam, string verwacht)
    {
        Assert.Equal(verwacht, Onderdeelkleuren.Initialen(voornaam, achternaam));
    }

    /// <summary>Besluit 243: dezelfde verwachtingen als src/lib/ontwerp.test.ts in de webapp, zodat beide dezelfde kleur geven.</summary>
    [Theory]
    [InlineData("Bevers", "Violet")]
    [InlineData("Welpen", "Amber")]
    [InlineData("Scouts", "Groenblauw")]
    [InlineData("Explorers", "Koraal")]
    [InlineData("Junioren", "Indigo")]
    public void Groepskleur_is_gelijk_aan_die_in_de_webapp(string groep, string kleur)
    {
        var verwacht = (Kleur)typeof(Onderdeelkleuren).GetField(kleur)!.GetValue(null)!;
        Assert.Equal(verwacht, Onderdeelkleuren.VoorGroep(groep));
    }
}
