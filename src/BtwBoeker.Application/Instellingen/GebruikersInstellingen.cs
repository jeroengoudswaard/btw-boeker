using BtwBoeker.Application.Modules;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.Application.Instellingen;

public enum Thema
{
    Systeem,
    Licht,
    Donker,
}

/// <summary>Welke woorden het tabblad Financiën gebruikt; de gegevens blijven altijd formeel.</summary>
public enum Weergavemodus
{
    Expert,
    Eenvoudig,
}

/// <summary>De pagina die bij een herstart weer opent: module, administratie en onderdeel (subtabblad).</summary>
public sealed record LaatstePagina(ModuleSoort Module, AdministratieSoort? Administratie, int? Onderdeel);

/// <summary>Voorkeuren per Windows-gebruiker. Bevat geen persoonsgegevens of bedragen.</summary>
public sealed record GebruikersInstellingen
{
    public Thema Thema { get; init; } = Thema.Systeem;

    /// <summary>Standaard Expert.</summary>
    public Weergavemodus Weergave { get; init; } = Weergavemodus.Expert;

    public string? LaatsteDatabase { get; init; }

    public LaatstePagina? LaatstePagina { get; init; }

    /// <summary>De startwizard opent automatisch bij de eerste start, maar één keer.</summary>
    public bool StartwizardGetoond { get; init; }
}

public interface IInstellingenOpslag
{
    GebruikersInstellingen Laad();

    void Bewaar(GebruikersInstellingen instellingen);
}
