using Verenigd.Domain.Leden;

namespace Verenigd.Domain.Tests.Leden;

public class TelefoonnummerTests
{
    [Theory]
    [InlineData("0612345678", "+31612345678")]
    [InlineData("06-12345678", "+31612345678")]
    [InlineData("06 12 34 56 78", "+31612345678")]
    [InlineData("+31 6 12345678", "+31612345678")]
    [InlineData("+31 (0)6 12345678", "+31612345678")]
    [InlineData("0031612345678", "+31612345678")]
    [InlineData("612345678", "+31612345678")] // voorloopnul verloren (bijv. via Excel)
    [InlineData("070-1234567", "+31701234567")]
    [InlineData("+32 470 12 34 56", "+32470123456")]
    [InlineData("0032 2 123 45 67", "+3221234567")]
    [InlineData("+49 30 1234567", "+49301234567")]
    public void Herkende_nummers_worden_internationaal_opgeslagen(string invoer, string verwacht)
    {
        var nummer = Telefoonnummer.Normaliseer(invoer);

        Assert.True(nummer.Herkend);
        Assert.Equal(verwacht, nummer.Waarde);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("06-1234")]
    [InlineData("onbekend")]
    [InlineData("0612345678 of 0701234567")]
    public void Onherkenbaar_nummer_blijft_ongewijzigd(string invoer)
    {
        var nummer = Telefoonnummer.Normaliseer(invoer);

        Assert.False(nummer.Herkend);
        Assert.Equal(invoer, nummer.Waarde);
    }

    [Theory]
    [InlineData("+31612345678", "06 12345678")]
    [InlineData("+31701234567", "0701234567")]
    [InlineData("+32470123456", "+32 470123456")]
    [InlineData("12345", "12345")]
    public void Weergave_volgt_nederlandse_notatie(string opgeslagen, string verwacht)
    {
        Assert.Equal(verwacht, Telefoonnummer.Weergave(opgeslagen));
    }
}
