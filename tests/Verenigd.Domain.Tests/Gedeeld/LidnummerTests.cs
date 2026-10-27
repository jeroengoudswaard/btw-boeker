using Verenigd.Domain.Gedeeld;

namespace Verenigd.Domain.Tests.Gedeeld;

public class LidnummerTests
{
    [Theory]
    [InlineData("123456789", "123456789")]
    [InlineData(" 123456789 ", "123456789")]
    public void TryParse_cijfers_geeft_lidnummer(string invoer, string verwacht)
    {
        Assert.True(Lidnummer.TryParse(invoer, out var lidnummer));
        Assert.Equal(verwacht, lidnummer.Waarde);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("12a45")]
    [InlineData("12 45")]
    public void TryParse_ongeldige_invoer_geeft_false(string? invoer)
    {
        Assert.False(Lidnummer.TryParse(invoer, out _));
    }

    [Fact]
    public void Lidnummers_met_dezelfde_waarde_zijn_gelijk()
    {
        Assert.Equal(Lidnummer.Parse("100000001"), Lidnummer.Parse(" 100000001"));
    }
}
