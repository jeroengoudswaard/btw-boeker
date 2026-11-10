using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace BtwBoeker.Domain.Administraties;

/// <summary>Een RGB-kleur, uitgewisseld als hexcode in de vorm <c>#RRGGBB</c>.</summary>
public readonly record struct Kleur(byte R, byte G, byte B)
{
    public static Kleur Zwart { get; } = new(0, 0, 0);
    public static Kleur Wit { get; } = new(255, 255, 255);

    public static Kleur Parse(string invoer) =>
        TryParse(invoer, out var kleur)
            ? kleur
            : throw new FormatException($"'{invoer}' is geen geldige kleur; verwacht formaat #RRGGBB.");

    public static bool TryParse([NotNullWhen(true)] string? invoer, out Kleur kleur)
    {
        kleur = default;
        var tekst = invoer?.Trim();
        if (tekst is not { Length: 7 } || tekst[0] != '#'
            || !int.TryParse(tekst.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var waarde))
        {
            return false;
        }

        kleur = new Kleur((byte)(waarde >> 16), (byte)(waarde >> 8), (byte)waarde);
        return true;
    }

    /// <summary>Relatieve luminantie volgens WCAG 2.x.</summary>
    public double Luminantie() => 0.2126 * Lineair(R) + 0.7152 * Lineair(G) + 0.0722 * Lineair(B);

    /// <summary>Contrastverhouding volgens WCAG 2.x (1 t/m 21).</summary>
    public double Contrastverhouding(Kleur ander)
    {
        var (licht, donker) = (Math.Max(Luminantie(), ander.Luminantie()), Math.Min(Luminantie(), ander.Luminantie()));
        return (licht + 0.05) / (donker + 0.05);
    }

    /// <summary>Zwart of wit, afhankelijk van wat het best leesbaar is op deze kleur.</summary>
    public Kleur LeesbareTekstkleur() => Contrastverhouding(Wit) >= Contrastverhouding(Zwart) ? Wit : Zwart;

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";

    private static double Lineair(byte component)
    {
        var c = component / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
