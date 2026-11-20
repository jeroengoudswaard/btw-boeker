using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;
using BtwBoeker.Domain.Financien.Btw;

namespace BtwBoeker.Domain.Tests.Financien.Boekhouding;

/// <summary>
/// Besluiten 44–49, fase B1: elke journaalregel draagt een btw-code. Een regel op een btw-rekening (rol <i>Te betalen btw</i>
/// of <i>Voorbelasting</i>) is het btw-bedrag; elke andere regel met een code is de grondslag.
/// </summary>
public class BtwPerRegelTests
{
    private const AdministratieSoort Adm = AdministratieSoort.Standaard;
    private static readonly DateTime Nu = new(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Datum = new(2026, 11, 2);
    private static readonly Grootboekrekening Energie = new(Adm, 4115, "Energie", RekeningSoort.Resultaat, null);
    private static readonly Grootboekrekening Voorbelasting = new(Adm, 1520, "Voorbelasting", RekeningSoort.Balans, null, RekeningRol.Voorbelasting);
    private static readonly Grootboekrekening Crediteuren = new(Adm, 1600, "Crediteuren", RekeningSoort.Balans, null, RekeningRol.Crediteuren);
    private static readonly Dagboek Memoriaal = new(Adm, DagboekSoort.Memoriaal, "MEM", "Memoriaal", null);
    private static readonly BtwCode Voor21 = new(BtwSoort.Voorbelasting, 21);

    private static Journaalpost Factuur(BtwCode? codeOpBtwRegel)
    {
        var post = new Journaalpost(Adm, Memoriaal, Datum, "Energie november");
        post.Wijzig(Memoriaal, Datum, "Energie november",
        [
            new RegelInvoer(Energie, null, new Bedrag(865), Bedrag.Nul, Voor21),
            new RegelInvoer(Voorbelasting, null, new Bedrag(182), Bedrag.Nul, codeOpBtwRegel),
            new RegelInvoer(Crediteuren, null, Bedrag.Nul, new Bedrag(1047)),
        ]);
        return post;
    }

    [Theory]
    [InlineData(21)]
    [InlineData(9)]
    [InlineData(0)]
    public void Een_btw_code_kent_21_9_en_0_procent(int percentage)
    {
        var code = new BtwCode(BtwSoort.Omzet, percentage);

        Assert.Equal(percentage, code.Percentage);
        Assert.True(code.HeeftBtw);
    }

    [Fact]
    public void Een_ander_percentage_of_geen_btw_met_een_percentage_mag_niet()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BtwCode(BtwSoort.Omzet, 6));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BtwCode(BtwSoort.Geen, 21));
        Assert.False(BtwCode.Geen.HeeftBtw);
        Assert.Equal(BtwCode.Geen, default);
    }

    [Fact]
    public void Een_regel_bewaart_zijn_code_en_zonder_code_is_het_geen_btw()
    {
        var post = Factuur(Voor21);

        Assert.Equal(Voor21, post.Regels.Single(r => r.Rekening == Energie).Btw);
        Assert.Equal(BtwCode.Geen, post.Regels.Single(r => r.Rekening == Crediteuren).Btw);
        Assert.True(post.Regels.Single(r => r.Rekening == Voorbelasting).IsBtwBedrag);
        Assert.False(post.Regels.Single(r => r.Rekening == Energie).IsBtwBedrag);
        Assert.Empty(post.Controleer());
    }

    [Fact]
    public void Een_regel_op_een_btw_rekening_zonder_code_kan_niet_worden_geboekt()
    {
        var post = Factuur(codeOpBtwRegel: null);

        Assert.Contains(post.Controleer(), f => f.Contains("btw", StringComparison.OrdinalIgnoreCase) && f.Contains("1520", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => post.Boek(1, Nu));
    }

    [Fact]
    public void Een_tegenboeking_neemt_de_btw_codes_over()
    {
        var post = Factuur(Voor21);
        post.Boek(1, Nu);

        var tegen = post.MaakTegenboeking(new DateOnly(2026, 11, 3));

        Assert.Equal(Voor21, tegen.Regels.Single(r => r.Rekening == Energie).Btw);
        Assert.Equal(new Bedrag(865), tegen.Regels.Single(r => r.Rekening == Energie).Credit);
        Assert.Equal(Voor21, tegen.Regels.Single(r => r.Rekening == Voorbelasting).Btw);
        Assert.Empty(tegen.Controleer());
    }
}
