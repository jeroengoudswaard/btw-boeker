using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.Application.Relaties;

namespace Verenigd.App.ViewModels.Boekhouding;

public sealed partial class BoekhoudingViewModel
{
    private RelatieDienst? _relatiedienst;

    private RelatieDienst Relatiedienst => _relatiedienst ??= new RelatieDienst(_sessie);

    public Verzameling<RelatieRegel> Relaties { get; } = [];

    [ObservableProperty]
    public partial RelatieRegel? GeselecteerdeRelatie { get; set; }

    private bool RelatiesGewijzigd => Relaties.Any(r => r.IsGewijzigd);

    private bool RelatiesGeldig => Relaties.Where(r => r.IsGewijzigd).All(r => r.Fout is null);

    private void LaadRelaties()
    {
        KoppelRelaties(false);
        var gekozenRelatie = GeselecteerdeRelatie?.Id;
        Relaties.Vervang(_stand!.Relaties.Select(relatie => new RelatieRegel(relatie)));
        KoppelRelaties(true);
        GeselecteerdeRelatie = Relaties.FirstOrDefault(r => r.Id == gekozenRelatie);
    }

    private async Task BewaarRelatiesAsync()
    {
        foreach (var relatie in Relaties.Where(r => r.IsGewijzigd).ToList())
        {
            await Relatiedienst.BewaarAsync(relatie.Invoer);
        }
    }

    [RelayCommand]
    private void NieuweRelatie()
    {
        var regel = new RelatieRegel(null);
        regel.PropertyChanged += RelatieGewijzigd;
        Relaties.Add(regel);
        GeselecteerdeRelatie = regel;
        Subtab = BoekhoudingSubtab.Relaties;
        CommandsVernieuwen();
    }

    private void KoppelRelaties(bool koppel)
    {
        foreach (var regel in Relaties)
        {
            regel.PropertyChanged -= RelatieGewijzigd;
            if (koppel)
            {
                regel.PropertyChanged += RelatieGewijzigd;
            }
        }
    }

    private void RelatieGewijzigd(object? sender, PropertyChangedEventArgs e) => CommandsVernieuwen();
}
