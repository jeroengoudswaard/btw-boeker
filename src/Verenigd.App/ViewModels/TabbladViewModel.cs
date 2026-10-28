using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Verenigd.Application.Modules;

namespace Verenigd.App.ViewModels;

/// <summary>Basis voor elk tabblad. Een tabblad hoort bij hooguit één administratie.</summary>
public abstract class TabbladViewModel : ObservableObject
{
    protected TabbladViewModel(ModuleDefinitie module, AdministratieViewModel? administratie)
    {
        Module = module;
        Administratie = administratie;
        if (administratie is not null)
        {
            administratie.PropertyChanged += AdministratieGewijzigd;
        }
    }

    public ModuleDefinitie Module { get; }

    public AdministratieViewModel? Administratie { get; }

    public string Titel => Administratie is null ? Moduletitel : $"{Moduletitel} · {Administratie.Naam}";

    /// <summary>De naam van de module in de tabtitel; Financiën volgt de weergave (besluit 127).</summary>
    protected virtual string Moduletitel => Module.Titel;

    public bool KanSluiten => Module.KanSluiten;

    public virtual bool HeeftOnopgeslagenWijzigingen => false;

    public virtual bool KanOpslaan => false;

    /// <returns><c>true</c> als alles is opgeslagen.</returns>
    public virtual Task<bool> BewaarAsync() => Task.FromResult(true);

    /// <summary>Wordt aangeroepen als het tabblad definitief gesloten is.</summary>
    public virtual void Opruimen()
    {
        if (Administratie is not null)
        {
            Administratie.PropertyChanged -= AdministratieGewijzigd;
        }
    }

    private void AdministratieGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdministratieViewModel.Naam))
        {
            OnPropertyChanged(nameof(Titel));
        }
    }
}

/// <summary>Plaatshouder voor modules die in een latere fase worden gebouwd.</summary>
public sealed class ModuleInOntwikkelingViewModel(ModuleDefinitie module, AdministratieViewModel? administratie)
    : TabbladViewModel(module, administratie)
{
    public string Melding => $"De module {Module.Titel} wordt gebouwd in fase {Module.Fase}.";
}
