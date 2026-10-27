using System.Globalization;

namespace Verenigd.Domain.Financien;

/// <summary>Een kalendermaand; de eenheid van de contributie (besluiten 45 en 47).</summary>
public readonly record struct Maand : IComparable<Maand>
{
    // Vaste namen: SEPA-omschrijvingen moeten op elk systeem gelijk zijn.
    private static readonly string[] Namen =
        ["januari", "februari", "maart", "april", "mei", "juni", "juli", "augustus", "september", "oktober", "november", "december"];

    private static readonly string[] KorteNamen = ["jan", "feb", "mrt", "apr", "mei", "jun", "jul", "aug", "sep", "okt", "nov", "dec"];

    public Maand(int jaar, int nummer)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(jaar, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(jaar, 9998);
        ArgumentOutOfRangeException.ThrowIfLessThan(nummer, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(nummer, 12);
        Jaar = jaar;
        Nummer = nummer;
    }

    public int Jaar { get; }
    public int Nummer { get; }

    public DateOnly EersteDag => new(Jaar, Nummer, 1);

    public Maand Volgende => Nummer == 12 ? new Maand(Jaar + 1, 1) : new Maand(Jaar, Nummer + 1);

    public Maand Vorige => Nummer == 1 ? new Maand(Jaar - 1, 12) : new Maand(Jaar, Nummer - 1);

    /// <summary>Bijvoorbeeld <c>2027-03</c>.</summary>
    public string Code => $"{Jaar.ToString(CultureInfo.InvariantCulture)}-{Nummer.ToString("00", CultureInfo.InvariantCulture)}";

    /// <summary>Bijvoorbeeld <c>mrt</c>.</summary>
    public string Kort => KorteNamen[Nummer - 1];

    public static Maand Van(DateOnly datum) => new(datum.Year, datum.Month);

    public static Maand Parse(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        var delen = code.Split('-');
        return new Maand(int.Parse(delen[0], CultureInfo.InvariantCulture), int.Parse(delen[1], CultureInfo.InvariantCulture));
    }

    public static bool operator <(Maand a, Maand b) => a.CompareTo(b) < 0;

    public static bool operator >(Maand a, Maand b) => a.CompareTo(b) > 0;

    public static bool operator <=(Maand a, Maand b) => a.CompareTo(b) <= 0;

    public static bool operator >=(Maand a, Maand b) => a.CompareTo(b) >= 0;

    /// <summary>Alle maanden van deze maand tot en met <paramref name="laatste"/>.</summary>
    public IReadOnlyList<Maand> Tot(Maand laatste)
    {
        var maanden = new List<Maand>();
        for (var m = this; m <= laatste; m = m.Volgende)
        {
            maanden.Add(m);
        }

        return maanden;
    }

    public int CompareTo(Maand other) => (Jaar * 12 + Nummer).CompareTo(other.Jaar * 12 + other.Nummer);

    /// <summary>Bijvoorbeeld <c>maart 2027</c>.</summary>
    public override string ToString() =>
        $"{Namen[Nummer - 1]} {Jaar.ToString(CultureInfo.InvariantCulture)}";
}
