using Verenigd.Domain.Financien;

namespace Verenigd.Domain.Tests.Financien;

public class SepaTekstTests
{
    [Theory]
    [InlineData("Zoë van 't Hoff", "Zoe van 't Hoff")]
    [InlineData("Jansen & Zn.", "Jansen + Zn.")]
    [InlineData("Ærø Straße", "AEro Strasse")]
    [InlineData("a\tb\nc", "a b c")]
    [InlineData("€ 5 #1 *", "EUR 5 .1 .")]
    [InlineData("  dubbel   spatie  ", "dubbel spatie")]
    public void Alleen_toegestane_sepa_tekens(string invoer, string verwacht)
    {
        Assert.Equal(verwacht, SepaTekst.Normaliseer(invoer, 140));
    }

    [Fact]
    public void Wordt_ingekort_tot_maximale_lengte()
    {
        Assert.Equal(new string('a', 35), SepaTekst.Normaliseer(new string('a', 50), 35));
    }

    [Theory]
    [InlineData("VER-100000001-01", true)]
    [InlineData("MANDAAT-2019/07", true)]
    [InlineData("MET SPATIE", false)]
    [InlineData("", false)]
    [InlineData("/begin", false)]
    [InlineData("eind/", false)]
    [InlineData("a//b", false)]
    [InlineData("Zoë", false)]
    public void Mandaatkenmerk_regels(string kenmerk, bool geldig)
    {
        Assert.Equal(geldig, SepaTekst.IsGeldigKenmerk(kenmerk));
        Assert.False(SepaTekst.IsGeldigKenmerk(new string('a', 36)));
    }
}
