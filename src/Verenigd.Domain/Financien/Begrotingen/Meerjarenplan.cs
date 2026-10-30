using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Domain.Financien.Begrotingen;

/// <summary>
/// Het meerjarenplan van de Stichting (besluiten 159–167): per resultaatrekening een bedrag per jaar over 10 jaar, in de
/// prijzen van dat jaar (geen indexatie). Versies en vaststellen werken als bij de begroting. Telt nooit mee in het grootboek.
/// </summary>
public sealed class Meerjarenplan
{
    public const int StandaardAantalJaren = 10;

    private readonly List<Meerjarenpost> _posten = [];

    private Meerjarenplan()
    {
        Omschrijving = null!;
    }

    public Meerjarenplan(AdministratieSoort administratie, int startjaar, DateTime nuUtc)
        : this(administratie, startjaar, 1, "Oorspronkelijk", nuUtc)
    {
    }

    private Meerjarenplan(AdministratieSoort administratie, int startjaar, int versie, string omschrijving, DateTime nuUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(startjaar, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startjaar, 2090);
        Administratie = administratie;
        Startjaar = startjaar;
        AantalJaren = StandaardAantalJaren;
        Versie = versie;
        Omschrijving = omschrijving;
        Status = BegrotingStatus.Concept;
        AangemaaktUtc = nuUtc;
    }

    public int Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public int Startjaar { get; private set; }
    public int AantalJaren { get; private set; }
    public int Versie { get; private set; }
    public BegrotingStatus Status { get; private set; }
    public string Omschrijving { get; private set; }
    public DateOnly? VastgesteldOp { get; private set; }
    public DateTime AangemaaktUtc { get; private set; }
    public IReadOnlyList<Meerjarenpost> Posten => _posten;

    public int Eindjaar => Startjaar + AantalJaren - 1;
    public IEnumerable<int> Jaren => Enumerable.Range(Startjaar, AantalJaren);
    public bool IsVastgesteld => Status == BegrotingStatus.Vastgesteld;

    public Bedrag Bedrag(Grootboekrekening rekening, int jaar) => Post(rekening)?.Bedrag(jaar) ?? Financien.Bedrag.Nul;

    public string? Toelichting(Grootboekrekening rekening) => Post(rekening)?.Toelichting;

    /// <summary>Een bedrag van nul verwijdert het bedrag.</summary>
    /// <exception cref="InvalidOperationException">Het plan is vastgesteld.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Het jaar valt buiten het plan.</exception>
    /// <exception cref="ArgumentException">Geen resultaatrekening, of een negatief bedrag.</exception>
    public void ZetBedrag(Grootboekrekening rekening, int jaar, Bedrag bedrag)
    {
        ArgumentNullException.ThrowIfNull(rekening);
        ControleerConcept();
        ArgumentOutOfRangeException.ThrowIfLessThan(jaar, Startjaar);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(jaar, Eindjaar);
        if (rekening.Soort != RekeningSoort.Resultaat)
        {
            throw new ArgumentException($"Alleen baten en lasten worden gepland, niet {rekening}.", nameof(rekening));
        }

        if (bedrag.Centen < 0)
        {
            throw new ArgumentException("Een gepland bedrag is nooit negatief.", nameof(bedrag));
        }

        var post = Post(rekening);
        if (post is null)
        {
            if (bedrag == Financien.Bedrag.Nul)
            {
                return;
            }

            post = new Meerjarenpost(rekening);
            _posten.Add(post);
        }

        post.Zet(jaar, bedrag);
        RuimOp(post);
    }

    /// <summary>Besluit 166: het bedrag van <paramref name="vanafJaar"/> naar alle latere jaren van het plan.</summary>
    public void KopieerNaarVolgendeJaren(Grootboekrekening rekening, int vanafJaar)
    {
        ControleerConcept();
        var bedrag = Bedrag(rekening, vanafJaar);
        for (var jaar = vanafJaar + 1; jaar <= Eindjaar; jaar++)
        {
            ZetBedrag(rekening, jaar, bedrag);
        }
    }

    public void ZetToelichting(Grootboekrekening rekening, string? toelichting)
    {
        ArgumentNullException.ThrowIfNull(rekening);
        ControleerConcept();
        var post = Post(rekening);
        if (post is null)
        {
            if (string.IsNullOrWhiteSpace(toelichting))
            {
                return;
            }

            post = new Meerjarenpost(rekening);
            _posten.Add(post);
        }

        post.ZetToelichting(toelichting);
        RuimOp(post);
    }

    /// <exception cref="InvalidOperationException">Al vastgesteld.</exception>
    public void StelVast(DateOnly datum, string? omschrijving)
    {
        ControleerConcept();
        Status = BegrotingStatus.Vastgesteld;
        VastgesteldOp = datum;
        if (!string.IsNullOrWhiteSpace(omschrijving))
        {
            var tekst = omschrijving.Trim();
            Omschrijving = tekst.Length > Begroting.MaximaleOmschrijvingLengte ? tekst[..Begroting.MaximaleOmschrijvingLengte] : tekst;
        }
    }

    /// <summary>Een nieuwe conceptversie met dezelfde bedragen en toelichtingen (besluit 164).</summary>
    /// <exception cref="InvalidOperationException">Deze versie is nog niet vastgesteld.</exception>
    public Meerjarenplan Herzie(DateTime nuUtc)
    {
        if (!IsVastgesteld)
        {
            throw new InvalidOperationException("Alleen een vastgesteld meerjarenplan kan worden herzien; pas het concept gewoon aan.");
        }

        var herziening = new Meerjarenplan(Administratie, Startjaar, Versie + 1, $"Herziening {Versie}", nuUtc) { AantalJaren = AantalJaren };
        herziening._posten.AddRange(_posten.Select(p => p.Kopie()));
        return herziening;
    }

    private Meerjarenpost? Post(Grootboekrekening rekening) =>
        _posten.FirstOrDefault(p => ReferenceEquals(p.Rekening, rekening) || (rekening.Id != 0 && p.Rekening.Id == rekening.Id));

    private void RuimOp(Meerjarenpost post)
    {
        if (post.IsLeeg)
        {
            _posten.Remove(post);
        }
    }

    private void ControleerConcept()
    {
        if (IsVastgesteld)
        {
            throw new InvalidOperationException($"Het meerjarenplan {Startjaar}–{Eindjaar} ({Omschrijving}) is vastgesteld en kan niet meer worden gewijzigd. Maak een herziening.");
        }
    }
}

/// <summary>Eén rekening in het meerjarenplan, met een bedrag per jaar en een toelichting.</summary>
public sealed class Meerjarenpost
{
    public const int MaximaleToelichtingLengte = 200;

    private readonly List<Meerjarenbedrag> _bedragen = [];

    private Meerjarenpost()
    {
        Rekening = null!;
    }

    internal Meerjarenpost(Grootboekrekening rekening)
    {
        Rekening = rekening;
    }

    public int Id { get; private set; }
    public Grootboekrekening Rekening { get; private set; }
    public string? Toelichting { get; private set; }
    public IReadOnlyList<Meerjarenbedrag> Bedragen => _bedragen;

    internal bool IsLeeg => _bedragen.Count == 0 && Toelichting is null;

    public Bedrag Bedrag(int jaar) => _bedragen.FirstOrDefault(b => b.Jaar == jaar)?.Bedrag ?? Financien.Bedrag.Nul;

    internal void Zet(int jaar, Bedrag bedrag)
    {
        var bestaand = _bedragen.FirstOrDefault(b => b.Jaar == jaar);
        if (bedrag == Financien.Bedrag.Nul)
        {
            if (bestaand is not null)
            {
                _bedragen.Remove(bestaand);
            }
        }
        else if (bestaand is null)
        {
            _bedragen.Add(new Meerjarenbedrag(jaar, bedrag));
        }
        else
        {
            bestaand.Wijzig(bedrag);
        }
    }

    internal void ZetToelichting(string? toelichting)
    {
        var tekst = toelichting?.Trim();
        Toelichting = string.IsNullOrEmpty(tekst) ? null : tekst.Length > MaximaleToelichtingLengte ? tekst[..MaximaleToelichtingLengte] : tekst;
    }

    internal Meerjarenpost Kopie()
    {
        var kopie = new Meerjarenpost(Rekening) { Toelichting = Toelichting };
        kopie._bedragen.AddRange(_bedragen.Select(b => new Meerjarenbedrag(b.Jaar, b.Bedrag)));
        return kopie;
    }
}

public sealed class Meerjarenbedrag
{
    private Meerjarenbedrag()
    {
    }

    internal Meerjarenbedrag(int jaar, Bedrag bedrag)
    {
        Jaar = jaar;
        Bedrag = bedrag;
    }

    public int Id { get; private set; }
    public int Jaar { get; private set; }
    public Bedrag Bedrag { get; private set; }

    internal void Wijzig(Bedrag bedrag) => Bedrag = bedrag;
}
