using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Verenigd.Domain.Financien;

/// <summary>
/// Het SEPA-incassant-ID (Creditor Identifier), bijvoorbeeld <c>NL12ZZZ123456780000</c>. Het controlegetal (posities 3-4)
/// wordt berekend over landcode en nationale identificatie; de bedrijfscode (posities 5-7) telt niet mee.
/// </summary>
public readonly record struct IncassantId
{
    private IncassantId(string waarde) => Waarde = waarde;

    public string Waarde { get; }

    public static bool TryMaak([NotNullWhen(true)] string? invoer, out IncassantId id)
    {
        id = default;
        var schoon = string.Concat((invoer ?? string.Empty).Where(t => !char.IsWhiteSpace(t))).ToUpperInvariant();
        if (schoon.Length is < 8 or > 35
            || !char.IsAsciiLetterUpper(schoon[0]) || !char.IsAsciiLetterUpper(schoon[1])
            || !char.IsAsciiDigit(schoon[2]) || !char.IsAsciiDigit(schoon[3])
            || !schoon.All(char.IsAsciiLetterOrDigit))
        {
            return false;
        }

        if (Controlegetal(schoon[..2], schoon[7..]) != schoon[2..4])
        {
            return false;
        }

        id = new IncassantId(schoon);
        return true;
    }

    public static IncassantId Maak(string invoer) =>
        TryMaak(invoer, out var id) ? id : throw new ArgumentException("Dit is geen geldig incassant-ID.", nameof(invoer));

    /// <summary>Stelt een incassant-ID samen en berekent het controlegetal.</summary>
    public static IncassantId Bereken(string landcode, string bedrijfscode, string nationaleId)
    {
        ArgumentNullException.ThrowIfNull(landcode);
        ArgumentNullException.ThrowIfNull(bedrijfscode);
        ArgumentNullException.ThrowIfNull(nationaleId);
        return Maak(landcode + Controlegetal(landcode.ToUpperInvariant(), nationaleId.ToUpperInvariant()) + bedrijfscode + nationaleId);
    }

    public override string ToString() => Waarde ?? string.Empty;

    private static string Controlegetal(string landcode, string nationaleId)
    {
        var cijfers = new StringBuilder();
        foreach (var teken in nationaleId + landcode + "00")
        {
            cijfers.Append(char.IsAsciiDigit(teken) ? (teken - '0').ToString(CultureInfo.InvariantCulture) : (teken - 'A' + 10).ToString(CultureInfo.InvariantCulture));
        }

        var rest = 0;
        foreach (var cijfer in cijfers.ToString())
        {
            rest = ((rest * 10) + (cijfer - '0')) % 97;
        }

        return (98 - rest).ToString("00", CultureInfo.InvariantCulture);
    }
}
