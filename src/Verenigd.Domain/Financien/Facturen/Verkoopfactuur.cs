using System.Globalization;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Domain.Financien.Relaties;

namespace Verenigd.Domain.Financien.Facturen;

public enum FactuurStatus
{
    Concept = 1,
    Definitief = 2,
}

public sealed record FactuurregelInvoer(string Omschrijving, decimal Aantal, Bedrag Prijs, Grootboekrekening Rekening);

public sealed class Factuurregel
{
    public const int MaximaleOmschrijvingLengte = 200;

    private Factuurregel()
    {
        Omschrijving = null!;
        Rekening = null!;
    }

    internal Factuurregel(string omschrijving, decimal aantal, Bedrag prijs, Grootboekrekening rekening)
    {
        Omschrijving = omschrijving;
        Aantal = aantal;
        Prijs = prijs;
        Rekening = rekening;
    }

    public int Id { get; private set; }
    public string Omschrijving { get; private set; }
    public decimal Aantal { get; private set; }

    /// <summary>Prijs per stuk in centen.</summary>
    public Bedrag Prijs { get; private set; }

    /// <summary>De opbrengstrekening.</summary>
    public Grootboekrekening Rekening { get; private set; }

    /// <summary>Aantal × prijs, afgerond op hele centen (half naar boven, weg van nul).</summary>
    public Bedrag Bedrag => new((long)Math.Round(Aantal * Prijs.Centen, MidpointRounding.AwayFromZero));
}

/// <summary>
/// Een verkoopfactuur (besluiten 66, 94 en 105–107). Een concept is vrij te wijzigen; een definitieve factuur krijgt een
/// nummer (bijvoorbeeld <c>V2026-001</c>), wordt geboekt en is daarna onveranderlijk. Corrigeren gaat met een creditfactuur.
/// </summary>
public sealed class Verkoopfactuur
{
    private readonly List<Factuurregel> _regels = [];

    private Verkoopfactuur()
    {
    }

    public Verkoopfactuur(AdministratieSoort administratie, Relatie relatie, DateOnly datum, int termijn)
    {
        ArgumentNullException.ThrowIfNull(relatie);
        Id = Guid.NewGuid();
        Administratie = administratie;
        Status = FactuurStatus.Concept;
        Wijzig(relatie, datum, termijn, []);
    }

    public Guid Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public FactuurStatus Status { get; private set; }
    public Guid RelatieId { get; private set; }
    public DateOnly Factuurdatum { get; private set; }

    /// <summary>Betaaltermijn in dagen.</summary>
    public int Termijn { get; private set; }

    public int? Jaar { get; private set; }
    public int? Volgnummer { get; private set; }

    /// <summary>Het factuurnummer; <c>null</c> zolang de factuur concept is.</summary>
    public string? Nummer { get; private set; }

    /// <summary>Naam en adres van de ontvanger, vastgelegd bij Definitief maken.</summary>
    public string? Ontvanger { get; private set; }

    /// <summary>De gecrediteerde factuur, bij een creditfactuur.</summary>
    public Guid? CreditVan { get; private set; }

    /// <summary>De journaalpost van de definitieve factuur.</summary>
    public Guid? PostId { get; private set; }

    public IReadOnlyList<Factuurregel> Regels => _regels;

    public Bedrag Totaal => new(_regels.Sum(r => r.Bedrag.Centen));

    public DateOnly Vervaldatum => Factuurdatum.AddDays(Termijn);

    public bool IsCreditfactuur => CreditVan is not null;

    public bool IsDefinitief => Status == FactuurStatus.Definitief;

    public static string Voorvoegsel(AdministratieSoort administratie) => administratie == AdministratieSoort.Stichting ? "S" : "V";

    public void Wijzig(Relatie relatie, DateOnly datum, int termijn, IEnumerable<FactuurregelInvoer> regels)
    {
        ArgumentNullException.ThrowIfNull(relatie);
        ArgumentNullException.ThrowIfNull(regels);
        if (IsDefinitief)
        {
            throw new InvalidOperationException("Een definitieve factuur kan niet worden gewijzigd; maak een creditfactuur.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(termijn);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(termijn, 365);
        var lijst = regels.ToList();
        RelatieId = relatie.Id;
        Factuurdatum = datum;
        Termijn = termijn;
        _regels.Clear();
        _regels.AddRange(lijst.Select(r => new Factuurregel(Kort((r.Omschrijving ?? string.Empty).Trim()), r.Aantal, r.Prijs, r.Rekening)));
    }

    /// <summary>Wat het definitief maken in de weg staat; leeg als alles klopt.</summary>
    public IReadOnlyList<string> Controleer()
    {
        var fouten = new List<string>();
        if (_regels.Count == 0)
        {
            fouten.Add("Een factuur heeft minstens één regel.");
        }

        if (_regels.Exists(r => r.Omschrijving.Length == 0))
        {
            fouten.Add("Elke regel heeft een omschrijving.");
        }

        if (_regels.Exists(r => r.Aantal == 0))
        {
            fouten.Add("Een regel met aantal 0 heeft geen zin.");
        }

        if (_regels.Exists(r => r.Rekening.Administratie != Administratie || !r.Rekening.IsActief))
        {
            fouten.Add("Elke regel heeft een actieve opbrengstrekening van deze administratie.");
        }

        if (IsCreditfactuur ? Totaal.Centen >= 0 : Totaal.Centen <= 0)
        {
            fouten.Add(IsCreditfactuur ? "Het totaal van een creditfactuur is negatief." : "Het totaal van een factuur is positief.");
        }

        return fouten;
    }

    /// <exception cref="InvalidOperationException">Al definitief of <see cref="Controleer"/> geeft fouten.</exception>
    public void MaakDefinitief(int volgnummer, Relatie relatie, Guid postId)
    {
        ArgumentNullException.ThrowIfNull(relatie);
        if (IsDefinitief)
        {
            throw new InvalidOperationException("Deze factuur is al definitief.");
        }

        if (Controleer() is { Count: > 0 } fouten)
        {
            throw new InvalidOperationException(string.Join(" ", fouten));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(volgnummer, 1);
        Jaar = Factuurdatum.Year;
        Volgnummer = volgnummer;
        Nummer = string.Create(CultureInfo.InvariantCulture, $"{Voorvoegsel(Administratie)}{Jaar}-{volgnummer:D3}");
        Ontvanger = string.Join('\n', new[] { relatie.Naam }.Concat(relatie.Adresregels));
        PostId = postId;
        Status = FactuurStatus.Definitief;
    }

    /// <summary>Een concept-creditfactuur die deze definitieve factuur precies tenietdoet.</summary>
    public Verkoopfactuur MaakCreditfactuur(DateOnly datum)
    {
        if (!IsDefinitief || IsCreditfactuur)
        {
            throw new InvalidOperationException("Alleen een definitieve factuur kan worden gecrediteerd.");
        }

        var credit = new Verkoopfactuur
        {
            Id = Guid.NewGuid(),
            Administratie = Administratie,
            Status = FactuurStatus.Concept,
            RelatieId = RelatieId,
            Factuurdatum = datum,
            Termijn = 0,
            CreditVan = Id,
        };
        credit._regels.AddRange(_regels.Select(r => new Factuurregel(
            Kort($"Creditering {Nummer}: {r.Omschrijving}"), -r.Aantal, r.Prijs, r.Rekening)));
        return credit;
    }

    private static string Kort(string tekst) => tekst.Length > Factuurregel.MaximaleOmschrijvingLengte ? tekst[..Factuurregel.MaximaleOmschrijvingLengte] : tekst;
}

/// <summary>De gegevens van de afzender op een factuur, per administratie (besluit 112).</summary>
public sealed class Factuurinstellingen
{
    public const int MaximaleLogoGrootte = 2 * 1024 * 1024;

    private Factuurinstellingen()
    {
    }

    public Factuurinstellingen(AdministratieSoort administratie)
    {
        Administratie = administratie;
        Termijn = 14;
    }

    public int Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public string? Naam { get; private set; }
    public string? Straat { get; private set; }
    public string? Postcode { get; private set; }
    public string? Plaats { get; private set; }
    public string? Kvk { get; private set; }
    public string? Iban { get; private set; }
    public string? Email { get; private set; }
    public string? Telefoon { get; private set; }
    public int Termijn { get; private set; }
    public string? Voettekst { get; private set; }

#pragma warning disable CA1819 // Een logo is een blok bytes; een kopie per lezing heeft geen zin.
    public byte[]? Logo { get; private set; }
#pragma warning restore CA1819

    public string? LogoType { get; private set; }

    /// <exception cref="ArgumentException">Ongeldig IBAN of termijn.</exception>
    public void Wijzig(string? naam, string? straat, string? postcode, string? plaats, string? kvk, string? iban, string? email, string? telefoon,
        int termijn, string? voettekst)
    {
        string? schoonIban = null;
        if (Financien.Iban.TryMaak(iban, out var i))
        {
            schoonIban = i.ControlegetalGeldig ? i.Waarde : throw new ArgumentException("Het IBAN is ongeldig (controlegetal klopt niet).", nameof(iban));
        }

        if (termijn is < 0 or > 365)
        {
            throw new ArgumentException("De betaaltermijn is 0 tot en met 365 dagen.", nameof(termijn));
        }

        Naam = Leeg(naam);
        Straat = Leeg(straat);
        Postcode = Leeg(postcode)?.ToUpperInvariant();
        Plaats = Leeg(plaats);
        Kvk = Leeg(kvk);
        Iban = schoonIban;
        Email = Leeg(email);
        Telefoon = Leeg(telefoon);
        Termijn = termijn;
        Voettekst = Leeg(voettekst);
    }

    /// <summary>Alleen PNG of JPG, tot <see cref="MaximaleLogoGrootte"/>; <c>null</c> wist het logo.</summary>
    public void ZetLogo(byte[]? logo)
    {
        if (logo is null)
        {
            Logo = null;
            LogoType = null;
            return;
        }

        if (logo.Length > MaximaleLogoGrootte)
        {
            throw new ArgumentException("Het logo is groter dan 2 MB.", nameof(logo));
        }

        LogoType = logo is [0x89, 0x50, 0x4E, 0x47, ..] ? "image/png"
            : logo is [0xFF, 0xD8, 0xFF, ..] ? "image/jpeg"
            : throw new ArgumentException("Kies een logo in PNG- of JPG-formaat.", nameof(logo));
        Logo = logo;
    }

    private static string? Leeg(string? tekst) => string.IsNullOrWhiteSpace(tekst) ? null : tekst.Trim();
}

/// <summary>De inhoud van een EPC-QR-code ("girocode", EPC069-12 versie 002) voor een SEPA-overboeking (besluit 110).</summary>
public static class EpcQr
{
    public static string Inhoud(string naam, Iban iban, Bedrag bedrag, string kenmerk)
    {
        if (bedrag.Centen is < 1 or > 99_999_999_999)
        {
            throw new ArgumentOutOfRangeException(nameof(bedrag), "Het bedrag ligt tussen € 0,01 en € 999.999.999,99.");
        }

        var regels = new[]
        {
            "BCD", "002", "1", "SCT", string.Empty,
            SepaTekst.Normaliseer(naam, 70), iban.Waarde, "EUR" + bedrag.SepaNotatie, string.Empty, string.Empty,
            SepaTekst.Normaliseer(kenmerk, 140),
        };
        return string.Join('\n', regels);
    }
}
