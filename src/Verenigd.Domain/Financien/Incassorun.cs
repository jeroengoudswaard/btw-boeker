using Verenigd.Domain.Gedeeld;

namespace Verenigd.Domain.Financien;

public enum IncassorunStatus
{
    Definitief = 1,

    /// <summary>Niet door de bank uitgevoerd; de maanden zijn weer vrij (besluit 60).</summary>
    Geannuleerd = 2,
}

/// <summary>
/// Een definitieve incassorun voor één maand. Een run wordt pas opgeslagen als hij definitief is; daarna is de inhoud
/// onveranderlijk. Posten bevatten een momentopname van de betaler.
/// </summary>
public sealed class Incassorun
{
    private readonly List<Incassopost> _posten = [];

    private Incassorun()
    {
    }

    public Incassorun(Maand maand, DateOnly uitvoerdatum, DateTime definitiefUtc)
        : this(Guid.NewGuid(), maand, uitvoerdatum, definitiefUtc)
    {
    }

    internal Incassorun(Guid id, Maand maand, DateOnly uitvoerdatum, DateTime definitiefUtc)
    {
        Id = id;
        Maand = maand;
        Uitvoerdatum = uitvoerdatum;
        DefinitiefUtc = definitiefUtc;
        Status = IncassorunStatus.Definitief;
    }

    public Guid Id { get; private set; }
    public Maand Maand { get; private set; }
    public DateOnly Uitvoerdatum { get; private set; }
    public DateTime DefinitiefUtc { get; private set; }
    public IncassorunStatus Status { get; private set; }
    public DateTime? GeannuleerdUtc { get; private set; }

    /// <summary>SHA-256 van het pain.008-bestand; bij opnieuw opslaan moet de inhoud gelijk zijn.</summary>
    public string? BestandHash { get; private set; }

    public IReadOnlyList<Incassopost> Posten => _posten;

    public Bedrag Totaal => _posten.Select(p => p.Bedrag).Som();

    public int AantalPosten => _posten.Count;

    /// <summary>Kort, uniek kenmerk voor bericht- en batch-ID's in het SEPA-bestand.</summary>
    public string Kenmerk => "VER" + Id.ToString("N")[..12].ToUpperInvariant();

    /// <param name="herincassoVan">Bij een herincasso (besluit 100): het EndToEndId van de gestorneerde post.</param>
    public Incassopost VoegPostToe(
        Guid betalerId, Iban iban, string rekeninghouder, string mandaatkenmerk, DateOnly ondertekendOp, string omschrijving,
        string? herincassoVan = null)
    {
        if (BestandHash is not null)
        {
            throw new InvalidOperationException("Het bestand van deze run is al gemaakt; de run kan niet meer worden gewijzigd.");
        }

        var post = new Incassopost(this, _posten.Count + 1, betalerId, iban, rekeninghouder, mandaatkenmerk, ondertekendOp, omschrijving)
        {
            HerincassoVan = string.IsNullOrWhiteSpace(herincassoVan) ? null : herincassoVan,
        };
        _posten.Add(post);
        return post;
    }

    public void LegBestandVast(string hash)
    {
        if (BestandHash is not null && BestandHash != hash)
        {
            throw new InvalidOperationException("Het bestand wijkt af van het eerder gemaakte bestand van deze run.");
        }

        BestandHash = hash;
    }

    public void Annuleer(DateTime nuUtc)
    {
        if (Status != IncassorunStatus.Definitief)
        {
            throw new InvalidOperationException("Deze run is al geannuleerd.");
        }

        Status = IncassorunStatus.Geannuleerd;
        GeannuleerdUtc = nuUtc;
        foreach (var regel in _posten.SelectMany(p => p.Regels))
        {
            regel.Annuleer();
        }
    }
}

/// <summary>Eén incasso bij één betaler, met een momentopname van IBAN, naam en machtiging.</summary>
public sealed class Incassopost
{
    private readonly List<Postregel> _regels = [];

    private Incassopost()
    {
        EndToEndId = null!;
        Rekeninghouder = null!;
        Mandaatkenmerk = null!;
        Omschrijving = null!;
    }

    internal Incassopost(
        Incassorun run, int volgnummer, Guid betalerId, Iban iban, string rekeninghouder, string mandaatkenmerk, DateOnly ondertekendOp,
        string omschrijving)
    {
        EndToEndId = $"{run.Kenmerk}-{volgnummer:D4}";
        BetalerId = betalerId;
        Iban = iban;
        Rekeninghouder = SepaTekst.Normaliseer(rekeninghouder, 70);
        Mandaatkenmerk = SepaTekst.IsGeldigKenmerk(mandaatkenmerk)
            ? mandaatkenmerk
            : throw new ArgumentException("Ongeldig mandaatkenmerk.", nameof(mandaatkenmerk));
        OndertekendOp = ondertekendOp;
        Omschrijving = SepaTekst.Normaliseer(omschrijving, 140);
    }

    public int Id { get; private set; }
    public string EndToEndId { get; private set; }
    public Guid BetalerId { get; private set; }
    public Iban Iban { get; private set; }
    public string Rekeninghouder { get; private set; }
    public string Mandaatkenmerk { get; private set; }
    public DateOnly OndertekendOp { get; private set; }
    public string Omschrijving { get; private set; }
    public IReadOnlyList<Postregel> Regels => _regels;

    public Bedrag Bedrag => _regels.Select(r => r.Bedrag).Som();

    /// <summary>Het EndToEndId van de gestorneerde post die deze post opnieuw incasseert, of <c>null</c>.</summary>
    public string? HerincassoVan { get; internal init; }

    public bool IsHerincasso => HerincassoVan is not null;

    /// <summary>De vordering die deze post int: bij een herincasso die van de oorspronkelijke post.</summary>
    public string Vordering => HerincassoVan ?? EndToEndId;

    public Postregel VoegRegelToe(Lidnummer lidnummer, Maand maand, string categorie, Bedrag bedrag)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bedrag.Centen, 0L, nameof(bedrag));
        var regel = new Postregel(lidnummer, maand, categorie, bedrag) { IsHerincasso = IsHerincasso };
        _regels.Add(regel);
        return regel;
    }
}

/// <summary>Eén lid × één maand. Lidnummer + maand is uniek over alle niet-geannuleerde runs (besluit 55).</summary>
public sealed class Postregel
{
    private Postregel()
    {
        Categorie = null!;
    }

    internal Postregel(Lidnummer lidnummer, Maand maand, string categorie, Bedrag bedrag)
    {
        Lidnummer = lidnummer;
        Maand = maand;
        Categorie = categorie;
        Bedrag = bedrag;
    }

    public int Id { get; private set; }
    public Lidnummer Lidnummer { get; private set; }
    public Maand Maand { get; private set; }
    public string Categorie { get; private set; }
    public Bedrag Bedrag { get; private set; }
    public bool IsGeannuleerd { get; private set; }

    /// <summary>
    /// Een herincasso van een al gevorderde maand: telt niet mee voor "nooit twee keer" (besluit 55) en levert geen nieuwe
    /// opbrengst op (besluit 100).
    /// </summary>
    public bool IsHerincasso { get; internal init; }

    internal void Annuleer() => IsGeannuleerd = true;
}
