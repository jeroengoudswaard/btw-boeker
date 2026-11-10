using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.Domain.Financien.Boekhouding;

public enum RekeningSoort
{
    Balans = 1,

    /// <summary>Baten en lasten (W&amp;V).</summary>
    Resultaat = 2,
}

/// <summary>Bijzondere rol van een rekening, nodig voor automatische boekingen.</summary>
/// <summary>Of een resultaatrekening baten of lasten bevat; bepaalt de plaats in de jaarrekening, ook zonder saldo.</summary>
public enum ResultaatKant
{
    /// <summary>Balansrekening.</summary>
    Geen = 0,
    Baten = 1,
    Lasten = 2,
}

public enum RekeningRol
{
    Geen = 0,

    /// <summary>Het resultaat wordt bij de jaarafsluiting hierheen geboekt (bijv. 600 Algemene reserve).</summary>
    Reserve = 1,

    /// <summary>Tussenrekening voor overboekingen tussen eigen rekeningen.</summary>
    Kruisposten = 2,

    /// <summary>De kasrekening van het kasboek.</summary>
    Kas = 3,
}

/// <summary>Een groep rekeningen, zoals "Huisvesting"; bepaalt de indeling van balans en W&amp;V.</summary>
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
        Kant = StandaardKant(soort, nummer);
        IsActief = true;
    }

    public int Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public int Nummer { get; private set; }
    public string Naam { get; private set; }
    public Rubriek? Rubriek { get; private set; }
    public RekeningSoort Soort { get; private set; }
    public RekeningRol Rol { get; private set; }

    /// <summary>Baten of lasten bij een resultaatrekening; <see cref="ResultaatKant.Geen"/> bij een balansrekening.</summary>
    public ResultaatKant Kant { get; private set; }

    /// <summary>Een rekening met boekingen wordt nooit verwijderd, alleen inactief gemaakt.</summary>
    public bool IsActief { get; private set; }

    public void Wijzig(string naam, Rubriek? rubriek, RekeningSoort soort)
    {
        Naam = ValideerNaam(naam);
        Rubriek = rubriek;
        if (soort != Soort)
        {
            Kant = StandaardKant(soort, Nummer);
        }

        Soort = soort;
    }

    public void WijzigRol(RekeningRol rol) => Rol = rol;

    /// <exception cref="ArgumentException">Een balansrekening, of geen kant bij een resultaatrekening.</exception>
    public void WijzigKant(ResultaatKant kant)
    {
        if (Soort == RekeningSoort.Balans ? kant != ResultaatKant.Geen : kant == ResultaatKant.Geen)
        {
            throw new ArgumentException(Soort == RekeningSoort.Balans
                ? "Een balansrekening is geen baten- of lastenrekening."
                : "Kies baten of lasten.", nameof(kant));
        }

        Kant = kant;
    }

    /// <summary>Het gebruikelijke rekeningschema: rekeningen vanaf 8000 zijn baten, andere resultaatrekeningen lasten.</summary>
    public static ResultaatKant StandaardKant(RekeningSoort soort, int nummer) =>
        soort == RekeningSoort.Balans ? ResultaatKant.Geen : nummer is >= 8000 and <= 9999 ? ResultaatKant.Baten : ResultaatKant.Lasten;

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

    /// <summary>De eigen bankrekening van een bankdagboek.</summary>
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

    public bool IsOpen => Status == BoekjaarStatus.Open;

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
        Status = BoekjaarStatus.Open;
        AfgeslotenUtc = null;
    }
}
