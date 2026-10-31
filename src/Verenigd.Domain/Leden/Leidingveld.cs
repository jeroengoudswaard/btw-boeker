namespace Verenigd.Domain.Leden;

/// <summary>
/// De velden waarvoor de leiding in de webapp een voorstel mag doen (besluiten 33 en 34).
/// Dezelfde namen staan in de controle op <c>public.voorstellen.veld</c> op de server.
/// </summary>
public static class Leidingveld
{
    public const string Email = "email";
    public const string Telefoon = "telefoon";

    private static readonly string[] Voogdvelden = ["naam", "email", "telefoon", "mobiel"];

    public static IReadOnlyList<string> Alle { get; } =
        [Email, Telefoon, .. new[] { 1, 2 }.SelectMany(n => Voogdvelden.Select(v => $"voogd{n}_{v}"))];

    public static bool IsGeldig(string veld) => Alle.Contains(veld, StringComparer.Ordinal);

    public static string Omschrijving(string veld) => veld switch
    {
        Email => "E-mail",
        Telefoon => "Telefoon",
        _ when Voogdveld(veld) is ({ } n, { } v) => $"Voogd {n} · {v}",
        _ => veld,
    };

    /// <summary>De waarde zoals Verenigd die nu heeft, om te vergelijken met de oude waarde in het voorstel.</summary>
    public static string? HuidigeWaarde(Persoon persoon, string veld)
    {
        ArgumentNullException.ThrowIfNull(persoon);
        return veld switch
        {
            Email => persoon.Email,
            Telefoon => persoon.Telefoon,
            _ when Voogdveld(veld) is ({ } n, { } v) => persoon.Voogden.FirstOrDefault(x => x.Volgnummer == n) is { } voogd
                ? v switch
                {
                    "naam" => voogd.NaamOnbekend ? null : voogd.Naam,
                    "email" => voogd.Email,
                    "telefoon" => voogd.Telefoon,
                    _ => voogd.Mobiel,
                }
                : null,
            _ => throw new ArgumentException($"Onbekend veld '{veld}'.", nameof(veld)),
        };
    }

    internal static (int? Volgnummer, string? Deel) Voogdveld(string veld)
    {
        if (veld.Length > 6 && veld.StartsWith("voogd", StringComparison.Ordinal) && veld[6] == '_'
            && veld[5] is '1' or '2' && Voogdvelden.Contains(veld[7..], StringComparer.Ordinal))
        {
            return (veld[5] - '0', veld[7..]);
        }

        return (null, null);
    }
}
