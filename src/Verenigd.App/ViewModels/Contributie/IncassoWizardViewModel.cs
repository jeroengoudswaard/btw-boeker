using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.App.Diensten;
using Verenigd.Application.Incasso;
using Verenigd.Domain.Financien;

namespace Verenigd.App.ViewModels.Contributie;

public enum IncassoStap
{
    Maand,
    Berekening,
    Controleren,
    Klaar,
}

public sealed record PostRegelWeergave(string Naam, string Maand, string Categorie, string Bedrag, bool IsNaheffing);

public sealed record PostWeergave(string Rekeninghouder, string Iban, string Bedrag, string Omschrijving, IReadOnlyList<PostRegelWeergave> Regels);

public sealed record ZelfTeInnenWeergave(string Lidnummer, string Naam, string Maanden, string Bedrag, string Reden);

/// <summary>
/// Wizard voor een incassorun: 1 maand kiezen, 2 berekening bekijken, 3 controleren en definitief maken (na een back-up),
/// 4 klaar. Pas in stap 3 wordt er iets opgeslagen.
/// </summary>
public sealed partial class IncassoWizardViewModel : ObservableObject
{
    private readonly IncassoDienst _dienst;
    private readonly IDialoogService _dialogen;
    private IncassoVoorstel? _voorstel;

    public IncassoWizardViewModel(IncassoDienst dienst, IDialoogService dialogen, TimeProvider tijd)
    {
        ArgumentNullException.ThrowIfNull(tijd);
        _dienst = dienst;
        _dialogen = dialogen;
        var huidig = Maand.Van(DateOnly.FromDateTime(tijd.GetLocalNow().DateTime));
        Maanden = huidig.Vorige.Vorige.Tot(huidig.Volgende.Volgende);
        GekozenMaand = huidig.Volgende;
        VolgendeCommand = new AsyncRelayCommand(VolgendeAsync, KanVolgende);
        VorigeCommand = new RelayCommand(Vorige, KanVorige);
    }

    public IReadOnlyList<Maand> Maanden { get; }

    public ObservableCollection<PostWeergave> Posten { get; } = [];

    public ObservableCollection<ZelfTeInnenWeergave> ZelfTeInnen { get; } = [];

    public ObservableCollection<string> Meldingen { get; } = [];

    public IAsyncRelayCommand VolgendeCommand { get; }

    public IRelayCommand VorigeCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Volgendetekst), nameof(IsKlaar))]
    public partial IncassoStap Stap { get; private set; }

    [ObservableProperty]
    public partial Maand GekozenMaand { get; set; }

    [ObservableProperty]
    public partial bool IsGecontroleerd { get; set; }

    [ObservableProperty]
    public partial bool IsBezig { get; private set; }

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    [ObservableProperty]
    public partial Incassorun? Resultaat { get; private set; }

    [ObservableProperty]
    public partial string Samenvatting { get; private set; } = string.Empty;

    public bool IsKlaar => Stap == IncassoStap.Klaar;

    public bool IsLeeg => _voorstel?.IsLeeg != false;

    public string Volgendetekst => Stap == IncassoStap.Controleren ? "Definitief maken" : "Volgende";

    public string Uitvoerdatum => _voorstel is null ? string.Empty : Weergave.Datum(_voorstel.Uitvoerdatum);

    public string Totaaltekst => _voorstel is null
        ? string.Empty
        : string.Create(CultureInfo.CurrentCulture, $"{_voorstel.AantalPosten} {(_voorstel.AantalPosten == 1 ? "post" : "posten")} · {_voorstel.Totaal}");

    public string PostenKop => $"Posten ({Posten.Count.ToString(CultureInfo.CurrentCulture)})";

    public string ZelfTeInnenKop => $"Zelf te innen ({ZelfTeInnen.Count.ToString(CultureInfo.CurrentCulture)})";

    private bool KanVolgende() => !IsBezig && Stap switch
    {
        IncassoStap.Maand or IncassoStap.Berekening => true,
        IncassoStap.Controleren => IsGecontroleerd && !IsLeeg,
        _ => false,
    };

    private bool KanVorige() => !IsBezig && Stap is IncassoStap.Berekening or IncassoStap.Controleren;

    private async Task VolgendeAsync()
    {
        IsBezig = true;
        Foutmelding = null;
        Vernieuw();
        try
        {
            switch (Stap)
            {
                case IncassoStap.Maand:
                    await BerekenAsync();
                    Stap = IncassoStap.Berekening;
                    break;
                case IncassoStap.Berekening:
                    Stap = IncassoStap.Controleren;
                    break;
                case IncassoStap.Controleren:
                    await MaakDefinitiefAsync();
                    Stap = IncassoStap.Klaar;
                    break;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Foutmelding = ex.Message;
        }
        finally
        {
            IsBezig = false;
            Vernieuw();
        }
    }

    private void Vorige()
    {
        Stap = Stap == IncassoStap.Controleren ? IncassoStap.Berekening : IncassoStap.Maand;
        IsGecontroleerd = false;
        Foutmelding = null;
        Vernieuw();
    }

    private async Task BerekenAsync()
    {
        _voorstel = await _dienst.BerekenAsync(GekozenMaand);
        Posten.Clear();
        foreach (var post in _voorstel.Posten)
        {
            Posten.Add(new PostWeergave(
                post.Rekeninghouder, post.Betaler.Iban.ToString(), post.Bedrag.ToString(), post.Omschrijving,
                [.. post.Regels.Select(r => new PostRegelWeergave(r.Naam, r.Maand.ToString(), r.Categorie, r.Bedrag.ToString(), r.IsNaheffing))]));
        }

        ZelfTeInnen.Clear();
        foreach (var regel in _voorstel.ZelfTeInnen)
        {
            ZelfTeInnen.Add(new ZelfTeInnenWeergave(regel.Lidnummer.Waarde, regel.Naam, regel.Maanden, regel.Bedrag?.ToString() ?? "onbekend", regel.Reden));
        }

        Meldingen.Clear();
        foreach (var melding in _voorstel.Meldingen)
        {
            Meldingen.Add(melding);
        }

        IsGecontroleerd = false;
        OnPropertyChanged(nameof(IsLeeg));
        OnPropertyChanged(nameof(Totaaltekst));
        OnPropertyChanged(nameof(Uitvoerdatum));
        OnPropertyChanged(nameof(PostenKop));
        OnPropertyChanged(nameof(ZelfTeInnenKop));
    }

    private async Task MaakDefinitiefAsync()
    {
        var run = await _dienst.MaakDefinitiefAsync(_voorstel!);
        Resultaat = run;
        var regels = new List<string>
        {
            string.Create(CultureInfo.CurrentCulture,
                $"De run van {run.Maand} is definitief: {Totaaltekst}, uitvoerdatum {Weergave.Datum(run.Uitvoerdatum)}. Vooraf is een back-up gemaakt."),
        };

        try
        {
            var bestand = await _dienst.MaakBestandAsync(run);
            if (_dialogen.KiesXmlDoel(bestand.Bestandsnaam) is { } doel)
            {
                await File.WriteAllBytesAsync(doel, bestand.Inhoud);
                regels.Add($"Het incassobestand is opgeslagen als {Path.GetFileName(doel)}. Upload het in Rabo Internetbankieren (Betalen › Incasso › Bestand uploaden).");
            }
            else
            {
                regels.Add("Het incassobestand is niet opgeslagen. U kunt het later opslaan via Incassoruns › Bestand opslaan.");
            }

            if (_voorstel!.ZelfTeInnen.Count > 0
                && _dialogen.KiesCsvDoel($"zelf-te-innen-{run.Maand.Code}.csv") is { } csv)
            {
                await File.WriteAllTextAsync(csv, IncassoDienst.ZelfTeInnenCsv(_voorstel));
                regels.Add($"De lijst 'zelf te innen' is opgeslagen als {Path.GetFileName(csv)}.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            regels.Add($"Het opslaan is mislukt ({ex.Message}). De run is wel definitief; sla het bestand later op via Incassoruns.");
        }

        Samenvatting = string.Join(Environment.NewLine + Environment.NewLine, regels);
    }

    partial void OnIsGecontroleerdChanged(bool value) => Vernieuw();

    private void Vernieuw()
    {
        VolgendeCommand.NotifyCanExecuteChanged();
        VorigeCommand.NotifyCanExecuteChanged();
    }
}
