using System.Globalization;
using Verenigd.Domain.Administraties;

namespace Verenigd.Domain.Financien.Boekhouding;

public enum PostStatus
{
    Concept = 1,
    Geboekt = 2,
}

public enum PostSoort
{
    Gewoon = 1,
    Openingsbalans = 2,

    /// <summary>Automatisch bij de jaarafsluiting: resultaat naar de reserve.</summary>
    Afsluiting = 3,

    Tegenboeking = 4,
}

/// <summary>Invoer voor één regel: óf debet óf credit, groter dan nul.</summary>
public sealed record RegelInvoer(Grootboekrekening Rekening, string? Omschrijving, Bedrag Debet, Bedrag Credit)
{
    public void Valideer()
    {
        ArgumentNullException.ThrowIfNull(Rekening);
        if (Debet.Centen < 0 || Credit.Centen < 0)
        {
            throw new ArgumentException("Bedragen zijn nooit negatief; gebruik de andere kolom.");
        }

        if ((Debet.Centen > 0) == (Credit.Centen > 0))
        {
            throw new ArgumentException("Vul per regel óf debet óf credit in.");
        }
    }
}

/// <summary>
/// Een journaalpost (besluit 71): <see cref="PostStatus.Concept"/> is nog te wijzigen; een geboekte post is onveranderlijk
/// en krijgt dan pas een doorlopend nummer per dagboek per jaar. Corrigeren gaat met een tegenboeking.
/// </summary>
public sealed class Journaalpost
{
    public const int MaximaleOmschrijvingLengte = 200;

    private readonly List<Journaalregel> _regels = [];

    private Journaalpost()
    {
        Dagboek = null!;
        Omschrijving = null!;
    }

    public Journaalpost(AdministratieSoort administratie, Dagboek dagboek, DateOnly datum, string omschrijving, PostSoort soort = PostSoort.Gewoon)
    {
        ArgumentNullException.ThrowIfNull(dagboek);
        Id = Guid.NewGuid();
        Administratie = administratie;
        Dagboek = dagboek;
        Datum = datum;
        Jaar = datum.Year;
        Omschrijving = SchoonOmschrijving(omschrijving);
        Soort = soort;
        Status = PostStatus.Concept;
    }

    public Guid Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public Dagboek Dagboek { get; private set; }
    public DateOnly Datum { get; private set; }

    /// <summary>Het boekjaar (= jaar van de datum); opgeslagen voor de unieke nummering per dagboek per jaar.</summary>
    public int Jaar { get; private set; }

    public string Omschrijving { get; private set; }
    public PostStatus Status { get; private set; }
    public PostSoort Soort { get; private set; }
    public int? Volgnummer { get; private set; }
    public DateTime? GeboektUtc { get; private set; }

    /// <summary>De post die door deze tegenboeking wordt gecorrigeerd.</summary>
    public Guid? TegenboekingVan { get; private set; }

    public IReadOnlyList<Journaalregel> Regels => _regels;

    public string Nummer => Volgnummer is { } n
        ? $"{Dagboek.Code}-{Datum.Year.ToString(CultureInfo.InvariantCulture)}-{n.ToString("D4", CultureInfo.InvariantCulture)}"
        : "(concept)";

    public Bedrag TotaalDebet => _regels.Select(r => r.Debet).Som();

    public Bedrag TotaalCredit => _regels.Select(r => r.Credit).Som();

    public Bedrag Verschil => new(TotaalDebet.Centen - TotaalCredit.Centen);

    public bool IsInEvenwicht => Verschil == Bedrag.Nul;

    public bool IsGeboekt => Status == PostStatus.Geboekt;

    public void Wijzig(Dagboek dagboek, DateOnly datum, string omschrijving, IEnumerable<RegelInvoer> regels)
    {
        ArgumentNullException.ThrowIfNull(dagboek);
        ArgumentNullException.ThrowIfNull(regels);
        if (IsGeboekt)
        {
            throw new InvalidOperationException("Een geboekte post kan niet worden gewijzigd; maak een tegenboeking.");
        }

        var lijst = regels.ToList();
        foreach (var regel in lijst)
        {
            regel.Valideer();
        }

        Dagboek = dagboek;
        Datum = datum;
        Jaar = datum.Year;
        Omschrijving = SchoonOmschrijving(omschrijving);
        _regels.Clear();
        _regels.AddRange(lijst.Select(r => new Journaalregel(r.Rekening, r.Omschrijving, r.Debet, r.Credit)));
    }

    /// <summary>Redenen waarom de post (nog) niet kan worden geboekt; leeg als alles klopt.</summary>
    public IReadOnlyList<string> Controleer()
    {
        var fouten = new List<string>();
        if (_regels.Count < 2)
        {
            fouten.Add("Een post heeft minstens twee regels.");
        }

        if (!IsInEvenwicht)
        {
            fouten.Add($"De post is niet in evenwicht (verschil {Verschil}).");
        }

        if (TotaalDebet == Bedrag.Nul)
        {
            fouten.Add("Het totaal van de post is nul.");
        }

        foreach (var rekening in _regels.Select(r => r.Rekening).Distinct())
        {
            if (rekening.Administratie != Administratie)
            {
                fouten.Add($"Rekening {rekening} hoort bij een andere administratie.");
            }
            else if (!rekening.IsActief)
            {
                fouten.Add($"Rekening {rekening} is inactief.");
            }
        }

        if (Dagboek.Rekening is { } dagboekrekening && !_regels.Exists(r => ReferenceEquals(r.Rekening, dagboekrekening) || (r.Rekening.Id != 0 && r.Rekening.Id == dagboekrekening.Id)))
        {
            fouten.Add($"Een post in {Dagboek.Naam} bevat altijd een regel op rekening {dagboekrekening}.");
        }

        return fouten;
    }

    /// <exception cref="InvalidOperationException">Al geboekt of <see cref="Controleer"/> geeft fouten.</exception>
    public void Boek(int volgnummer, DateTime nuUtc)
    {
        if (IsGeboekt)
        {
            throw new InvalidOperationException("Deze post is al geboekt.");
        }

        var fouten = Controleer();
        if (fouten.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", fouten));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(volgnummer, 1);
        Volgnummer = volgnummer;
        Status = PostStatus.Geboekt;
        GeboektUtc = nuUtc;
    }

    /// <summary>Een nieuwe (concept)post die deze geboekte post precies tenietdoet.</summary>
    public Journaalpost MaakTegenboeking(DateOnly datum)
    {
        if (!IsGeboekt)
        {
            throw new InvalidOperationException("Alleen een geboekte post kan worden tegengeboekt; een concept kunt u wijzigen of verwijderen.");
        }

        var tegen = new Journaalpost(Administratie, Dagboek, datum, $"Tegenboeking {Nummer}: {Omschrijving}", PostSoort.Tegenboeking)
        {
            TegenboekingVan = Id,
        };
        tegen.Wijzig(Dagboek, datum, tegen.Omschrijving, _regels.Select(r => new RegelInvoer(r.Rekening, r.Omschrijving, r.Credit, r.Debet)));
        return tegen;
    }

    private static string SchoonOmschrijving(string omschrijving)
    {
        var schoon = omschrijving?.Trim() ?? string.Empty;
        return schoon.Length > MaximaleOmschrijvingLengte ? schoon[..MaximaleOmschrijvingLengte] : schoon;
    }
}

public sealed class Journaalregel
{
    private Journaalregel()
    {
        Rekening = null!;
    }

    internal Journaalregel(Grootboekrekening rekening, string? omschrijving, Bedrag debet, Bedrag credit)
    {
        Rekening = rekening;
        Omschrijving = string.IsNullOrWhiteSpace(omschrijving) ? null : omschrijving.Trim();
        Debet = debet;
        Credit = credit;
    }

    public int Id { get; private set; }
    public Grootboekrekening Rekening { get; private set; }
    public string? Omschrijving { get; private set; }
    public Bedrag Debet { get; private set; }
    public Bedrag Credit { get; private set; }

    /// <summary>Debet positief, credit negatief.</summary>
    public long Saldo => Debet.Centen - Credit.Centen;
}
