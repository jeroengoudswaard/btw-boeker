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

/// <summary>Besluit 50: welke soort btw bij een rekening hoort en op welke btw-rekening het btw-bedrag komt.</summary>
public static class BtwRegels
{
    /// <summary>Baten is omzet; lasten en balans is voorbelasting.</summary>
    public static BtwSoort SoortVoor(Boekhouding.Grootboekrekening rekening)
    {
        ArgumentNullException.ThrowIfNull(rekening);
        return rekening.Soort == Boekhouding.RekeningSoort.Resultaat && rekening.Kant == Boekhouding.ResultaatKant.Baten
            ? BtwSoort.Omzet
            : BtwSoort.Voorbelasting;
    }

    /// <summary>De rol van de btw-rekening: voorbelasting, of te betalen btw bij omzet en privégebruik.</summary>
    public static Boekhouding.RekeningRol RolVoor(BtwSoort soort) =>
        soort == BtwSoort.Voorbelasting ? Boekhouding.RekeningRol.Voorbelasting : Boekhouding.RekeningRol.TeBetalenBtw;

    /// <summary>De actieve rekening met die rol, of <c>null</c> als er geen is aangewezen.</summary>
    public static Boekhouding.Grootboekrekening? Btwrekening(IEnumerable<Boekhouding.Grootboekrekening> rekeningen, BtwSoort soort)
    {
        ArgumentNullException.ThrowIfNull(rekeningen);
        var rol = RolVoor(soort);
        return rekeningen.FirstOrDefault(r => r.Rol == rol && r.IsActief);
    }
}
