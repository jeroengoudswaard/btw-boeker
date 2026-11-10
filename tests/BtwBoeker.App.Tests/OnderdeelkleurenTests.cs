using BtwBoeker.App.ViewModels;
using BtwBoeker.Application.Modules;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.App.Tests;

/// <summary>Elk onderdeel een eigen kleur, leesbaar in licht én donker.</summary>
public class OnderdeelkleurenTests
{
    private static readonly Kleur LichteGrond = new(0xFF, 0xFF, 0xFF);
    private static readonly Kleur DonkereGrond = new(0x20, 0x20, 0x20);
    private static readonly Kleur DonkereKaart = new(0x2B, 0x2B, 0x2B);
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
        foreach (var soort in Enum.GetValues<BtwBoeker.Application.Inrichting.Startkaartsoort>())
        {
            Assert.True(Enum.IsDefined(Onderdeelkleuren.OnderdeelVan(soort)));
        }

        Assert.Equal(ModuleSoort.Boekhouding, Onderdeelkleuren.OnderdeelVan(BtwBoeker.Application.Inrichting.Startkaartsoort.Bank));
    }

    [Theory]
    [InlineData("Anna", "Vries", "AV")]
    [InlineData("bo", "jansen", "BJ")]
    [InlineData("", "Bakker", "B")]
    public void Initialen_zijn_de_eerste_letters_van_voor_en_achternaam(string voornaam, string achternaam, string verwacht)
    {
        Assert.Equal(verwacht, Onderdeelkleuren.Initialen(voornaam, achternaam));
    }

}
