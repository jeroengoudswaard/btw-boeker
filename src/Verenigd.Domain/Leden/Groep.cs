namespace Verenigd.Domain.Leden;

/// <summary>Een organisatie-eenheid uit SOL3, zoals een speltak of het bestuur.</summary>
public sealed class Groep
{
    public const int MaximaleNaamLengte = 200;

    public Groep(string naam, GroepSoort soort)
    {
        var schoon = naam?.Trim();
        if (string.IsNullOrEmpty(schoon) || schoon.Length > MaximaleNaamLengte)
        {
            throw new ArgumentException($"De groepsnaam is verplicht en maximaal {MaximaleNaamLengte} tekens.", nameof(naam));
        }

        Naam = schoon;
        Soort = soort;
    }

    public int Id { get; private set; }

    /// <summary>Zoals SOL3 hem levert; uniek.</summary>
    public string Naam { get; private set; }

    public GroepSoort Soort { get; private set; }

    public void WijzigSoort(GroepSoort soort) => Soort = soort;

    /// <summary>Voorstel voor de importwizard; de gebruiker kan het aanpassen.</summary>
    public static GroepSoort VoorgesteldeSoort(string naam)
    {
        ArgumentNullException.ThrowIfNull(naam);
        if (naam.Contains("(Migratie)", StringComparison.OrdinalIgnoreCase))
        {
            return GroepSoort.Technisch;
        }

        return naam.Contains("bestuur", StringComparison.OrdinalIgnoreCase) || naam.Contains("sticht", StringComparison.OrdinalIgnoreCase)
            ? GroepSoort.Bestuur
            : GroepSoort.Speltak;
    }
}
