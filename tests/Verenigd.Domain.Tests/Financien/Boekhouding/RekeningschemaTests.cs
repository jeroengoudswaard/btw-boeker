using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Domain.Tests.Financien.Boekhouding;

public class RekeningschemaTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(100000)]
    public void Rekeningnummer_buiten_bereik_wordt_geweigerd(int nummer)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Grootboekrekening(AdministratieSoort.Vereniging, nummer, "x", RekeningSoort.Balans, null));
    }

    [Fact]
    public void Rekening_weergave_en_wijzigen()
    {
        var rekening = new Grootboekrekening(AdministratieSoort.Vereniging, 4115, "  Energie  ", RekeningSoort.Resultaat, null);

        Assert.Equal("4115 Energie", rekening.ToString());
        Assert.True(rekening.IsActief);
        rekening.Deactiveer();
        Assert.False(rekening.IsActief);
        rekening.Activeer();
        rekening.Wijzig("Energie/water", new Rubriek(AdministratieSoort.Vereniging, "Terreinkosten", 3), RekeningSoort.Resultaat);
        Assert.Equal("Terreinkosten", rekening.Rubriek?.Naam);
        Assert.Throws<ArgumentException>(() => rekening.Wijzig(" ", null, RekeningSoort.Resultaat));
    }

    [Fact]
    public void Dagboekcode_hoofdletters_en_kas_en_bank_vereisen_rekening()
    {
        Assert.Equal("MEM", new Dagboek(AdministratieSoort.Vereniging, DagboekSoort.Memoriaal, "mem", "Memoriaal", null).Code);
        Assert.Throws<ArgumentException>(() => new Dagboek(AdministratieSoort.Vereniging, DagboekSoort.Kas, "KAS", "Kas", null));
        Assert.Throws<ArgumentException>(() => new Dagboek(AdministratieSoort.Vereniging, DagboekSoort.Memoriaal, "M E", "x", null));
    }

    [Fact]
    public void Boekjaar_sluiten_en_heropenen()
    {
        var jaar = new Boekjaar(AdministratieSoort.Vereniging, 2026);
        Assert.True(jaar.IsOpen);

        jaar.Sluit(DateTime.UtcNow);
        Assert.False(jaar.IsOpen);
        Assert.Throws<InvalidOperationException>(() => jaar.Sluit(DateTime.UtcNow));

        jaar.Heropen();
        Assert.True(jaar.IsOpen);
        Assert.Null(jaar.AfgeslotenUtc);
    }
}
