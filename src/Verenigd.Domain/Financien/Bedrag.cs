using System.Globalization;
using System.Text.RegularExpressions;

namespace Verenigd.Domain.Financien;

/// <summary>Een geldbedrag in euro, opgeslagen in hele centen (nooit als drijvendekommagetal).</summary>
public readonly partial record struct Bedrag(long Centen) : IComparable<Bedrag>
{
    private static readonly CultureInfo Nederlands = CultureInfo.GetCultureInfo("nl-NL");

    public static Bedrag Nul { get; } = new(0);

    /// <summary>Zoals in een SEPA-bestand: punt als decimaalteken, twee decimalen.</summary>
    public string SepaNotatie => (Centen / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    public static Bedrag operator +(Bedrag a, Bedrag b) => new(checked(a.Centen + b.Centen));

    public static Bedrag operator *(Bedrag a, int factor) => new(checked(a.Centen * factor));

    public static bool operator <(Bedrag a, Bedrag b) => a.Centen < b.Centen;

    public static bool operator >(Bedrag a, Bedrag b) => a.Centen > b.Centen;

    public static bool operator <=(Bedrag a, Bedrag b) => a.Centen <= b.Centen;

    public static bool operator >=(Bedrag a, Bedrag b) => a.Centen >= b.Centen;

    public static Bedrag Add(Bedrag a, Bedrag b) => a + b;

    public static Bedrag Multiply(Bedrag a, int factor) => a * factor;

    /// <summary>Nederlandse invoer zoals <c>12,50</c>, <c>€ 1.234,56</c> of <c>12</c>. Negatieve bedragen zijn niet toegestaan.</summary>
    public static bool TryParse(string? invoer, out Bedrag bedrag)
    {
        bedrag = Nul;
        var match = Invoerpatroon().Match(invoer ?? string.Empty);
        if (!match.Success)
        {
            return false;
        }

        var euro = long.Parse(match.Groups["euro"].Value.Replace(".", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);
        var centen = match.Groups["centen"].Success ? match.Groups["centen"].Value.PadRight(2, '0') : "00";
        bedrag = new Bedrag((euro * 100) + int.Parse(centen, CultureInfo.InvariantCulture));
        return true;
    }

    public int CompareTo(Bedrag other) => Centen.CompareTo(other.Centen);

    /// <summary>Bijvoorbeeld <c>€ 1.234,56</c>; zelf opgebouwd zodat de notatie op elk systeem gelijk is.</summary>
    public override string ToString() =>
        (Centen < 0 ? "-€ " : "€ ") + Math.Abs(Centen / 100m).ToString("#,##0.00", Nederlands);

    [GeneratedRegex(@"^\s*(€\s*)?(?<euro>\d{1,3}(\.\d{3})+|\d+)(,(?<centen>\d{1,2}))?\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex Invoerpatroon();
}

public static class BedragExtensies
{
    public static Bedrag Som(this IEnumerable<Bedrag> bedragen) =>
        bedragen.Aggregate(Bedrag.Nul, (totaal, bedrag) => totaal + bedrag);
}
