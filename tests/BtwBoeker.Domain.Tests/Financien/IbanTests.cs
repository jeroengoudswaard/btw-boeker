using BtwBoeker.Domain.Financien;

namespace BtwBoeker.Domain.Tests.Financien;

// Alle IBAN's in deze tests zijn openbare voorbeeldnummers, geen echte rekeningen.
public class IbanTests
{
    [Theory]
    [InlineData("NL91ABNA0417164300", "NL91ABNA0417164300")]
    [InlineData("nl91 abna 0417 1643 00", "NL91ABNA0417164300")]
    [InlineData("  NL91ABNA0417164300  ", "NL91ABNA0417164300")]
    public void Maak_normaliseert_spaties_en_hoofdletters(string invoer, string verwacht)
    {
        Assert.Equal(verwacht, Iban.Maak(invoer).Waarde);
    }

    [Fact]
    public void Gelijkheid_is_ongevoelig_voor_spaties_en_hoofdletters()
    {
        Assert.Equal(Iban.Maak("NL91ABNA0417164300"), Iban.Maak("nl91 abna 0417 1643 00"));
    }

    [Theory]
    [InlineData("NL91ABNA0417164300")]
    [InlineData("BE68539007547034")]
    [InlineData("DE89370400440532013000")]
    public void Geldig_controlegetal_wordt_herkend(string invoer)
    {
        Assert.True(Iban.Maak(invoer).ControlegetalGeldig);
    }

    [Theory]
    [InlineData("NL92ABNA0417164300")]
    [InlineData("NL91ABNA041716430")]
    [InlineData("1234567890")]
    [InlineData("NL91ABNA04171643!0")]
    public void Ongeldig_controlegetal_wordt_herkend_maar_waarde_blijft(string invoer)
    {
        var iban = Iban.Maak(invoer);

        Assert.False(iban.ControlegetalGeldig);
        Assert.Equal(invoer.ToUpperInvariant(), iban.Waarde);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryMaak_lege_invoer_geeft_false(string? invoer)
    {
        Assert.False(Iban.TryMaak(invoer, out _));
    }

    [Fact]
    public void ToString_toont_groepen_van_vier()
    {
        Assert.Equal("NL91 ABNA 0417 1643 00", Iban.Maak("NL91ABNA0417164300").ToString());
    }
}
