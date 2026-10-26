namespace Verenigd.Domain.Administraties;

/// <summary>Een afzonderlijke (boekhoudkundige) administratie: de Vereniging of de Stichting.</summary>
public sealed class Administratie
{
    public const int MaximaleNaamLengte = 100;

    public Administratie(AdministratieSoort soort, string naam, Kleur accentkleur)
    {
        Soort = soort;
        Naam = ValideerNaam(naam);
        Accentkleur = accentkleur;
    }

    public int Id { get; private set; }
    public AdministratieSoort Soort { get; private set; }
    public string Naam { get; private set; }
    public Kleur Accentkleur { get; private set; }

    public static Kleur Standaardkleur(AdministratieSoort soort) => soort switch
    {
        AdministratieSoort.Vereniging => new Kleur(0x2E, 0x7D, 0x32),
        AdministratieSoort.Stichting => new Kleur(0x15, 0x65, 0xC0),
        _ => throw new ArgumentOutOfRangeException(nameof(soort), soort, null),
    };

    public void WijzigNaam(string naam) => Naam = ValideerNaam(naam);

    public void WijzigAccentkleur(Kleur kleur) => Accentkleur = kleur;

    private static string ValideerNaam(string naam)
    {
        var schoon = naam?.Trim();
        if (string.IsNullOrEmpty(schoon))
        {
            throw new ArgumentException("De naam van een administratie is verplicht.", nameof(naam));
        }

        if (schoon.Length > MaximaleNaamLengte)
        {
            throw new ArgumentException($"De naam mag maximaal {MaximaleNaamLengte} tekens lang zijn.", nameof(naam));
        }

        return schoon;
    }
}
