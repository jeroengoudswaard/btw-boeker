using Verenigd.Domain.Financien;

namespace Verenigd.Domain.Tests.Financien;

public class IncassantIdTests
{
    // Openbare voorbeeld-ID's uit de SEPA-documentatie, geen echte incassanten.
    [Theory]
    [InlineData("DE98ZZZ09999999999")]
    [InlineData("de98 zzz 0999 9999 999")]
    public void Geldig_id_wordt_herkend_en_genormaliseerd(string invoer)
    {
        Assert.True(IncassantId.TryMaak(invoer, out var id));
        Assert.Equal("DE98ZZZ09999999999", id.Waarde);
    }

    [Fact]
    public void Controlegetal_wordt_berekend_zonder_bedrijfscode()
    {
        var id = IncassantId.Bereken("NL", "ZZZ", "123456780000");

        Assert.StartsWith("NL", id.Waarde, StringComparison.Ordinal);
        Assert.Equal("ZZZ123456780000", id.Waarde[4..]);
        Assert.True(IncassantId.TryMaak(id.Waarde, out _));
        Assert.True(IncassantId.TryMaak(id.Waarde.Replace("ZZZ", "ABC", StringComparison.Ordinal), out _)); // bedrijfscode telt niet mee
    }

    [Theory]
    [InlineData("DE97ZZZ09999999999")]
    [InlineData("DE98ZZZ")]
    [InlineData("")]
    [InlineData("1234567890")]
    public void Ongeldig_id_wordt_geweigerd(string invoer)
    {
        Assert.False(IncassantId.TryMaak(invoer, out _));
    }
}
