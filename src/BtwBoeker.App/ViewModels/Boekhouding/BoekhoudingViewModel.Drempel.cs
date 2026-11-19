using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtwBoeker.Domain.Financien;

namespace BtwBoeker.App.ViewModels.Boekhouding;

/// <summary>
/// Besluit 42 (keuze 2B): de drempel van het filter <i>Zonder bon</i> staat bij het filter zelf, alleen als het filter aan
/// staat. Hij wordt direct bewaard, bij het verlaten van het veld of bij Enter; een ongeldig bedrag wordt niet bewaard.
/// </summary>
public sealed partial class BoekhoudingViewModel
{
    private static readonly CultureInfo Nederlands = CultureInfo.GetCultureInfo("nl-NL");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Drempelfout))]
    public partial string Drempeltekst { get; set; } = string.Empty;

    public bool IsDrempelZichtbaar => AlleenZonderBon;

    public string? Drempelfout => Drempeltekst.Trim().Length > 0 && Drempel is null ? "Vul een bedrag in, bijvoorbeeld 10,00." : null;

    private long? Drempel => Bedrag.TryParse(Drempeltekst, out var bedrag) && bedrag.Centen >= 0 ? bedrag.Centen : null;

    [RelayCommand]
    private async Task BevestigDrempelAsync()
    {
        if (Drempel is not { } centen)
        {
            return;
        }

        if (centen != Administratie.Model.ZonderBonDrempel)
        {
            Administratie.Model.WijzigZonderBonDrempel(centen);
            await _sessie.Administraties.BewaarAsync(Administratie.Model);
            Administratie.Ververs();
            if (_stand is not null)
            {
                BerekenZonderBon();
                PasPostfilterToe(GeselecteerdePost?.Id);
            }
        }

        Drempeltekst = Drempelweergave(centen);
    }

    private static string Drempelweergave(long centen) => (centen / 100m).ToString("0.00", Nederlands);
}
