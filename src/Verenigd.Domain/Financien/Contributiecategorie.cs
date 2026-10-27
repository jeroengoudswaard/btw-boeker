using Verenigd.Domain.Gedeeld;

namespace Verenigd.Domain.Financien;

/// <summary>Soort contributiecategorie (besluit 44).</summary>
public enum CategorieSoort
{
    Jeugdlid = 1,

    /// <summary>Leiding betaalt geen contributie.</summary>
    Leiding = 2,

    Vrijgesteld = 3,
}

/// <summary>Een contributiecategorie, zoals <c>Jeugdlid Welpen</c>, <c>Leiding</c> of <c>Vrijgesteld</c>.</summary>
public sealed class Contributiecategorie
{
    public const int MaximaleNaamLengte = 100;

    public Contributiecategorie(string naam, CategorieSoort soort, string? speltak)
    {
        var schoon = naam?.Trim();
        if (string.IsNullOrEmpty(schoon) || schoon.Length > MaximaleNaamLengte)
        {
            throw new ArgumentException($"De naam is verplicht en maximaal {MaximaleNaamLengte} tekens.", nameof(naam));
        }

        Naam = schoon;
        Soort = soort;
        Speltak = soort == CategorieSoort.Jeugdlid ? speltak?.Trim() : null;
    }

    public int Id { get; private set; }
    public string Naam { get; private set; }
    public CategorieSoort Soort { get; private set; }

    /// <summary>Naam van de SOL3-groep waarvoor deze categorie wordt voorgesteld (alleen jeugdleden, besluit 52).</summary>
    public string? Speltak { get; private set; }

    /// <summary>Alleen jeugdleden betalen contributie.</summary>
    public bool HeeftTarief => Soort == CategorieSoort.Jeugdlid;

    public void WijzigNaam(string naam)
    {
        var schoon = naam?.Trim();
        if (string.IsNullOrEmpty(schoon) || schoon.Length > MaximaleNaamLengte)
        {
            throw new ArgumentException($"De naam is verplicht en maximaal {MaximaleNaamLengte} tekens.", nameof(naam));
        }

        Naam = schoon;
    }
}

/// <summary>Het maandbedrag van een categorie in een boekjaar (besluiten 15 en 53).</summary>
public sealed class Tarief
{
    private Tarief()
    {
        Categorie = null!;
    }

    public Tarief(Contributiecategorie categorie, int boekjaar, Bedrag maandbedrag)
    {
        ArgumentNullException.ThrowIfNull(categorie);
        Categorie = categorie;
        Boekjaar = boekjaar;
        WijzigBedrag(maandbedrag);
    }

    public int Id { get; private set; }
    public Contributiecategorie Categorie { get; private set; }
    public int Boekjaar { get; private set; }
    public Bedrag Maandbedrag { get; private set; }

    public void WijzigBedrag(Bedrag maandbedrag)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maandbedrag.Centen, 0L, nameof(maandbedrag));
        if (!Categorie.HeeftTarief && maandbedrag != Bedrag.Nul)
        {
            throw new InvalidOperationException($"{Categorie.Naam} betaalt geen contributie.");
        }

        Maandbedrag = maandbedrag;
    }
}

/// <summary>De categorie van een lid vanaf een ingangsdatum; meerdere rijen vormen de historie (besluit 54).</summary>
public sealed class LidCategorie
{
    private LidCategorie()
    {
        Categorie = null!;
    }

    public LidCategorie(Lidnummer lidnummer, Contributiecategorie categorie, DateOnly ingangsdatum)
    {
        ArgumentNullException.ThrowIfNull(categorie);
        Lidnummer = lidnummer;
        Categorie = categorie;
        Ingangsdatum = ingangsdatum;
    }

    public int Id { get; private set; }
    public Lidnummer Lidnummer { get; private set; }
    public Contributiecategorie Categorie { get; private set; }
    public DateOnly Ingangsdatum { get; private set; }

    /// <summary>De categorie die op <paramref name="datum"/> geldt: de laatste met een ingangsdatum op of vóór die datum.</summary>
    public static LidCategorie? Op(IEnumerable<LidCategorie> historie, Lidnummer lidnummer, DateOnly datum)
    {
        ArgumentNullException.ThrowIfNull(historie);
        return historie
            .Where(c => c.Lidnummer == lidnummer && c.Ingangsdatum <= datum)
            .OrderByDescending(c => c.Ingangsdatum)
            .ThenByDescending(c => c.Id)
            .FirstOrDefault();
    }
}
