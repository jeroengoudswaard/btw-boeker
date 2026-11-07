using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Verenigd.Application.Modules;

namespace Verenigd.App.ViewModels;

/// <summary>
/// Meldingen van een pagina voor de statusregel (besluit 168): <see cref="Melding"/> krijgt ✓, <see cref="Waarschuwing"/> ⚠,
/// <see cref="Foutmelding"/> ✕.
/// </summary>
public interface IPaginameldingen
{
    string? Melding => null;

    string? Waarschuwing => null;

    string? Foutmelding => null;
}

/// <summary>
/// Basis voor elke pagina (sinds v0.8.0 geen tabblad meer, maar een pagina achter de navigatiebalk; de naam is gebleven).
/// Een pagina hoort bij hooguit één administratie.
/// </summary>
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

    /// <summary>Besluit 242: de titel in de kop van de pagina (zonder de administratie, die staat ernaast).</summary>
    public string Paginatitel => Moduletitel;

    /// <summary>Besluit 242: één zin uitleg onder de titel.</summary>
    public string Omschrijving => Module.Omschrijving;

    public bool KanSluiten => Module.KanSluiten;

    /// <summary>Het gekozen onderdeel (subtabblad) als getal, of <c>null</c> bij een pagina zonder onderdelen.</summary>
    public virtual int? Onderdeel
    {
        get => null;
        set { }
    }

    public virtual bool HeeftOnopgeslagenWijzigingen => false;

    /// <summary>Opent E-mail › Opstellen met een voorbereid bericht (besluiten 205–207); de Shell vult dit in.</summary>
    public Func<Email.EmailVoorstel, Task>? EmailAanbieden { get; set; }

    protected Task BiedEmailAanAsync(Email.EmailVoorstel voorstel) => EmailAanbieden?.Invoke(voorstel) ?? Task.CompletedTask;

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
            OnPropertyChanged(nameof(Paginatitel));
        }
    }
}

/// <summary>Plaatshouder voor modules die in een latere fase worden gebouwd.</summary>
public sealed class ModuleInOntwikkelingViewModel(ModuleDefinitie module, AdministratieViewModel? administratie)
    : TabbladViewModel(module, administratie)
{
    public string Melding => $"De module {Module.Titel} wordt gebouwd in fase {Module.Fase}.";
}
