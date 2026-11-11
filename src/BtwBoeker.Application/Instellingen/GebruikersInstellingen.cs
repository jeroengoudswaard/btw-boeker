using BtwBoeker.Application.Modules;

namespace BtwBoeker.Application.Instellingen;

public enum Thema
{
    Systeem,
    Licht,
    Donker,
}

/// <summary>Welke woorden Boekhouding gebruikt; de gegevens blijven altijd formeel.</summary>
public enum Weergavemodus
{
    Expert,
    Eenvoudig,
}

/// <summary>
/// De pagina die bij een herstart weer opent: module en onderdeel (subtabblad). Een bestand van v0.19.0 bevat ook de
/// administratie; dat veld wordt bij het lezen overgeslagen.
/// </summary>
public sealed record LaatstePagina(ModuleSoort Module, int? Onderdeel);

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
