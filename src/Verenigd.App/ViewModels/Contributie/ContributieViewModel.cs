using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Verenigd.App.Diensten;
using Verenigd.Application.Database;
using Verenigd.Application.Incasso;
using Verenigd.Application.Modules;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Leden;

namespace Verenigd.App.ViewModels.Contributie;

/// <summary>
/// Tabblad Contributie (alleen Vereniging, besluit 16) met vier subtabbladen: leden &amp; categorie, betalers &amp;
/// machtigingen, tarieven en incassoruns.
/// </summary>
public sealed partial class ContributieViewModel : TabbladViewModel
{
    private readonly IDatabaseSessie _sessie;
    private readonly IDialoogService _dialogen;
    private readonly TimeProvider _tijd;
    private readonly ILogger _logger;
    private readonly IncassoDienst _dienst;
    private readonly Dictionary<int, List<TariefRegel>> _tarievenPerJaar = [];
    private List<LidCategorieRegel> _alleLeden = [];
    private IncassoStand? _stand;

    public ContributieViewModel(
        IDatabaseSessie sessie, IDialoogService dialogen, TimeProvider tijd, AdministratieViewModel administratie, ILogger logger)
        : base(ModuleCatalogus.Geef(ModuleSoort.Contributie), administratie)
    {
        _sessie = sessie;
        _dialogen = dialogen;
        _tijd = tijd;
        _logger = logger;
        _dienst = new IncassoDienst(sessie, tijd);
        VernieuwCommand = new AsyncRelayCommand(VernieuwAsync);
        OpslaanCommand = new AsyncRelayCommand(BewaarAsync, () => KanOpslaan);
        Boekjaar = Vandaag.Year;
    }

    public IAsyncRelayCommand VernieuwCommand { get; }

    public IAsyncRelayCommand OpslaanCommand { get; }

    public ObservableCollection<LidCategorieRegel> Leden { get; } = [];

    public ObservableCollection<TariefRegel> Tarieven { get; } = [];

    public ObservableCollection<BetalerRegel> Betalers { get; } = [];

    public ObservableCollection<RunRegel> Runs { get; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Contributiecategorie> Categorieen { get; private set; } = [];

    public IReadOnlyList<int> Boekjaren => [Vandaag.Year - 1, Vandaag.Year, Vandaag.Year + 1];

    [ObservableProperty]
    public partial int Boekjaar { get; set; }

    [ObservableProperty]
    public partial string Zoektekst { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool AlleenWaarschuwingen { get; set; }

    [ObservableProperty]
    public partial BetalerRegel? GeselecteerdeBetaler { get; set; }

    [ObservableProperty]
    public partial BetalerEditor? Betaler { get; private set; }

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KanOpslaan))]
    public partial bool IsBezig { get; private set; }

    public int AantalZonderBetaler => _alleLeden.Count(l => l.Waarschuwing == "Geen betaler");

    public int AantalVoorstellen => _alleLeden.Count(l => l.GekozenCategorie is null && l.Voorstel is not null);

    public override bool HeeftOnopgeslagenWijzigingen =>
        _alleLeden.Exists(l => l.IsGewijzigd)
        || _tarievenPerJaar.Values.SelectMany(t => t).Any(t => t.IsGewijzigd)
        || Betaler?.IsGewijzigd == true;

    public override bool KanOpslaan =>
        HeeftOnopgeslagenWijzigingen && !IsBezig
        && _alleLeden.TrueForAll(l => l.Fout is null)
        && _tarievenPerJaar.Values.SelectMany(t => t).All(t => t.Fout is null)
        && (Betaler is not { IsGewijzigd: true } || Betaler.Fouten.Count == 0);

    private DateOnly Vandaag => DateOnly.FromDateTime(_tijd.GetLocalNow().DateTime);

    public override async Task<bool> BewaarAsync()
    {
        if (!KanOpslaan)
        {
            return !HeeftOnopgeslagenWijzigingen;
        }

        IsBezig = true;
        try
        {
            var categorieen = _alleLeden
                .Where(l => l.IsGewijzigd)
                .Select(l => new LidCategorieInvoer(l.Lidnummer, l.GekozenCategorie!.Id, DateOnly.FromDateTime(l.Ingangsdatum!.Value)))
                .ToList();
            if (categorieen.Count > 0)
            {
                await _sessie.Contributie.BewaarLidCategorieenAsync(categorieen);
            }

            foreach (var (jaar, regels) in _tarievenPerJaar)
            {
                var bedragen = regels.Where(r => r.IsGewijzigd && r.Bedrag is not null).ToDictionary(r => r.CategorieId, r => r.Bedrag!.Value);
                if (bedragen.Count > 0)
                {
                    await _sessie.Contributie.BewaarTarievenAsync(jaar, bedragen);
                }
            }

            Guid? betalerId = null;
            if (Betaler is { IsGewijzigd: true } editor)
            {
                betalerId = await _sessie.Contributie.BewaarBetalerAsync(editor.Invoer);
            }

            Foutmelding = null;
            await LaadAsync(betalerId ?? Betaler?.Id);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or InvalidOperationException)
        {
            Foutmelding = ex.Message;
            return false;
        }
        finally
        {
            IsBezig = false;
            WijzigingenGewijzigd();
        }
    }

    public override void Opruimen()
    {
        Ontkoppel();
        base.Opruimen();
    }

    private async Task VernieuwAsync()
    {
        if (HeeftOnopgeslagenWijzigingen
            && !_dialogen.Bevestig("Vernieuwen", "Er zijn niet-opgeslagen wijzigingen. Wilt u die verwerpen en de gegevens opnieuw laden?"))
        {
            return;
        }

        IsBezig = true;
        try
        {
            var speltakken = (await _sessie.Leden.HaalGroepenAsync()).Where(g => g.Soort == GroepSoort.Speltak).Select(g => g.Naam);
            await _dienst.ZorgVoorStandaardcategorieenAsync(speltakken);
            await LaadAsync(Betaler?.Id);
            Foutmelding = null;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Foutmelding = "De contributiegegevens konden niet worden geladen.";
        }
        finally
        {
            IsBezig = false;
            WijzigingenGewijzigd();
        }
    }

    private async Task LaadAsync(Guid? betalerSelectie)
    {
        _stand = await _sessie.Contributie.LaadAsync();
        var vandaag = Vandaag;
        Ontkoppel();
        Categorieen = [.. _stand.Categorieen.OrderBy(c => c.Soort).ThenBy(c => c.Naam, StringComparer.CurrentCulture)];

        var koppelingen = _stand.Koppelingen.ToDictionary(k => k.Lidnummer, k => k.BetalerId);
        var betalers = _stand.Betalers.ToDictionary(b => b.Id);
        var beginJaar = new DateOnly(vandaag.Year, 1, 1);
        _alleLeden = [.. _stand.Personen
            .Where(p => p.UitgeschrevenOp is null || p.UitgeschrevenOp >= beginJaar)
            .OrderBy(p => p.Achternaam, StringComparer.CurrentCulture).ThenBy(p => p.Voornaam, StringComparer.CurrentCulture)
            .Select(p =>
            {
                var huidig = LidCategorie.Op(_stand.LidCategorieen, p.Lidnummer, vandaag)
                    ?? _stand.LidCategorieen.Where(c => c.Lidnummer == p.Lidnummer).OrderBy(c => c.Ingangsdatum).FirstOrDefault();
                var betaler = koppelingen.TryGetValue(p.Lidnummer, out var id) && betalers.TryGetValue(id, out var b) ? b : null;
                return new LidCategorieRegel(
                    p, huidig?.Categorie, huidig?.Ingangsdatum, CategorieVoorstel.Bepaal(p, _stand.Categorieen, vandaag),
                    betaler is null ? null : betaler.Rekeninghouder ?? betaler.Iban.ToString(),
                    betaler?.HeeftActieveMachtiging(vandaag) == true, vandaag);
            })];
        foreach (var regel in _alleLeden)
        {
            regel.PropertyChanged += RegelGewijzigd;
        }

        PasLedenfilterToe();

        Betalers.Clear();
        foreach (var betaler in _stand.Betalers.OrderBy(b => b.Rekeninghouder ?? b.Iban.Waarde, StringComparer.CurrentCulture))
        {
            var aantal = _stand.Koppelingen.Count(k => k.BetalerId == betaler.Id);
            var machtiging = betaler.Mandaatkenmerk is null
                ? "Geen"
                : betaler.HeeftActieveMachtiging(vandaag)
                    ? $"{betaler.Mandaatkenmerk} ({Weergave.Datum(betaler.MachtigingOndertekendOp)})"
                    : $"Ingetrokken per {Weergave.Datum(betaler.MachtigingIngetrokkenOp)}";
            var waarschuwing = !betaler.Iban.ControlegetalGeldig ? "Ongeldig IBAN"
                : !betaler.HeeftActieveMachtiging(vandaag) ? "Geen geldige machtiging"
                : aantal == 0 ? "Geen leden gekoppeld"
                : null;
            Betalers.Add(new BetalerRegel(betaler.Id, betaler.Rekeninghouder ?? "(onbekend)", betaler.Iban.ToString(), aantal, machtiging, waarschuwing));
        }

        _tarievenPerJaar.Clear();
        ToonTarieven();

        Runs.Clear();
        foreach (var run in _stand.Runs)
        {
            Runs.Add(new RunRegel(run));
        }

        GeselecteerdeBetaler = Betalers.FirstOrDefault(b => b.Id == betalerSelectie);
        if (GeselecteerdeBetaler is null)
        {
            Betaler = null;
        }

        OnPropertyChanged(nameof(AantalZonderBetaler));
        OnPropertyChanged(nameof(AantalVoorstellen));
    }

    [RelayCommand]
    private void VoorstellenOvernemen()
    {
        foreach (var regel in _alleLeden)
        {
            regel.NeemVoorstelOver();
        }

        OnPropertyChanged(nameof(AantalVoorstellen));
    }

    [RelayCommand]
    private void KopieerVorigJaar()
    {
        if (_stand is null)
        {
            return;
        }

        foreach (var regel in Tarieven.Where(r => r.IsBewerkbaar))
        {
            var vorig = _stand.Tarieven.FirstOrDefault(t => t.Boekjaar == Boekjaar - 1 && t.Categorie.Id == regel.CategorieId);
            if (vorig is not null)
            {
                regel.Bedragtekst = Weergave.Invoer(vorig.Maandbedrag);
            }
        }
    }

    [RelayCommand]
    private void NieuweBetaler()
    {
        GeselecteerdeBetaler = null;
        ZetEditor(new BetalerEditor(null, [], BeschikbareLeden(), _stand?.Betalers ?? []));
    }

    [RelayCommand]
    private async Task NieuweRunAsync()
    {
        if (HeeftOnopgeslagenWijzigingen)
        {
            _dialogen.ToonInformatie("Nieuwe incassorun", "Sla eerst de wijzigingen op (Ctrl+S).");
            return;
        }

        var wizard = new IncassoWizardViewModel(_dienst, _dialogen, _tijd);
        _dialogen.ToonIncassoWizard(wizard);
        if (wizard.Resultaat is { } run)
        {
            ShellLog.IncassorunDefinitief(_logger, run.Maand.Code, run.AantalPosten, run.Totaal.SepaNotatie);
        }

        await LaadAsync(Betaler?.Id);
        WijzigingenGewijzigd();
    }

    [RelayCommand]
    private async Task BestandOpslaanAsync(RunRegel? regel)
    {
        if (regel is null)
        {
            return;
        }

        try
        {
            var bestand = await _dienst.MaakBestandAsync(regel.Run);
            if (_dialogen.KiesXmlDoel(bestand.Bestandsnaam) is { } doel)
            {
                await File.WriteAllBytesAsync(doel, bestand.Inhoud);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _dialogen.ToonFout("Incassobestand opslaan", ex.Message);
        }
    }

    [RelayCommand]
    private async Task AnnuleerRunAsync(RunRegel? regel)
    {
        if (regel is not { IsDefinitief: true }
            || !_dialogen.Bevestig(
                "Run annuleren",
                $"Weet u zeker dat de bank de run van {regel.Maand} niet heeft uitgevoerd?\n\nNa annuleren komen de maanden weer vrij voor een nieuwe run. De run blijft zichtbaar in de historie."))
        {
            return;
        }

        await _dienst.AnnuleerAsync(regel.Run);
        ShellLog.IncassorunGeannuleerd(_logger, regel.Run.Maand.Code);
        await LaadAsync(Betaler?.Id);
    }

    partial void OnBoekjaarChanged(int value) => ToonTarieven();

    partial void OnZoektekstChanged(string value) => PasLedenfilterToe();

    partial void OnAlleenWaarschuwingenChanged(bool value) => PasLedenfilterToe();

    partial void OnGeselecteerdeBetalerChanged(BetalerRegel? value)
    {
        if (value is null || _stand is null)
        {
            return;
        }

        var betaler = _stand.Betalers.Single(b => b.Id == value.Id);
        var namen = _stand.Personen.ToDictionary(p => p.Lidnummer, p => p.VolledigeNaam);
        var leden = _stand.Koppelingen
            .Where(k => k.BetalerId == betaler.Id)
            .Select(k => new LidKeuze(k.Lidnummer, namen.GetValueOrDefault(k.Lidnummer, "(onbekend lidnummer)")));
        ZetEditor(new BetalerEditor(betaler, leden, BeschikbareLeden(), _stand.Betalers));
    }

    private List<LidKeuze> BeschikbareLeden() =>
        [.. (_stand?.Personen ?? []).Where(p => !p.IsUitgeschreven).OrderBy(p => p.VolledigeNaam, StringComparer.CurrentCulture)
            .Select(p => new LidKeuze(p.Lidnummer, p.VolledigeNaam))];

    private void ZetEditor(BetalerEditor editor)
    {
        if (Betaler is not null)
        {
            Betaler.PropertyChanged -= RegelGewijzigd;
        }

        Betaler = editor;
        editor.PropertyChanged += RegelGewijzigd;
        WijzigingenGewijzigd();
    }

    private void ToonTarieven()
    {
        if (_stand is null)
        {
            return;
        }

        if (!_tarievenPerJaar.TryGetValue(Boekjaar, out var regels))
        {
            regels = [.. Categorieen.Select(c => new TariefRegel(c, _stand.Tarieven.FirstOrDefault(t => t.Boekjaar == Boekjaar && t.Categorie.Id == c.Id)))];
            foreach (var regel in regels)
            {
                regel.PropertyChanged += RegelGewijzigd;
            }

            _tarievenPerJaar[Boekjaar] = regels;
        }

        Tarieven.Clear();
        foreach (var regel in regels)
        {
            Tarieven.Add(regel);
        }
    }

    private void PasLedenfilterToe()
    {
        var zoek = Zoektekst?.Trim() ?? string.Empty;
        Leden.Clear();
        foreach (var regel in _alleLeden.Where(r =>
            (!AlleenWaarschuwingen || r.Waarschuwing is not null)
            && (zoek.Length == 0
                || r.Naam.Contains(zoek, StringComparison.CurrentCultureIgnoreCase)
                || r.Lidnummer.Waarde.StartsWith(zoek, StringComparison.Ordinal))))
        {
            Leden.Add(regel);
        }
    }

    private void Ontkoppel()
    {
        foreach (var regel in _alleLeden)
        {
            regel.PropertyChanged -= RegelGewijzigd;
        }

        foreach (var regel in _tarievenPerJaar.Values.SelectMany(t => t))
        {
            regel.PropertyChanged -= RegelGewijzigd;
        }
    }

    private void RegelGewijzigd(object? sender, PropertyChangedEventArgs e) => WijzigingenGewijzigd();

    private void WijzigingenGewijzigd()
    {
        OnPropertyChanged(nameof(HeeftOnopgeslagenWijzigingen));
        OnPropertyChanged(nameof(KanOpslaan));
        OpslaanCommand.NotifyCanExecuteChanged();
    }
}
