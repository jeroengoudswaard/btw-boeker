using Verenigd.Domain.Financien;

namespace Verenigd.Domain.Tests.Financien;

public class MaandTests
{
    [Fact]
    public void Van_datum_en_eerste_dag()
    {
        var maand = Maand.Van(new DateOnly(2027, 3, 17));

        Assert.Equal(new Maand(2027, 3), maand);
        Assert.Equal(new DateOnly(2027, 3, 1), maand.EersteDag);
    }

    [Fact]
    public void Volgende_en_vorige_over_jaargrens()
    {
        Assert.Equal(new Maand(2028, 1), new Maand(2027, 12).Volgende);
        Assert.Equal(new Maand(2026, 12), new Maand(2027, 1).Vorige);
    }

    [Fact]
    public void Vergelijken_en_tot()
    {
        Assert.True(new Maand(2027, 1) < new Maand(2027, 2));
        Assert.Equal([new Maand(2026, 11), new Maand(2026, 12), new Maand(2027, 1)], new Maand(2026, 11).Tot(new Maand(2027, 1)));
    }

    [Theory]
    [InlineData(2027, 3, "maart 2027", "mrt", "2027-03")]
    [InlineData(2027, 12, "december 2027", "dec", "2027-12")]
    public void Weergave(int jaar, int nummer, string volledig, string kort, string code)
    {
        var maand = new Maand(jaar, nummer);

        Assert.Equal(volledig, maand.ToString());
        Assert.Equal(kort, maand.Kort);
        Assert.Equal(code, maand.Code);
    }

    [Fact]
    public void Ongeldig_maandnummer_wordt_geweigerd()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Maand(2027, 13));
    }
}
