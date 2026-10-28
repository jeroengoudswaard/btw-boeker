using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;

namespace Verenigd.App.ViewModels.Boekhouding;

public enum HistorieImportStap
{
    Bestand,
    Voorbeeld,
    Klaar,
}

/// <summary>Eén rekening in het voorbeeld: aantal regels, saldo van de regels en saldo volgens het Voorblad.</summary>
public sealed record HistorieRekeningRegel(HistorieRekening Bron)
{
    public int Nummer => Bron.Nummer;
    public string Naam => Bron.Naam;
    public int AantalRegels => Bron.Regels.Count;
    public string SaldoRegels => Bron.Regels.Count == 0 ? string.Empty : Saldotekst(Bron.SaldoRegels);
    public string SaldoVoorblad => Bron.OpVoorblad ? Saldotekst(Bron.SaldoVoorblad) : string.Empty;

    public string Status =>
        !Bron.OpVoorblad ? "Niet op het Voorblad"
        : Bron.Regels.Count == 0 ? "Geen regels"
        : Bron.Klopt ? "✓ Klopt"
        : $"⚠ Wijkt {Saldotekst(Bron.SaldoRegels - Bron.SaldoVoorblad)} af";

    private static string Saldotekst(long saldo) => saldo < 0 ? $"{new Bedrag(-saldo)} credit" : $"{new Bedrag(saldo)} debet";
}

/// <summary>
/// Historie uit de Excel-boekhouding (besluiten 122–126): 1 bestand lezen, 2 voorbeeld per rekening met de vergelijking
/// met het Voorblad (een afwijking wordt overgenomen met een ⚠), 3 klaar. Vooraf wordt een back-up gemaakt; het
/// Excel-bestand wordt alleen gelezen.
/// </summary>
public sealed partial class HistorieImportViewModel : ObservableObject
{
    private readonly HistorieDienst _dienst;
    private readonly IWerkbladLezer _lezer;
    private readonly AdministratieSoort _administratie;
    private readonly string _pad;
    private HistorieVoorstel? _voorstel;

    public HistorieImportViewModel(HistorieDienst dienst, IWerkbladLezer lezer, AdministratieSoort administratie, string pad)
    {
        _dienst = dienst;
        _lezer = lezer;
        _administratie = administratie;
        _pad = pad;
        VolgendeCommand = new AsyncRelayCommand(VolgendeAsync, () => !IsBezig && Stap != HistorieImportStap.Klaar);
        VorigeCommand = new RelayCommand(() => Stap = HistorieImportStap.Bestand, () => !IsBezig && Stap == HistorieImportStap.Voorbeeld);
    }

    public string Bestandsnaam => Path.GetFileName(_pad);

    public string Titel => _voorstel is null ? "Historie" : $"Historie {_voorstel.Jaar.ToString(CultureInfo.InvariantCulture)}";

    public ObservableCollection<HistorieRekeningRegel> Rekeningen { get; } = [];

    public ObservableCollection<string> Meldingen { get; } = [];

    public IAsyncRelayCommand VolgendeCommand { get; }

    public IRelayCommand VorigeCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Volgendetekst), nameof(IsKlaar))]
    public partial HistorieImportStap Stap { get; private set; }

    [ObservableProperty]
    public partial bool IsBezig { get; private set; }

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    /// <summary>T-rekeningen die niet in het rekeningschema staan als inactieve rekening toevoegen (anders overslaan).</summary>
    [ObservableProperty]
    public partial bool OnbekendeToevoegen { get; set; }

    [ObservableProperty]
    public partial string Samenvatting { get; private set; } = string.Empty;

    public bool IsKlaar => Stap == HistorieImportStap.Klaar;

    public string Volgendetekst => Stap == HistorieImportStap.Voorbeeld ? "Overnemen" : "Volgende";

    public string Overzicht => _voorstel is null ? string.Empty : string.Create(CultureInfo.CurrentCulture,
        $"{_voorstel.AantalRegels} regels op {_voorstel.Rekeningen.Count(r => r.Regels.Count > 0)} rekeningen · {_voorstel.Rekeningen.Count(r => r.Klopt)} kloppen met het Voorblad · "
        + $"{_voorstel.AantalAfwijkend} {(_voorstel.AantalAfwijkend == 1 ? "rekening wijkt" : "rekeningen wijken")} af");

    internal int AantalOvergenomen { get; private set; }

    private async Task VolgendeAsync()
    {
        IsBezig = true;
        Foutmelding = null;
        Vernieuw();
        try
        {
            if (Stap == HistorieImportStap.Bestand)
            {
                Lees();
                Stap = HistorieImportStap.Voorbeeld;
            }
            else
            {
                AantalOvergenomen = await _dienst.ImporteerAsync(_administratie, _voorstel!,
                    OnbekendeToevoegen ? OnbekendeRekening.Toevoegen : OnbekendeRekening.Overslaan);
                Samenvatting = string.Create(CultureInfo.CurrentCulture,
                    $"{AantalOvergenomen} regels zijn overgenomen in het historische boekjaar {_voorstel!.Jaar}. Het jaar is alleen-lezen en telt niet mee in het grootboek. Vooraf is een back-up gemaakt.")
                    + (_voorstel.AantalAfwijkend == 0 ? string.Empty : " Rekeningen die afwijken van het Voorblad zijn gemarkeerd met ⚠ in de Grootboekkaart.");
                Stap = HistorieImportStap.Klaar;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException)
        {
            Foutmelding = ex.Message;
        }
        finally
        {
            IsBezig = false;
            Vernieuw();
        }
    }

    private void Lees()
    {
        _voorstel = HistorieInlezer.Lees(_lezer.LeesAlle(_pad));
        Rekeningen.Clear();
        foreach (var rekening in _voorstel.Rekeningen)
        {
            Rekeningen.Add(new HistorieRekeningRegel(rekening));
        }

        Meldingen.Clear();
        foreach (var melding in _voorstel.Meldingen)
        {
            Meldingen.Add(melding);
        }

        OnPropertyChanged(nameof(Titel));
        OnPropertyChanged(nameof(Overzicht));
    }

    partial void OnStapChanged(HistorieImportStap value) => Vernieuw();

    private void Vernieuw()
    {
        VolgendeCommand.NotifyCanExecuteChanged();
        VorigeCommand.NotifyCanExecuteChanged();
    }
}
