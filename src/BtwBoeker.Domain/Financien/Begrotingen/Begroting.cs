using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Domain.Financien.Begrotingen;

public enum BegrotingStatus
{
    Concept = 1,
    Vastgesteld = 2,
}

/// <summary>
/// De begroting van één jaar (besluiten 146–150): een jaarbedrag per resultaatrekening, positief in de richting van de
/// rekening (baten of lasten, besluit 152). Een vastgestelde versie is alleen-lezen; een herziening is een nieuwe versie.
/// Telt nooit mee in het grootboek.
/// </summary>
public sealed class Begroting
{
    public const int MaximaleOmschrijvingLengte = 100;

    private readonly List<Begrotingsregel> _regels = [];

    private Begroting()
    {
        Omschrijving = null!;
    }

    public Begroting(AdministratieSoort administratie, int jaar, DateTime nuUtc)
        : this(administratie, jaar, 1, "Oorspronkelijk", nuUtc)
    {
    }

    private Begroting(AdministratieSoort administratie, int jaar, int versie, string omschrijving, DateTime nuUtc)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(jaar, 2000);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(jaar, 2100);
        Administratie = administratie;
        Jaar = jaar;
        Versie = versie;
        Omschrijving = omschrijving;
        Status = BegrotingStatus.Concept;
        AangemaaktUtc = nuUtc;
    }

    public int Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public int Jaar { get; private set; }

    /// <summary>1 = oorspronkelijk; 2 en hoger = herziening.</summary>
    public int Versie { get; private set; }

    public BegrotingStatus Status { get; private set; }
    public string Omschrijving { get; private set; }
    public DateOnly? VastgesteldOp { get; private set; }
    public DateTime AangemaaktUtc { get; private set; }
    public IReadOnlyList<Begrotingsregel> Regels => _regels;

    public bool IsVastgesteld => Status == BegrotingStatus.Vastgesteld;

    public Bedrag Bedrag(Grootboekrekening rekening) => Regel(rekening)?.Bedrag ?? Financien.Bedrag.Nul;

    /// <summary>Een bedrag van nul verwijdert de regel.</summary>
    /// <exception cref="InvalidOperationException">De begroting is vastgesteld.</exception>
    /// <exception cref="ArgumentException">Geen resultaatrekening, of een negatief bedrag.</exception>
    public void ZetBedrag(Grootboekrekening rekening, Bedrag bedrag, string? toelichting)
    {
        ArgumentNullException.ThrowIfNull(rekening);
        ControleerConcept();
        if (rekening.Soort != RekeningSoort.Resultaat)
        {
            throw new ArgumentException($"Alleen baten en lasten worden begroot, niet {rekening}.", nameof(rekening));
        }

        if (bedrag.Centen < 0)
        {
            throw new ArgumentException("Een begroot bedrag is nooit negatief.", nameof(bedrag));
        }

        var regel = Regel(rekening);
        if (bedrag == Financien.Bedrag.Nul)
        {
            if (regel is not null)
            {
                _regels.Remove(regel);
            }

            return;
        }

        if (regel is null)
        {
            _regels.Add(new Begrotingsregel(rekening, bedrag, toelichting));
        }
        else
        {
            regel.Wijzig(bedrag, toelichting);
        }
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
            Omschrijving = tekst.Length > MaximaleOmschrijvingLengte ? tekst[..MaximaleOmschrijvingLengte] : tekst;
        }
    }

    /// <summary>Een nieuwe conceptversie met dezelfde bedragen (besluit 149).</summary>
    /// <exception cref="InvalidOperationException">Deze versie is nog niet vastgesteld.</exception>
    public Begroting Herzie(DateTime nuUtc)
    {
        if (!IsVastgesteld)
        {
            throw new InvalidOperationException("Alleen een vastgestelde begroting kan worden herzien; pas het concept gewoon aan.");
        }

        var herziening = new Begroting(Administratie, Jaar, Versie + 1, $"Herziening {Versie}", nuUtc);
        herziening._regels.AddRange(_regels.Select(r => new Begrotingsregel(r.Rekening, r.Bedrag, r.Toelichting)));
        return herziening;
    }

    private Begrotingsregel? Regel(Grootboekrekening rekening) =>
        _regels.FirstOrDefault(r => ReferenceEquals(r.Rekening, rekening) || (rekening.Id != 0 && r.Rekening.Id == rekening.Id));

    private void ControleerConcept()
    {
        if (IsVastgesteld)
        {
            throw new InvalidOperationException($"De begroting {Jaar} ({Omschrijving}) is vastgesteld en kan niet meer worden gewijzigd. Maak een herziening.");
        }
    }
}

public sealed class Begrotingsregel
{
    public const int MaximaleToelichtingLengte = 200;

    private Begrotingsregel()
    {
        Rekening = null!;
    }

    internal Begrotingsregel(Grootboekrekening rekening, Bedrag bedrag, string? toelichting)
    {
        Rekening = rekening;
        Wijzig(bedrag, toelichting);
    }

    public int Id { get; private set; }
    public Grootboekrekening Rekening { get; private set; }
    public Bedrag Bedrag { get; private set; }
    public string? Toelichting { get; private set; }

    internal void Wijzig(Bedrag bedrag, string? toelichting)
    {
        Bedrag = bedrag;
        var tekst = toelichting?.Trim();
        Toelichting = string.IsNullOrEmpty(tekst) ? null : tekst.Length > MaximaleToelichtingLengte ? tekst[..MaximaleToelichtingLengte] : tekst;
    }
}
