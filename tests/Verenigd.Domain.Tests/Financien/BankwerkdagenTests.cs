using Verenigd.Domain.Financien;

namespace Verenigd.Domain.Tests.Financien;

public class BankwerkdagenTests
{
    [Theory]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2028, 4, 16)]
    [InlineData(2030, 4, 21)]
    public void Pasen_wordt_juist_berekend(int jaar, int maand, int dag)
    {
        Assert.Equal(new DateOnly(jaar, maand, dag), Bankwerkdagen.Pasen(jaar));
    }

    [Theory]
    [InlineData("2027-01-01")] // nieuwjaar
    [InlineData("2027-03-26")] // Goede Vrijdag
    [InlineData("2027-03-29")] // Tweede Paasdag
    [InlineData("2027-05-01")] // 1 mei (zaterdag)
    [InlineData("2027-12-25")]
    [InlineData("2027-12-26")]
    [InlineData("2027-02-06")] // zaterdag
    [InlineData("2027-02-07")] // zondag
    public void Geen_target_werkdag(string datum)
    {
        Assert.False(Bankwerkdagen.IsWerkdag(DateOnly.Parse(datum, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("2027-04-27")] // Koningsdag: wel een TARGET-werkdag
    [InlineData("2027-02-08")]
    public void Wel_target_werkdag(string datum)
    {
        Assert.True(Bankwerkdagen.IsWerkdag(DateOnly.Parse(datum, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Volgende_werkdag_schuift_alleen_als_nodig()
    {
        Assert.Equal(new DateOnly(2027, 2, 8), Bankwerkdagen.OpOfNa(new DateOnly(2027, 2, 6)));
        Assert.Equal(new DateOnly(2027, 2, 8), Bankwerkdagen.OpOfNa(new DateOnly(2027, 2, 8)));
        Assert.Equal(new DateOnly(2027, 3, 30), Bankwerkdagen.OpOfNa(new DateOnly(2027, 3, 26))); // Goede Vrijdag t/m Paasmaandag
    }

    [Fact]
    public void Werkdagen_tellen()
    {
        // vrijdag 5 feb + 1 werkdag = maandag 8 feb
        Assert.Equal(new DateOnly(2027, 2, 8), Bankwerkdagen.TelOp(new DateOnly(2027, 2, 5), 1));
    }
}
