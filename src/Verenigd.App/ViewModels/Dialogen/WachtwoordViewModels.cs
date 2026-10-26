using CommunityToolkit.Mvvm.ComponentModel;
using Verenigd.App.Diensten;
using Verenigd.Application.Beveiliging;

namespace Verenigd.App.ViewModels.Dialogen;

/// <summary>Invoer van een nieuw wachtwoord met bevestiging.</summary>
public sealed partial class NieuwWachtwoordViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(KanBevestigen))]
    public partial string Wachtwoord { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(KanBevestigen))]
    public partial string Bevestiging { get; set; } = "";

    /// <summary>De eerste reden waarom het wachtwoord niet voldoet; <c>null</c> zolang er nog niets is ingevuld.</summary>
    public string? Fout
    {
        get
        {
            if (Wachtwoord.Length == 0 && Bevestiging.Length == 0)
            {
                return null;
            }

            var bezwaren = WachtwoordBeleid.Controleer(Wachtwoord);
            if (bezwaren.Count > 0)
            {
                return bezwaren[0];
            }

            return Bevestiging.Length > 0 && !string.Equals(Wachtwoord, Bevestiging, StringComparison.Ordinal)
                ? "De wachtwoorden komen niet overeen."
                : null;
        }
    }

    public bool KanBevestigen =>
        WachtwoordBeleid.Controleer(Wachtwoord).Count == 0 && string.Equals(Wachtwoord, Bevestiging, StringComparison.Ordinal);
}

/// <summary>Database openen met de herstelsleutel en een nieuw wachtwoord instellen.</summary>
public sealed partial class HerstelViewModel : ObservableObject
{
    public HerstelViewModel()
    {
        Wachtwoord.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Fout));
            OnPropertyChanged(nameof(KanBevestigen));
        };
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(KanBevestigen))]
    public partial string HerstelsleutelTekst { get; set; } = "";

    public NieuwWachtwoordViewModel Wachtwoord { get; } = new();

    public string? Fout =>
        HerstelsleutelTekst.Length > 0 && !Herstelsleutel.TryParse(HerstelsleutelTekst, out _)
            ? "De herstelsleutel is niet geldig. Hij bestaat uit 8 groepen van 5 tekens."
            : Wachtwoord.Fout;

    public bool KanBevestigen => Herstelsleutel.TryParse(HerstelsleutelTekst, out _) && Wachtwoord.KanBevestigen;

    public HerstelInvoer? MaakInvoer() =>
        KanBevestigen && Herstelsleutel.TryParse(HerstelsleutelTekst, out var sleutel)
            ? new HerstelInvoer(sleutel, Wachtwoord.Wachtwoord)
            : null;
}
