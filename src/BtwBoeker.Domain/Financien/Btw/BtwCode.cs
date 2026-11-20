namespace BtwBoeker.Domain.Financien.Btw;

/// <summary>Waar de btw van een regel in de aangifte telt (besluiten 13, 15 en 49).</summary>
public enum BtwSoort
{
    Geen = 0,

    /// <summary>Omzet: rubriek 1a (21 %), 1b (9 %) of 1e (0 %).</summary>
    Omzet = 1,

    /// <summary>Voorbelasting: rubriek 5b.</summary>
    Voorbelasting = 2,

    /// <summary>Correctie voor privégebruik: rubriek 1d (besluiten 15 en 23).</summary>
    Privegebruik = 3,
}

/// <summary>
/// De btw-code van een journaalregel (besluit 49, fase B1). Een regel op een rekening met de rol <i>Te betalen btw</i> of
/// <i>Voorbelasting</i> is het btw-bedrag; elke andere regel met een code is de grondslag. Alleen 21, 9 en 0 % (besluit 10).
/// </summary>
public readonly record struct BtwCode
{
    public BtwCode(BtwSoort soort, int percentage)
    {
        if (!Enum.IsDefined(soort))
        {
            throw new ArgumentOutOfRangeException(nameof(soort), soort, "Onbekende btw-soort.");
        }

        if (soort == BtwSoort.Geen ? percentage != 0 : percentage is not (21 or 9 or 0))
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), percentage, "Het btw-tarief is 21, 9 of 0 procent.");
        }

        Soort = soort;
        Percentage = percentage;
    }

    /// <summary>Geen btw; ook de waarde van een regel van vóór de btw-integratie.</summary>
    public static BtwCode Geen { get; }

    public BtwSoort Soort { get; }

    public int Percentage { get; }

    public bool HeeftBtw => Soort != BtwSoort.Geen;
}
