using Verenigd.Domain.Leden;

namespace Verenigd.Domain.Tests.Leden;

public class SolIdTests
{
    [Fact]
    public void Vergelijking_is_hoofdlettergevoelig()
    {
        Assert.NotEqual(new SolId("003Ab00000AbCdE"), new SolId("003AB00000ABCDE"));
        Assert.Equal(new SolId("003Ab00000AbCdE"), new SolId("003Ab00000AbCdE"));
    }

    [Fact]
    public void Spaties_aan_de_randen_worden_verwijderd()
    {
        Assert.Equal("003Ab00000AbCdE", new SolId(" 003Ab00000AbCdE ").Waarde);
    }

    [Fact]
    public void Lege_waarde_is_niet_toegestaan()
    {
        Assert.Throws<ArgumentException>(() => new SolId("  "));
    }
}
