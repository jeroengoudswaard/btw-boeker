using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.Domain.Financien.Boekhouding;

public enum HistorischeRegelSoort
{
    Gewoon = 1,

    /// <summary>Een regel zonder datum in Excel: een begin- of tussensaldo (besluit 123).</summary>
    Saldoregel = 2,
}

/// <summary>
/// Eén regel van een T-rekening uit de Excel-boekhouding (besluit 122). Geen journaalregel: er is geen tegenboeking en
/// de regel telt nooit mee in het grootboek.
/// </summary>
public sealed class HistorischeRegel
{
    public const int MaximaleOmschrijvingLengte = 200;

    private HistorischeRegel()
    {
        Omschrijving = string.Empty;
        Rekening = null!;
    }

    /// <exception cref="ArgumentException">De datum valt buiten het jaar, of er is geen bedrag.</exception>
    public HistorischeRegel(
        AdministratieSoort administratie, int jaar, Grootboekrekening rekening, DateOnly datum, string? omschrijving, Bedrag debet, Bedrag credit,
        HistorischeRegelSoort soort, int volgorde)
    {
        ArgumentNullException.ThrowIfNull(rekening);
        if (datum.Year != jaar)
        {
            throw new ArgumentException($"De datum valt niet in {jaar}.", nameof(datum));
        }

        if (debet == Bedrag.Nul && credit == Bedrag.Nul)
        {
            throw new ArgumentException("Een regel heeft een bedrag.", nameof(debet));
        }

        var tekst = (omschrijving ?? string.Empty).Trim();
        Administratie = administratie;
        Jaar = jaar;
        Rekening = rekening;
        Datum = datum;
        Omschrijving = tekst.Length > MaximaleOmschrijvingLengte ? tekst[..MaximaleOmschrijvingLengte] : tekst;
        Debet = debet;
        Credit = credit;
        Soort = soort;
        Volgorde = volgorde;
    }

    public int Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public int Jaar { get; private set; }
    public Grootboekrekening Rekening { get; private set; }
    public DateOnly Datum { get; private set; }
    public string Omschrijving { get; private set; }
    public Bedrag Debet { get; private set; }
    public Bedrag Credit { get; private set; }
    public HistorischeRegelSoort Soort { get; private set; }

    /// <summary>De volgorde zoals in Excel.</summary>
    public int Volgorde { get; private set; }

    public long Saldo => Debet.Centen - Credit.Centen;
}

/// <summary>Het totaal van een rekening volgens het Voorblad van de Excel-boekhouding (W&amp;V of balans per 31-12).</summary>
public sealed class HistorischTotaal
{
    private HistorischTotaal()
    {
        Rekening = null!;
    }

    public HistorischTotaal(AdministratieSoort administratie, int jaar, Grootboekrekening rekening, Bedrag debet, Bedrag credit)
    {
        ArgumentNullException.ThrowIfNull(rekening);
        Administratie = administratie;
        Jaar = jaar;
        Rekening = rekening;
        Debet = debet;
        Credit = credit;
    }

    public int Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public int Jaar { get; private set; }
    public Grootboekrekening Rekening { get; private set; }
    public Bedrag Debet { get; private set; }
    public Bedrag Credit { get; private set; }

    public long Saldo => Debet.Centen - Credit.Centen;
}
