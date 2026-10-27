using Verenigd.Application.Import;

namespace Verenigd.Application.Tests.Import;

public class Sol3DatumTests
{
    [Theory]
    [InlineData("30-11-2012", 2012, 11, 30)]
    [InlineData("30/11/2012", 2012, 11, 30)]
    [InlineData("1-2-2020", 2020, 2, 1)]
    [InlineData(" 06-07-2021 ", 2021, 7, 6)]
    public void Dag_maand_jaar_wordt_gelezen(string invoer, int jaar, int maand, int dag)
    {
        Assert.True(Sol3Datum.TryParse(invoer, out var datum));
        Assert.Equal(new DateOnly(jaar, maand, dag), datum);
    }

    [Theory]
    [InlineData("2012-11-30")]
    [InlineData("31-02-2020")]
    [InlineData("11-30-2012")]
    [InlineData("gisteren")]
    public void Onleesbare_datum_geeft_false(string invoer)
    {
        Assert.False(Sol3Datum.TryParse(invoer, out _));
    }
}
