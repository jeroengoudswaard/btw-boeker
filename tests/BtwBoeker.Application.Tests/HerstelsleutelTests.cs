using BtwBoeker.Application.Beveiliging;

namespace BtwBoeker.Application.Tests;

public class HerstelsleutelTests
{
    [Fact]
    public void Genereer_geeft_acht_groepen_van_vijf_base32_tekens()
    {
        var tekst = Herstelsleutel.Genereer().ToString();

        var groepen = tekst.Split('-');
        Assert.Equal(8, groepen.Length);
        Assert.All(groepen, g => Assert.Matches("^[A-Z2-7]{5}$", g));
    }

    [Fact]
    public void Genereer_geeft_steeds_een_andere_sleutel()
    {
        Assert.NotEqual(Herstelsleutel.Genereer().ToString(), Herstelsleutel.Genereer().ToString());
    }

    [Fact]
    public void Sleutel_bevat_200_bits_willekeur()
    {
        Assert.Equal(25, Herstelsleutel.Genereer().Bytes.Length);
    }

    [Fact]
    public void Parse_is_tolerant_voor_kleine_letters_spaties_en_streepjes()
    {
        var origineel = Herstelsleutel.Genereer();
        var rommelig = " " + origineel.ToString().ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal) + " ";

        Assert.True(Herstelsleutel.TryParse(rommelig, out var gelezen));
        Assert.Equal(origineel.Bytes, gelezen.Bytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCDE-FGHIJ")]
    [InlineData("ABCDE-FGHIJ-KLMNO-PQRST-UVWXY-Z2345-67ABC-DEFG0")] // 0 zit niet in Base32
    [InlineData(null)]
    public void TryParse_ongeldige_sleutel_geeft_false(string? invoer)
    {
        Assert.False(Herstelsleutel.TryParse(invoer, out _));
    }
}
