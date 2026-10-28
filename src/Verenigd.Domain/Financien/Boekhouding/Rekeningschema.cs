using Verenigd.Domain.Administraties;

namespace Verenigd.Domain.Financien.Boekhouding;

public enum RekeningSoort
{
    Balans = 1,

    /// <summary>Baten en lasten (W&amp;V).</summary>
    Resultaat = 2,
}

/// <summary>Bijzondere rol van een rekening, nodig voor automatische boekingen.</summary>
public enum RekeningRol
{
    Geen = 0,

    /// <summary>Het resultaat wordt bij de jaarafsluiting hierheen geboekt (bijv. 600 Algemene reserve).</summary>
    Reserve = 1,

    /// <summary>Tussenrekening voor overboekingen tussen eigen rekeningen (besluit 70).</summary>
    Kruisposten = 2,

    /// <summary>De kasrekening van het kasboek (besluit 65).</summary>
    Kas = 3,
}

/// <summary>Een groep rekeningen, zoals "Terreinkosten"; bepaalt de indeling van balans en W&amp;V (besluit 69).</summary>
public sealed class Rubriek
{
    public const int MaximaleNaamLengte = 100;

    public Rubriek(AdministratieSoort administratie, string naam, int volgorde)
    {
        Administratie = administratie;
        Naam = Valideer(naam);
        Volgorde = volgorde;
    }

    public int Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public string Naam { get; private set; }
    public int Volgorde { get; private set; }

    public void Wijzig(string naam, int volgorde)
    {
        Naam = Valideer(naam);
        Volgorde = volgorde;
    }

    private static string Valideer(string naam)
    {
        var schoon = naam?.Trim();
        return string.IsNullOrEmpty(schoon) || schoon.Length > MaximaleNaamLengte
            ? throw new ArgumentException($"De naam is verplicht en maximaal {MaximaleNaamLengte} tekens.", nameof(naam))
            : schoon;
    }
}

/// <summary>Een grootboekrekening van één administratie.</summary>
public sealed class Grootboekrekening
{
    public const int MaximaleNaamLengte = 100;

    private Grootboekrekening()
    {
        Naam = null!;
    }

    public Grootboekrekening(
        AdministratieSoort administratie, int nummer, string naam, RekeningSoort soort, Rubriek? rubriek, RekeningRol rol = RekeningRol.Geen)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nummer, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(nummer, 99999);
        Administratie = administratie;
        Nummer = nummer;
        Naam = ValideerNaam(naam);
        Soort = soort;
        Rubriek = rubriek;
        Rol = rol;
        IsActief = true;
    }

    public int Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public int Nummer { get; private set; }
    public string Naam { get; private set; }
    public Rubriek? Rubriek { get; private set; }
    public RekeningSoort Soort { get; private set; }
    public RekeningRol Rol { get; private set; }

    /// <summary>Een rekening met boekingen wordt nooit verwijderd, alleen inactief gemaakt.</summary>
    public bool IsActief { get; private set; }

    public void Wijzig(string naam, Rubriek? rubriek, RekeningSoort soort)
    {
        Naam = ValideerNaam(naam);
        Rubriek = rubriek;
        Soort = soort;
    }

    public void WijzigRol(RekeningRol rol) => Rol = rol;

    public void Deactiveer() => IsActief = false;

    public void Activeer() => IsActief = true;

    public override string ToString() => $"{Nummer} {Naam}";

    private static string ValideerNaam(string naam)
    {
        var schoon = naam?.Trim();
        return string.IsNullOrEmpty(schoon) || schoon.Length > MaximaleNaamLengte
            ? throw new ArgumentException($"De naam is verplicht en maximaal {MaximaleNaamLengte} tekens.", nameof(naam))
            : schoon;
    }
}

public enum DagboekSoort
{
    Memoriaal = 1,
    Kas = 2,
    Bank = 3,
    Inkoop = 4,
    Verkoop = 5,
}

/// <summary>Een dagboek; een post in een kas- of bankdagboek bevat altijd een regel op de bijbehorende rekening.</summary>
public sealed class Dagboek
{
    private Dagboek()
    {
        Code = null!;
        Naam = null!;
    }

    public Dagboek(AdministratieSoort administratie, DagboekSoort soort, string code, string naam, Grootboekrekening? rekening, Iban? iban = null)
    {
        if (iban is not null && soort != DagboekSoort.Bank)
        {
            throw new ArgumentException("Alleen een bankdagboek heeft een IBAN.", nameof(iban));
        }

        var schoon = code?.Trim().ToUpperInvariant();
        if (schoon is not { Length: >= 2 and <= 5 } || !schoon.All(char.IsAsciiLetterOrDigit))
        {
            throw new ArgumentException("Een dagboekcode heeft 2–5 letters of cijfers, zoals MEM.", nameof(code));
        }

        if (soort is DagboekSoort.Kas or DagboekSoort.Bank && rekening is null)
        {
            throw new ArgumentException("Een kas- of bankdagboek hoort bij een grootboekrekening.", nameof(rekening));
        }

        Administratie = administratie;
        Soort = soort;
        Code = schoon;
        Naam = string.IsNullOrWhiteSpace(naam) ? schoon : naam.Trim();
        Rekening = rekening;
        Iban = iban;
    }

    public int Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public DagboekSoort Soort { get; private set; }

    /// <summary>De eigen bankrekening van een bankdagboek (besluit 76).</summary>
    public Iban? Iban { get; private set; }
    public string Code { get; private set; }
    public string Naam { get; private set; }
    public Grootboekrekening? Rekening { get; private set; }

    public override string ToString() => Naam;
}

public enum BoekjaarStatus
{
    Open = 1,
    Afgesloten = 2,
}

/// <summary>Een boekjaar (kalenderjaar, besluit 15). In een afgesloten jaar wordt niets geboekt.</summary>
public sealed class Boekjaar
{
    public Boekjaar(AdministratieSoort administratie, int jaar)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(jaar, 2000);
        Administratie = administratie;
        Jaar = jaar;
        Status = BoekjaarStatus.Open;
    }

    public int Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public int Jaar { get; private set; }
    public BoekjaarStatus Status { get; private set; }
    public DateTime? AfgeslotenUtc { get; private set; }

    /// <summary>Overgenomen uit Excel als losse T-rekeningen (besluit 122): alleen-lezen en nooit in het grootboek.</summary>
    public bool IsHistorisch { get; private set; }

    public bool IsOpen => Status == BoekjaarStatus.Open;

    /// <summary>Een afgesloten, alleen-lezen boekjaar met de historie uit de Excel-boekhouding.</summary>
    public static Boekjaar Historisch(AdministratieSoort administratie, int jaar, DateTime nuUtc) =>
        new(administratie, jaar) { IsHistorisch = true, Status = BoekjaarStatus.Afgesloten, AfgeslotenUtc = nuUtc };

    public void Sluit(DateTime nuUtc)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException($"Boekjaar {Jaar} is al afgesloten.");
        }

        Status = BoekjaarStatus.Afgesloten;
        AfgeslotenUtc = nuUtc;
    }

    public void Heropen()
    {
        if (IsHistorisch)
        {
            throw new InvalidOperationException($"Boekjaar {Jaar} is historisch (overgenomen uit Excel) en kan niet worden heropend.");
        }

        Status = BoekjaarStatus.Open;
        AfgeslotenUtc = null;
    }
}
