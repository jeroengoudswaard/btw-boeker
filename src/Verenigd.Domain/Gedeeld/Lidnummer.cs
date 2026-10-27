using System.Diagnostics.CodeAnalysis;

namespace Verenigd.Domain.Gedeeld;

/// <summary>
/// Het lidmaatschapsnummer uit SOL3. Gedeeld begrip: de enige koppeling tussen ledengegevens en financiële
/// gegevens (besluit 36).
/// </summary>
public readonly record struct Lidnummer
{
    private Lidnummer(string waarde) => Waarde = waarde;

    public string Waarde { get; }

    public static Lidnummer Parse(string invoer) =>
        TryParse(invoer, out var lidnummer)
            ? lidnummer
            : throw new FormatException("Een lidnummer bestaat alleen uit cijfers.");

    public static bool TryParse([NotNullWhen(true)] string? invoer, out Lidnummer lidnummer)
    {
        lidnummer = default;
        var tekst = invoer?.Trim();
        if (string.IsNullOrEmpty(tekst) || !tekst.All(char.IsAsciiDigit))
        {
            return false;
        }

        lidnummer = new Lidnummer(tekst);
        return true;
    }

    public override string ToString() => Waarde ?? string.Empty;
}
