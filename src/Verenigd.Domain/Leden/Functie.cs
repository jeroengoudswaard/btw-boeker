using System.Globalization;

namespace Verenigd.Domain.Leden;

/// <summary>Een rol van een persoon in een groep, met lidtype en periode.</summary>
public sealed class Functie
{
    private Functie()
    {
        Groep = null!;
        Rol = null!;
    }

    internal Functie(Groep groep, FunctieGegevens gegevens)
    {
        Groep = groep;
        Rol = gegevens.Rol;
        Startdatum = gegevens.Startdatum;
        Neem(gegevens);
    }

    public int Id { get; private set; }
    public Groep Groep { get; private set; }
    public string Rol { get; private set; }
    public Lidtype Lidtype { get; private set; }
    public DateOnly? Startdatum { get; private set; }
    public DateOnly? Einddatum { get; private set; }
    public string? Status { get; private set; }

    public bool IsActief(DateOnly datum) =>
        (Startdatum is null || Startdatum <= datum) && (Einddatum is null || Einddatum >= datum);

    internal bool HeeftSleutel(FunctieGegevens gegevens) =>
        string.Equals(Groep.Naam, gegevens.Groep, StringComparison.Ordinal)
        && string.Equals(Rol, gegevens.Rol, StringComparison.Ordinal)
        && Startdatum == gegevens.Startdatum;

    internal bool IsGelijk(FunctieGegevens gegevens) =>
        Lidtype == gegevens.Lidtype && Einddatum == gegevens.Einddatum && string.Equals(Status, gegevens.Status, StringComparison.Ordinal);

    internal void Neem(FunctieGegevens gegevens)
    {
        Lidtype = gegevens.Lidtype;
        Einddatum = gegevens.Einddatum;
        Status = gegevens.Status;
    }

    internal void Beeindig(DateOnly datum) => Einddatum = datum;

    internal static string Omschrijf(string rol, string groep, DateOnly? start, DateOnly? eind)
    {
        var periode = new List<string>(2);
        if (start is { } s)
        {
            periode.Add("vanaf " + s.ToString(LidGegevens.Datumnotatie, CultureInfo.InvariantCulture));
        }

        if (eind is { } e)
        {
            periode.Add("tot " + e.ToString(LidGegevens.Datumnotatie, CultureInfo.InvariantCulture));
        }

        return periode.Count == 0 ? $"{rol} {groep}" : $"{rol} {groep} ({string.Join(", ", periode)})";
    }

    public override string ToString() => Omschrijf(Rol, Groep.Naam, Startdatum, Einddatum);
}
