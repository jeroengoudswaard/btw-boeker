using System.Globalization;
using System.Text;

namespace Verenigd.Domain.Financien;

/// <summary>
/// De tekenset die banken in SEPA-bestanden accepteren: a–z, A–Z, 0–9, spatie en <c>/ - ? : ( ) . , ' +</c>.
/// Andere tekens worden omgezet (é → e, &amp; → +) of vervangen door een punt.
/// </summary>
public static class SepaTekst
{
    private const string Leestekens = "/-?:().,'+ ";

    private static readonly Dictionary<char, string> Vervangingen = new()
    {
        ['&'] = "+", ['€'] = "EUR", ['ß'] = "ss", ['Æ'] = "AE", ['æ'] = "ae", ['Ø'] = "O", ['ø'] = "o",
        ['Œ'] = "OE", ['œ'] = "oe", ['Ð'] = "D", ['ð'] = "d", ['Þ'] = "Th", ['þ'] = "th", ['Ł'] = "L", ['ł'] = "l",
        ['‘'] = "'", ['’'] = "'", ['“'] = "'", ['”'] = "'", ['–'] = "-", ['—'] = "-", ['"'] = "'",
    };

    public static string Normaliseer(string tekst, int maximaleLengte)
    {
        ArgumentNullException.ThrowIfNull(tekst);
        var resultaat = new StringBuilder(tekst.Length);
        foreach (var teken in tekst.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(teken) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (Vervangingen.TryGetValue(teken, out var vervanging))
            {
                resultaat.Append(vervanging);
            }
            else if (char.IsWhiteSpace(teken))
            {
                resultaat.Append(' ');
            }
            else
            {
                resultaat.Append(IsToegestaan(teken) ? teken : '.');
            }
        }

        var zonderDubbeleSpaties = string.Join(' ', resultaat.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return zonderDubbeleSpaties.Length <= maximaleLengte ? zonderDubbeleSpaties : zonderDubbeleSpaties[..maximaleLengte].TrimEnd();
    }

    /// <summary>
    /// Kenmerken zoals het mandaat-ID: 1–35 tekens uit de SEPA-tekenset, zonder spaties, niet beginnend of eindigend
    /// met <c>/</c> en zonder <c>//</c>.
    /// </summary>
    public static bool IsGeldigKenmerk(string? kenmerk) =>
        kenmerk is { Length: >= 1 and <= 35 }
        && kenmerk.All(t => t != ' ' && IsToegestaan(t))
        && !kenmerk.StartsWith('/') && !kenmerk.EndsWith('/')
        && !kenmerk.Contains("//", StringComparison.Ordinal);

    private static bool IsToegestaan(char teken) => char.IsAsciiLetterOrDigit(teken) || Leestekens.Contains(teken, StringComparison.Ordinal);
}
