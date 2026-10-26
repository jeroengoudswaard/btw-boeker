using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Verenigd.App.ViewModels;

/// <summary>Eén item in het menu Modules. Items met kinderen hebben geen eigen opdracht.</summary>
public sealed class ModuleMenuItem : ObservableObject
{
    private readonly AdministratieViewModel? _administratie;
    private readonly string _titel;

    public ModuleMenuItem(
        string titel,
        string? pictogram,
        string? sneltoets,
        ICommand? opdracht,
        object? parameter,
        IReadOnlyList<ModuleMenuItem> kinderen,
        AdministratieViewModel? administratie = null)
    {
        _titel = titel;
        Pictogram = pictogram;
        Sneltoets = sneltoets;
        Opdracht = opdracht;
        Parameter = parameter;
        Kinderen = kinderen;
        _administratie = administratie;
        if (administratie is not null)
        {
            PropertyChangedEventManager.AddHandler(
                administratie, (_, _) => OnPropertyChanged(nameof(Titel)), nameof(AdministratieViewModel.Naam));
        }
    }

    public string Titel => _administratie?.Naam ?? _titel;

    public string? Pictogram { get; }

    public string? Sneltoets { get; }

    public ICommand? Opdracht { get; }

    public object? Parameter { get; }

    public IReadOnlyList<ModuleMenuItem> Kinderen { get; }

    public AdministratieViewModel? Administratie => _administratie;

    internal static ModuleMenuItem VoorAdministratie(AdministratieViewModel administratie, ICommand opdracht, object parameter) =>
        new(administratie.Naam, null, null, opdracht, parameter, [], administratie);
}
