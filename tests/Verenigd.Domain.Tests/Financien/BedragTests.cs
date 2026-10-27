using Verenigd.Domain.Financien;

namespace Verenigd.Domain.Tests.Financien;

public class BedragTests
{
    [Theory]
    [InlineData(0, "€ 0,00")]
    [InlineData(1250, "€ 12,50")]
    [InlineData(123456, "€ 1.234,56")]
    [InlineData(-500, "-€ 5,00")]
    public void ToString_gebruikt_nederlandse_notatie(long centen, string verwacht)
    {
        Assert.Equal(verwacht, new Bedrag(centen).ToString());
    }

    [Theory]
    [InlineData("12,50", 1250)]
    [InlineData("€ 12,50", 1250)]
    [InlineData("1.234,56", 123456)]
    [InlineData("12", 1200)]
    [InlineData(" 7,5 ", 750)]
    [InlineData("0", 0)]
    public void TryParse_leest_nederlandse_invoer(string invoer, long verwacht)
    {
        Assert.True(Bedrag.TryParse(invoer, out var bedrag));
        Assert.Equal(verwacht, bedrag.Centen);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1,234")]
    [InlineData("-5")]
    public void TryParse_ongeldige_of_negatieve_invoer_geeft_false(string invoer)
    {
        Assert.False(Bedrag.TryParse(invoer, out _));
    }

    [Fact]
    public void Optellen_en_vermenigvuldigen()
    {
        Assert.Equal(new Bedrag(3000), new Bedrag(1000) + new Bedrag(2000));
        Assert.Equal(new Bedrag(3750), new Bedrag(1250) * 3);
        Assert.Equal(new Bedrag(4500), new[] { new Bedrag(1500), new Bedrag(3000) }.Som());
    }

    [Fact]
    public void Sepa_notatie_heeft_punt_en_twee_decimalen()
    {
        Assert.Equal("1234.56", new Bedrag(123456).SepaNotatie);
        Assert.Equal("0.05", new Bedrag(5).SepaNotatie);
    }
}
