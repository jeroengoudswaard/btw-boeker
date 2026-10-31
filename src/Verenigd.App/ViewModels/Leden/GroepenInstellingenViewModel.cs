using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.Application.Leden;
using Verenigd.Domain.Leden;

namespace Verenigd.App.ViewModels.Leden;

/// <summary>
/// Instellingen › Groepen (besluit 215): per groep het soort (speltak, bestuur, technisch, overig), ook buiten de import.
/// Het soort bepaalt o.a. de speltak in e-mails en de voorgestelde contributiecategorie.
/// </summary>
public sealed partial class GroepenInstellingenViewModel : ObservableObject
{
    private readonly ILedenRepository _leden;

    public GroepenInstellingenViewModel(ILedenRepository leden)
    {
        _leden = leden;
        LaadCommand = new AsyncRelayCommand(LaadAsync);
    }

    public IAsyncRelayCommand LaadCommand { get; }

    public ObservableCollection<GroepSoortRegel> Regels { get; } = [];

    public static IReadOnlyList<GroepSoort> Soorten { get; } = Enum.GetValues<GroepSoort>();

    public bool IsGewijzigd => Regels.Any(r => r.IsGewijzigd);

    public async Task LaadAsync()
    {
        foreach (var regel in Regels)
        {
            regel.PropertyChanged -= RegelGewijzigd;
        }

        Regels.Clear();
        foreach (var groep in (await _leden.HaalGroepenAsync()).OrderBy(g => g.Naam, StringComparer.CurrentCulture))
        {
            var regel = new GroepSoortRegel(groep.Naam, groep.Soort);
            regel.PropertyChanged += RegelGewijzigd;
            Regels.Add(regel);
        }

        OnPropertyChanged(nameof(IsGewijzigd));
    }

    public async Task BewaarAsync()
    {
        await _leden.BewaarGroepsoortenAsync(Regels.Where(r => r.IsGewijzigd).ToDictionary(r => r.Naam, r => r.Soort, StringComparer.Ordinal));
        await LaadAsync();
    }

    private void RegelGewijzigd(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(IsGewijzigd));
}

public sealed partial class GroepSoortRegel(string naam, GroepSoort soort) : ObservableObject
{
    private readonly GroepSoort _origineel = soort;

    public string Naam { get; } = naam;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial GroepSoort Soort { get; set; } = soort;

    public bool IsGewijzigd => Soort != _origineel;
}
