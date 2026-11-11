using BtwBoeker.App.ViewModels;

namespace BtwBoeker.App.Tests;

public class KleurTests
{
    [Theory]
    [InlineData("#2E7D32", 0x2E, 0x7D, 0x32)]
    [InlineData("#2e7d32", 0x2E, 0x7D, 0x32)]
    [InlineData("  #FFFFFF ", 0xFF, 0xFF, 0xFF)]
    public void Parse_geldige_hexcode_geeft_juiste_componenten(string invoer, byte r, byte g, byte b)
    {
        var kleur = Kleur.Parse(invoer);

        Assert.Equal(r, kleur.R);
        Assert.Equal(g, kleur.G);
        Assert.Equal(b, kleur.B);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2E7D32")]
    [InlineData("#2E7D3")]
    [InlineData("#2E7D3G")]
    [InlineData("#2E7D3211")]
    [InlineData(null)]
    public void TryParse_ongeldige_invoer_geeft_false(string? invoer)
    {
        Assert.False(Kleur.TryParse(invoer, out _));
        Assert.Throws<FormatException>(() => Kleur.Parse(invoer!));
    }

    [Fact]
    public void ToString_geeft_hexcode_in_hoofdletters()
    {
        Assert.Equal("#2E7D32", Kleur.Parse("#2e7d32").ToString());
    }

    [Theory]
    [InlineData("#2E7D32", "#FFFFFF")] // donkergroen -> witte tekst
    [InlineData("#1565C0", "#FFFFFF")] // blauw -> witte tekst
    [InlineData("#FFEB3B", "#000000")] // geel -> zwarte tekst
    [InlineData("#FFFFFF", "#000000")]
    public void LeesbareTekstkleur_kiest_hoogste_contrast(string achtergrond, string verwacht)
    {
        Assert.Equal(verwacht, Kleur.Parse(achtergrond).LeesbareTekstkleur().ToString());
    }

    [Fact]
    public void Contrastverhouding_zwart_wit_is_21()
    {
        Assert.Equal(21.0, Kleur.Zwart.Contrastverhouding(Kleur.Wit), precision: 2);
    }

    [Fact]
    public void Gelijke_kleuren_zijn_gelijk()
    {
        Assert.Equal(Kleur.Parse("#ABCDEF"), Kleur.Parse("#abcdef"));
    }
}
