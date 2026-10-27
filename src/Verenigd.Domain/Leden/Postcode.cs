using System.Text.RegularExpressions;

namespace Verenigd.Domain.Leden;

/// <summary>Nederlandse postcodes krijgen de notatie <c>1234 AB</c>; andere postcodes blijven ongewijzigd.</summary>
public static partial class Postcode
{
    public readonly record struct Resultaat(string Waarde, bool Herkend);

    public static Resultaat Normaliseer(string invoer)
    {
        ArgumentNullException.ThrowIfNull(invoer);
        var match = NederlandsePostcode().Match(invoer);
        return match.Success
            ? new Resultaat($"{match.Groups[1].Value} {match.Groups[2].Value.ToUpperInvariant()}", true)
            : new Resultaat(invoer, false);
    }

    [GeneratedRegex(@"^\s*([1-9][0-9]{3})\s*-?\s*([A-Za-z]{2})\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex NederlandsePostcode();
}
