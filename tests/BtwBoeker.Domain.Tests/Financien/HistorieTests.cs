using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Domain.Tests.Financien;

/// <summary>Het historische boekjaar uit de Excel-boekhouding (besluiten 122 en 123).</summary>
public sealed class HistorieTests
{
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private static readonly DateTime Nu = new(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Grootboekrekening Bank = new(Ver, 1100, "Bank", RekeningSoort.Balans, null);

    [Fact]
    public void Een_historisch_boekjaar_is_afgesloten_en_kan_niet_worden_heropend()
    {
        var jaar = Boekjaar.Historisch(Ver, 2025, Nu);

        Assert.True(jaar.IsHistorisch);
        Assert.False(jaar.IsOpen);
        Assert.Equal(Nu, jaar.AfgeslotenUtc);
        Assert.Throws<InvalidOperationException>(jaar.Heropen);
        Assert.False(new Boekjaar(Ver, 2026).IsHistorisch);
    }

    [Fact]
    public void Een_historische_regel_bewaart_datum_omschrijving_en_bedragen()
    {
        var regel = new HistorischeRegel(Ver, 2025, Bank, new DateOnly(2025, 3, 1), "  Maart  ", new Bedrag(1250), Bedrag.Nul, HistorischeRegelSoort.Gewoon, 1);

        Assert.Equal("Maart", regel.Omschrijving);
        Assert.Equal(1250, regel.Saldo);
        Assert.Same(Bank, regel.Rekening);
    }

    [Fact]
    public void Een_historische_regel_valt_binnen_het_jaar_en_heeft_een_bedrag()
    {
        Assert.Throws<ArgumentException>(() =>
            new HistorischeRegel(Ver, 2025, Bank, new DateOnly(2024, 12, 31), "x", new Bedrag(1), Bedrag.Nul, HistorischeRegelSoort.Gewoon, 1));
        Assert.Throws<ArgumentException>(() =>
            new HistorischeRegel(Ver, 2025, Bank, new DateOnly(2025, 1, 1), "x", Bedrag.Nul, Bedrag.Nul, HistorischeRegelSoort.Gewoon, 1));
        var lang = new HistorischeRegel(Ver, 2025, Bank, new DateOnly(2025, 1, 1), new string('a', 300), Bedrag.Nul, new Bedrag(5), HistorischeRegelSoort.Saldoregel, 2);
        Assert.Equal(HistorischeRegel.MaximaleOmschrijvingLengte, lang.Omschrijving.Length);
        Assert.Equal(-5, lang.Saldo);
    }

    [Fact]
    public void Een_historisch_totaal_geeft_het_saldo_volgens_het_voorblad()
    {
        var totaal = new HistorischTotaal(Ver, 2025, Bank, new Bedrag(15000), new Bedrag(2500));

        Assert.Equal(12500, totaal.Saldo);
    }
}
