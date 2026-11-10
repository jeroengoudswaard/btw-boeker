namespace BtwBoeker.Domain.Administraties;

/// <summary>Een (boekhoudkundige) administratie met een eigen naam, accentkleur en drempel voor <i>Zonder bon</i>.</summary>
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

    /// <summary>Besluit 97: € 10.</summary>
    public const long StandaardZonderBonDrempel = 1000;

    /// <summary>Besluit 214: vanaf dit bedrag (in centen) toont het filter <i>Zonder bon</i> een boeking zonder bijlage.</summary>
    public long ZonderBonDrempel { get; private set; } = StandaardZonderBonDrempel;

    public static Kleur Standaardkleur(AdministratieSoort soort) => soort switch
    {
        AdministratieSoort.Vereniging => new Kleur(0x2E, 0x7D, 0x32),
        _ => throw new ArgumentOutOfRangeException(nameof(soort), soort, null),
    };

    public void WijzigNaam(string naam) => Naam = ValideerNaam(naam);

    public void WijzigAccentkleur(Kleur kleur) => Accentkleur = kleur;

    public void WijzigZonderBonDrempel(long centen) =>
        ZonderBonDrempel = centen >= 0 ? centen : throw new ArgumentOutOfRangeException(nameof(centen), "De drempel kan niet negatief zijn.");

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
