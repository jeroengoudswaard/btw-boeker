using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Btw;

namespace BtwBoeker.Domain.Tests.Financien;

public sealed class BtwBerekeningTests
{
    [Theory]
    [InlineData(2025, 12, 31, 9)]
    [InlineData(2026, 1, 1, 21)]
    public void Logies_tarief_volgt_de_verblijfsdatum(int jaar, int maand, int dag, int verwacht)
    {
        Assert.Equal(verwacht, BtwTarieven.Logies(new DateOnly(jaar, maand, dag)).Percentage);
    }

    [Fact]
    public void Afzonderlijke_faciliteit_heeft_het_verlaagde_tarief()
    {
        Assert.Equal(9, BtwTarieven.AfzonderlijkeFaciliteit.Percentage);
    }

    [Fact]
    public void Inclusief_bedrag_wordt_gesplitst_in_grondslag_en_btw()
    {
        var berekend = BtwBerekening.Bereken(new Bedrag(12100), 21, bedragInclusiefBtw: true);

        Assert.Equal(new Bedrag(10000), berekend.Grondslag);
        Assert.Equal(new Bedrag(2100), berekend.Btw);
        Assert.Equal(new Bedrag(12100), berekend.Totaal);
    }

    [Fact]
    public void Exclusief_bedrag_krijgt_btw_erbij()
    {
        var berekend = BtwBerekening.Bereken(new Bedrag(10000), 9, bedragInclusiefBtw: false);

        Assert.Equal(new Bedrag(10000), berekend.Grondslag);
        Assert.Equal(new Bedrag(900), berekend.Btw);
        Assert.Equal(new Bedrag(10900), berekend.Totaal);
    }

    [Fact]
    public void Inclusief_btw_wordt_op_centniveau_afgerond_en_sluit()
    {
        var berekend = BtwBerekening.Bereken(new Bedrag(4), 21, bedragInclusiefBtw: true);

        Assert.Equal(new Bedrag(3), berekend.Grondslag);
        Assert.Equal(new Bedrag(1), berekend.Btw);
        Assert.Equal(new Bedrag(4), berekend.Totaal);
    }
}
