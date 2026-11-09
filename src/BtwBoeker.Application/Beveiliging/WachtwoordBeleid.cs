namespace BtwBoeker.Application.Beveiliging;

/// <summary>Eisen aan het wachtwoord van een database.</summary>
public static class WachtwoordBeleid
{
    public const int MinimaleLengte = 12;

    /// <returns>De lijst met bezwaren; leeg als het wachtwoord voldoet.</returns>
    public static IReadOnlyList<string> Controleer(string? wachtwoord)
    {
        if (string.IsNullOrWhiteSpace(wachtwoord))
        {
            return ["Vul een wachtwoord in."];
        }

        if (wachtwoord.Length < MinimaleLengte)
        {
            return [$"Het wachtwoord moet minimaal {MinimaleLengte} tekens lang zijn. Tip: gebruik een korte zin."];
        }

        return [];
    }
}
