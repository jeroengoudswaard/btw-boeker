using Verenigd.Domain.Financien;

namespace Verenigd.Domain.Tests.Financien;

public class BetalerTests
{
    [Fact]
    public void Betaler_heeft_vaste_id_en_genormaliseerde_iban()
    {
        var betaler = new Betaler(Iban.Maak("nl91 abna 0417 1643 00"), "  J. de Vries ");

        Assert.NotEqual(Guid.Empty, betaler.Id);
        Assert.Equal("NL91ABNA0417164300", betaler.Iban.Waarde);
        Assert.Equal("J. de Vries", betaler.Rekeninghouder);
    }

    [Fact]
    public void Lege_rekeninghouder_wordt_null()
    {
        var betaler = new Betaler(Iban.Maak("NL91ABNA0417164300"), "x");
        betaler.WijzigRekeninghouder("  ");
        Assert.Null(betaler.Rekeninghouder);
    }
}
