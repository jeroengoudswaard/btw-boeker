using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Verenigd.Domain.Financien;

/// <summary>
/// Een IBAN, opgeslagen zonder spaties en in hoofdletters (besluit 29). Een ongeldig controlegetal maakt de
/// waarde niet onbruikbaar: SOL3 is leidend, de import geeft dan een waarschuwing.
/// </summary>
public readonly record struct Iban
{
    private Iban(string waarde) => Waarde = waarde;

    public string Waarde { get; }

    /// <summary>Klopt het controlegetal (ISO 13616, modulo 97)?</summary>
    public bool ControlegetalGeldig => HeeftGeldigControlegetal(Waarde);

    public static Iban Maak(string invoer) =>
        TryMaak(invoer, out var iban) ? iban : throw new ArgumentException("Een IBAN mag niet leeg zijn.", nameof(invoer));

    public static bool TryMaak([NotNullWhen(true)] string? invoer, out Iban iban)
    {
        iban = default;
        if (string.IsNullOrWhiteSpace(invoer))
        {
            return false;
        }

        var schoon = new StringBuilder(invoer.Length);
        foreach (var teken in invoer)
        {
            if (!char.IsWhiteSpace(teken))
            {
                schoon.Append(char.ToUpperInvariant(teken));
            }
        }

        iban = new Iban(schoon.ToString());
        return true;
    }

    /// <summary>In groepen van vier tekens, zoals op een bankpas.</summary>
    public override string ToString()
    {
        var waarde = Waarde ?? string.Empty;
        var tekst = new StringBuilder(waarde.Length + (waarde.Length / 4));
        for (var i = 0; i < waarde.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                tekst.Append(' ');
            }

            tekst.Append(waarde[i]);
        }

        return tekst.ToString();
    }

    private static bool HeeftGeldigControlegetal(string? waarde)
    {
        if (waarde is not { Length: >= 15 and <= 34 }
            || !char.IsAsciiLetterUpper(waarde[0]) || !char.IsAsciiLetterUpper(waarde[1])
            || !char.IsAsciiDigit(waarde[2]) || !char.IsAsciiDigit(waarde[3]))
        {
            return false;
        }

        // Landcode en controlegetal achteraan, letters als 10..35, dan stapsgewijs modulo 97.
        var rest = 0;
        foreach (var teken in waarde.AsSpan(4).ToString() + waarde[..4])
        {
            int cijfers;
            if (char.IsAsciiDigit(teken))
            {
                cijfers = teken - '0';
                rest = ((rest * 10) + cijfers) % 97;
            }
            else if (char.IsAsciiLetterUpper(teken))
            {
                cijfers = teken - 'A' + 10;
                rest = ((rest * 100) + cijfers) % 97;
            }
            else
            {
                return false;
            }
        }

        return rest == 1;
    }
}
