namespace Verenigd.Domain.Leden;

/// <summary>
/// Telefoonnummers worden opgeslagen als <c>+31612345678</c> en getoond als <c>06 12345678</c> (besluit 32).
/// Een nummer dat niet herkend wordt, blijft ongewijzigd.
/// </summary>
public static class Telefoonnummer
{
    public readonly record struct Resultaat(string Waarde, bool Herkend);

    public static Resultaat Normaliseer(string invoer)
    {
        ArgumentNullException.ThrowIfNull(invoer);
        var tekst = invoer.Trim().Replace("(0)", string.Empty, StringComparison.Ordinal);
        var cijfers = new string(tekst.Where(t => t is not (' ' or '-' or '.' or '/' or '(' or ')')).ToArray());

        if (cijfers.StartsWith("00", StringComparison.Ordinal))
        {
            cijfers = "+" + cijfers[2..];
        }

        var kern = cijfers.StartsWith('+') ? cijfers[1..] : cijfers;
        if (kern.Length == 0 || !kern.All(char.IsAsciiDigit))
        {
            return new Resultaat(invoer, false);
        }

        var internationaal = cijfers switch
        {
            ['+', '3', '1', ..] when kern.Length == 11 => cijfers,
            ['+', '3', '2', ..] when kern.Length is 10 or 11 => cijfers,
            ['+', '3', '1' or '2', ..] => null,
            ['+', ..] when kern.Length is >= 8 and <= 15 => cijfers,
            ['0', ..] when cijfers.Length == 10 => "+31" + cijfers[1..],
            ['6', ..] when cijfers.Length == 9 => "+31" + cijfers,
            _ => null,
        };

        return internationaal is null ? new Resultaat(invoer, false) : new Resultaat(internationaal, true);
    }

    public static string Weergave(string opgeslagen)
    {
        ArgumentNullException.ThrowIfNull(opgeslagen);
        return opgeslagen switch
        {
            ['+', '3', '1', '6', ..] when opgeslagen.Length == 12 => "06 " + opgeslagen[4..],
            ['+', '3', '1', ..] when opgeslagen.Length == 12 => "0" + opgeslagen[3..],
            ['+', '3', '2', ..] => "+32 " + opgeslagen[3..],
            _ => opgeslagen,
        };
    }
}
