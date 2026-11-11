using CommunityToolkit.Mvvm.ComponentModel;
using BtwBoeker.Application.Modules;

namespace BtwBoeker.App.ViewModels;

/// <summary>
/// Meldingen van een pagina voor de statusregel: <see cref="Melding"/> krijgt ✓, <see cref="Waarschuwing"/> ⚠,
/// <see cref="Foutmelding"/> ✕.
/// </summary>
public interface IPaginameldingen
{
    string? Melding => null;

    string? Waarschuwing => null;

    string? Foutmelding => null;
}

/// <summary>Basis voor elke pagina achter de navigatiebalk (Start, Boekhouding, Instellingen).</summary>
public abstract class PaginaViewModel(ModuleDefinitie module) : ObservableObject
{
    public ModuleDefinitie Module { get; } = module;

    /// <summary>De titel in de kop van de pagina; Boekhouding volgt de weergave.</summary>
    public virtual string Titel => Module.Titel;

    /// <summary>Eén zin uitleg onder de titel.</summary>
    public string Omschrijving => Module.Omschrijving;

    public bool KanSluiten => Module.KanSluiten;

    /// <summary>Het gekozen onderdeel (subtabblad) als getal, of <c>null</c> bij een pagina zonder onderdelen.</summary>
    public virtual int? Onderdeel
    {
        get => null;
        set { }
    }

    public virtual bool HeeftOnopgeslagenWijzigingen => false;

    /// <summary>Of Bestand › Opslaan (Ctrl+S) nu iets te bewaren heeft.</summary>
    public virtual bool KanOpslaan => false;

    /// <returns><c>true</c> als alles is opgeslagen.</returns>
    public virtual Task<bool> BewaarAsync() => Task.FromResult(true);

    /// <summary>Wordt aangeroepen als de pagina definitief gesloten is (bij het sluiten van de database).</summary>
    public virtual void Opruimen()
    {
    }
}
