namespace BtwBoeker.Domain.Administraties;

/// <summary>Een (boekhoudkundige) administratie met een eigen naam en drempel voor <i>Zonder bon</i>.</summary>
public sealed class Administratie
{
    public const int MaximaleNaamLengte = 100;

    /// <summary>De naam van een nieuwe administratie, tot de gebruiker er een andere naam aan geeft.</summary>
    public const string Standaardnaam = "Mijn administratie";

    public Administratie(AdministratieSoort soort, string naam)
    {
        Soort = soort;
        Naam = ValideerNaam(naam);
    }

    public int Id { get; private set; }
    public AdministratieSoort Soort { get; private set; }
    public string Naam { get; private set; }

    /// <summary>Standaard € 10.</summary>
    public const long StandaardZonderBonDrempel = 1000;

    /// <summary>Vanaf dit bedrag (in centen) toont het filter <i>Zonder bon</i> een boeking zonder bijlage.</summary>
    public long ZonderBonDrempel { get; private set; } = StandaardZonderBonDrempel;

    public void WijzigNaam(string naam) => Naam = ValideerNaam(naam);

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
