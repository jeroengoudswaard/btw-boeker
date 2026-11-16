using CommunityToolkit.Mvvm.ComponentModel;
using BtwBoeker.Application.Modules;

namespace BtwBoeker.App.ViewModels;

/// <summary>
/// Meldingen van een pagina voor de meldingsbalk: <see cref="Melding"/> krijgt ✓, <see cref="Waarschuwing"/> ⚠,
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

    /// <summary>De naam van de pagina, bijvoorbeeld in de vraag om op te slaan; Boekhouding volgt de weergave.</summary>
    public virtual string Titel => Module.Titel;

    /// <summary>De titel in de kop: het onderdeel waar de gebruiker is (UX-review stap 1), bijvoorbeeld <i>Bank</i>.</summary>
    public virtual string Paginatitel => Titel;

    /// <summary>Eén zin uitleg onder de titel, per onderdeel.</summary>
    public virtual string Omschrijving => Module.Omschrijving;

    /// <summary>
    /// De sleutel van de regel in de navigatiebalk die bij deze pagina hoort (besluit 33), bijvoorbeeld
    /// <c>Boekhouding.Bank</c>; de band boven de pagina krijgt de kleur daarvan.
    /// </summary>
    public virtual string Navigatiesleutel => Module.Soort == ModuleSoort.Overzicht ? "Start" : Module.Soort.ToString();

    /// <summary>Een ander onderdeel (of een andere weergave) geeft een andere kop en een andere band.</summary>
    protected void MeldKopGewijzigd()
    {
        OnPropertyChanged(nameof(Paginatitel));
        OnPropertyChanged(nameof(Omschrijving));
        OnPropertyChanged(nameof(Navigatiesleutel));
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(Onderdeel))
        {
            MeldKopGewijzigd();
        }
    }

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
