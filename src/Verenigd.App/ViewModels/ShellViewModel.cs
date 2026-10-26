using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Verenigd.App.Diensten;
using Verenigd.Application.Beveiliging;
using Verenigd.Application.Database;
using Verenigd.Application.Instellingen;
using Verenigd.Application.Modules;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.ViewModels;

public sealed record ModuleVerzoek(ModuleSoort Module, AdministratieSoort? Administratie)
{
    /// <summary>Voor gebruik in XAML (Extra › Instellingen, Ctrl+,).</summary>
    public static ModuleVerzoek Instellingen { get; } = new(ModuleSoort.Instellingen, null);
}

/// <summary>Hoofdvenster: database-sessie, tabbladen, menu's en statusbalk.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IDatabaseBeheer _beheer;
    private readonly IInstellingenOpslag _opslag;
    private readonly IDialoogService _dialogen;
    private readonly IThemaService _thema;
    private readonly TimeProvider _tijd;
    private readonly ILogger<ShellViewModel> _logger;
    private readonly OverzichtViewModel _overzicht;
    private GebruikersInstellingen _instellingen = new();
    private IDatabaseSessie? _sessie;

    public ShellViewModel(
        IDatabaseBeheer beheer,
        IInstellingenOpslag opslag,
        IDialoogService dialogen,
        IThemaService thema,
        TimeProvider tijd,
        ILogger<ShellViewModel> logger)
    {
        _beheer = beheer;
        _opslag = opslag;
        _dialogen = dialogen;
        _thema = thema;
        _tijd = tijd;
        _logger = logger;
        _overzicht = new OverzichtViewModel(this);
        Tabbladen.Add(_overzicht);
        GeselecteerdTabblad = _overzicht;
        BouwModuleMenu();
    }

    public ObservableCollection<TabbladViewModel> Tabbladen { get; } = [];

    public ObservableCollection<AdministratieViewModel> Administraties { get; } = [];

    public ObservableCollection<ModuleMenuItem> ModuleMenu { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActieveAdministratie), nameof(ActieveAdministratieNaam), nameof(Venstertitel))]
    [NotifyCanExecuteChangedFor(nameof(SluitTabbladCommand), nameof(OpslaanCommand))]
    public partial TabbladViewModel? GeselecteerdTabblad { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDatabaseOpen), nameof(Venstertitel))]
    [NotifyCanExecuteChangedFor(
        nameof(OpenModuleCommand), nameof(OpenModuleViaSneltoetsCommand), nameof(SluitDatabaseCommand), nameof(MaakBackupCommand))]
    public partial string? DatabaseNaam { get; set; }

    [ObservableProperty]
    public partial Thema Thema { get; set; }

    [ObservableProperty]
    public partial string Statusmelding { get; set; } = "Gereed";

    public bool IsDatabaseOpen => DatabaseNaam is not null;

    public AdministratieViewModel? ActieveAdministratie => GeselecteerdTabblad?.Administratie;

    public string? ActieveAdministratieNaam => ActieveAdministratie?.Naam;

    public string Boekjaartekst => $"Boekjaar {_tijd.GetLocalNow().Year.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Windows-conventie: "Document - Applicatie".</summary>
    public string Venstertitel => (DatabaseNaam, ActieveAdministratieNaam) switch
    {
        (null, _) => "Verenigd",
        (var db, null) => $"{db} - Verenigd",
        (var db, var adm) => $"{adm} - {db} - Verenigd",
    };

    public async Task InitialiseerAsync()
    {
        _instellingen = _opslag.Laad();
        Thema = _instellingen.Thema;
        _thema.PasToe(Thema);

        if (_instellingen.LaatsteDatabase is { } pad && _beheer.Bestaat(pad))
        {
            await OpenAsync(pad);
        }
    }

    /// <summary>Aangeroepen bij het sluiten van het hoofdvenster.</summary>
    /// <returns><c>false</c> als de gebruiker het afsluiten annuleert.</returns>
    public Task<bool> MagAfsluitenAsync() => SluitSessieAsync();

    // ---- Bestand ---------------------------------------------------------------------------

    [RelayCommand]
    private async Task NieuweDatabaseAsync()
    {
        var pad = _dialogen.KiesNieuwDatabasebestand("Nieuwe database aanmaken");
        if (pad is null || !await SluitSessieAsync())
        {
            return;
        }

        var wachtwoord = _dialogen.VraagNieuwWachtwoord();
        if (wachtwoord is null)
        {
            return;
        }

        try
        {
            var herstelsleutel = await _beheer.MaakAanAsync(pad, wachtwoord);
            ShellLog.DatabaseAangemaakt(_logger, Path.GetFileName(pad));
            _dialogen.ToonHerstelsleutel(herstelsleutel);
            _instellingen = _instellingen with { OpenTabbladen = [] };
            await NaOpenenAsync(await _beheer.OpenAsync(pad, wachtwoord), herstelTabbladen: false);
        }
        catch (Exception ex) when (ex is DatabaseBestandException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            ShellLog.Mislukt(_logger, ex, "Aanmaken van database");
            _dialogen.ToonFout("Nieuwe database", ex.Message);
        }
    }

    [RelayCommand]
    private async Task OpenDatabaseAsync()
    {
        var pad = _dialogen.KiesBestaandDatabasebestand();
        if (pad is not null)
        {
            await OpenAsync(pad);
        }
    }

    [RelayCommand(CanExecute = nameof(IsDatabaseOpen))]
    private async Task SluitDatabaseAsync() => await SluitSessieAsync();

    [RelayCommand(CanExecute = nameof(KanOpslaan))]
    private async Task OpslaanAsync()
    {
        if (GeselecteerdTabblad is { } tab && await tab.BewaarAsync())
        {
            Statusmelding = "Opgeslagen";
        }

        OpslaanCommand.NotifyCanExecuteChanged();
    }

    private bool KanOpslaan() => GeselecteerdTabblad?.KanOpslaan == true;

    [RelayCommand(CanExecute = nameof(IsDatabaseOpen))]
    private async Task MaakBackupAsync()
    {
        if (_sessie is null)
        {
            return;
        }

        var voorstel = string.Create(
            CultureInfo.InvariantCulture,
            $"{Path.GetFileNameWithoutExtension(_sessie.Pad)}-{_tijd.GetLocalNow():yyyy-MM-dd}.vbak");
        var doel = _dialogen.KiesBackupDoel(voorstel);
        if (doel is null)
        {
            return;
        }

        try
        {
            await _sessie.MaakBackupAsync(doel);
            ShellLog.BackupGemaakt(_logger);
            Statusmelding = $"Back-up opgeslagen als {Path.GetFileName(doel)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShellLog.Mislukt(_logger, ex, "Back-up maken");
            _dialogen.ToonFout("Back-up maken", $"De back-up kon niet worden opgeslagen.\n\n{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task HerstelBackupAsync()
    {
        var bron = _dialogen.KiesBackupBron();
        var doel = bron is null ? null : _dialogen.KiesNieuwDatabasebestand("Teruggezette database opslaan als");
        if (bron is null || doel is null)
        {
            return;
        }

        try
        {
            await _beheer.HerstelBackupAsync(bron, doel);
            ShellLog.BackupTeruggezet(_logger, Path.GetFileName(doel));
        }
        catch (Exception ex) when (ex is DatabaseBestandException or IOException or UnauthorizedAccessException)
        {
            ShellLog.Mislukt(_logger, ex, "Back-up terugzetten");
            _dialogen.ToonFout("Back-up terugzetten", ex.Message);
            return;
        }

        if (_dialogen.Bevestig("Back-up teruggezet", $"De back-up is teruggezet als '{Path.GetFileName(doel)}'.\n\nWilt u deze database nu openen?"))
        {
            await OpenAsync(doel);
        }
    }

    // ---- Beeld -----------------------------------------------------------------------------

    [RelayCommand]
    private void KiesThema(Thema thema)
    {
        Thema = thema;
        _thema.PasToe(thema);
        _instellingen = _instellingen with { Thema = thema };
        _opslag.Bewaar(_instellingen);
    }

    // ---- Tabbladen -------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(KanModuleOpenen))]
    private void OpenModule(ModuleVerzoek verzoek)
    {
        if (verzoek.Module == ModuleSoort.Overzicht)
        {
            GeselecteerdTabblad = _overzicht;
            return;
        }

        var bestaand = Tabbladen.FirstOrDefault(t => t.Module.Soort == verzoek.Module && t.Administratie?.Soort == verzoek.Administratie);
        if (bestaand is not null)
        {
            GeselecteerdTabblad = bestaand;
            return;
        }

        var definitie = ModuleCatalogus.Geef(verzoek.Module);
        var administratie = Administraties.FirstOrDefault(a => a.Soort == verzoek.Administratie);
        TabbladViewModel tab = verzoek.Module switch
        {
            ModuleSoort.Instellingen => new InstellingenViewModel(_sessie!.Administraties, Administraties),
            _ => new ModuleInOntwikkelingViewModel(definitie, administratie),
        };
        Tabbladen.Add(tab);
        GeselecteerdTabblad = tab;
    }

    private bool KanModuleOpenen(ModuleVerzoek? verzoek)
    {
        if (verzoek is null)
        {
            return false;
        }

        if (verzoek.Module == ModuleSoort.Overzicht)
        {
            return true;
        }

        var definitie = ModuleCatalogus.Geef(verzoek.Module);
        return IsDatabaseOpen
            && definitie.PerAdministratie == verzoek.Administratie.HasValue
            && (definitie.AlleenVoor is null || definitie.AlleenVoor == verzoek.Administratie)
            && (verzoek.Administratie is null || Administraties.Any(a => a.Soort == verzoek.Administratie));
    }

    /// <summary>Ctrl+1...6. Financiële modules openen voor de administratie van het actieve tabblad (anders de Vereniging).</summary>
    [RelayCommand]
    private void OpenModuleViaSneltoets(string? nummer)
    {
        if (!int.TryParse(nummer, CultureInfo.InvariantCulture, out var n)
            || ModuleCatalogus.Alle.FirstOrDefault(m => m.Sneltoetsnummer == n) is not { } definitie)
        {
            return;
        }

        var verzoek = new ModuleVerzoek(
            definitie.Soort,
            definitie.PerAdministratie
                ? definitie.AlleenVoor ?? ActieveAdministratie?.Soort ?? AdministratieSoort.Vereniging
                : null);
        if (KanModuleOpenen(verzoek))
        {
            OpenModule(verzoek);
        }
    }

    [RelayCommand(CanExecute = nameof(KanTabbladSluiten))]
    private async Task SluitTabbladAsync(TabbladViewModel? tab)
    {
        tab ??= GeselecteerdTabblad;
        if (tab is null || !tab.KanSluiten || !await MagTabbladSluitenAsync(tab))
        {
            return;
        }

        var index = Tabbladen.IndexOf(tab);
        var wasGeselecteerd = ReferenceEquals(GeselecteerdTabblad, tab);
        Tabbladen.RemoveAt(index);
        tab.Opruimen();
        if (wasGeselecteerd || GeselecteerdTabblad is null)
        {
            GeselecteerdTabblad = Tabbladen[Math.Min(index, Tabbladen.Count - 1)];
        }
    }

    private bool KanTabbladSluiten(TabbladViewModel? tab) => (tab ?? GeselecteerdTabblad)?.KanSluiten == true;

    [RelayCommand]
    private void VolgendTabblad() => SelecteerRelatief(+1);

    [RelayCommand]
    private void VorigTabblad() => SelecteerRelatief(-1);

    private void SelecteerRelatief(int stap)
    {
        if (Tabbladen.Count == 0)
        {
            return;
        }

        var index = GeselecteerdTabblad is null ? 0 : Tabbladen.IndexOf(GeselecteerdTabblad);
        GeselecteerdTabblad = Tabbladen[((index + stap) % Tabbladen.Count + Tabbladen.Count) % Tabbladen.Count];
    }

    partial void OnGeselecteerdTabbladChanged(TabbladViewModel? oldValue, TabbladViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= ActiefTabbladGewijzigd;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += ActiefTabbladGewijzigd;
        }
    }

    private void ActiefTabbladGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabbladViewModel.KanOpslaan))
        {
            OpslaanCommand.NotifyCanExecuteChanged();
        }
    }

    // ---- Sessiebeheer ----------------------------------------------------------------------

    private async Task OpenAsync(string pad)
    {
        if (!await SluitSessieAsync())
        {
            return;
        }

        var naam = Path.GetFileName(pad);
        string? foutmelding = null;
        while (true)
        {
            var invoer = _dialogen.VraagWachtwoord(naam, foutmelding);
            if (invoer is null)
            {
                return;
            }

            try
            {
                IDatabaseSessie sessie;
                if (invoer.HerstelGevraagd)
                {
                    var herstel = _dialogen.VraagHerstel(naam);
                    if (herstel is null)
                    {
                        foutmelding = null;
                        continue;
                    }

                    sessie = await _beheer.OpenMetHerstelsleutelAsync(pad, herstel.Herstelsleutel, herstel.NieuwWachtwoord);
                    ShellLog.HerstelsleutelGebruikt(_logger);
                    _dialogen.ToonInformatie("Wachtwoord gewijzigd", "Het nieuwe wachtwoord is ingesteld. Uw herstelsleutel blijft geldig.");
                }
                else
                {
                    sessie = await _beheer.OpenAsync(pad, invoer.Wachtwoord ?? "");
                }

                var zelfdeAlsVorige = string.Equals(pad, _instellingen.LaatsteDatabase, StringComparison.OrdinalIgnoreCase);
                await NaOpenenAsync(sessie, herstelTabbladen: zelfdeAlsVorige);
                return;
            }
            catch (OngeldigeSleutelException ex)
            {
                ShellLog.OnjuisteSleutel(_logger);
                foutmelding = ex.Message;
            }
            catch (Exception ex) when (ex is DatabaseBestandException or ArgumentException or IOException or UnauthorizedAccessException)
            {
                ShellLog.Mislukt(_logger, ex, "Openen van database");
                _dialogen.ToonFout("Database openen", ex.Message);
                return;
            }
        }
    }

    private async Task NaOpenenAsync(IDatabaseSessie sessie, bool herstelTabbladen)
    {
        _sessie = sessie;
        foreach (var administratie in await sessie.Administraties.HaalAlleAsync())
        {
            var vm = new AdministratieViewModel(administratie);
            vm.PropertyChanged += AdministratieGewijzigd;
            Administraties.Add(vm);
        }

        var teHerstellen = herstelTabbladen ? _instellingen.OpenTabbladen : [];
        _instellingen = _instellingen with { LaatsteDatabase = sessie.Pad, OpenTabbladen = teHerstellen };
        _opslag.Bewaar(_instellingen);

        DatabaseNaam = Path.GetFileName(sessie.Pad);
        BouwModuleMenu();
        foreach (var tab in teHerstellen)
        {
            var verzoek = new ModuleVerzoek(tab.Module, tab.Administratie);
            if (KanModuleOpenen(verzoek))
            {
                OpenModule(verzoek);
            }
        }

        GeselecteerdTabblad = _overzicht;
        Statusmelding = "Gereed";
        ShellLog.DatabaseGeopend(_logger, DatabaseNaam);
    }

    /// <returns><c>false</c> als de gebruiker het sluiten annuleert.</returns>
    private async Task<bool> SluitSessieAsync()
    {
        if (_sessie is null)
        {
            return true;
        }

        foreach (var tab in Tabbladen.ToList())
        {
            if (!await MagTabbladSluitenAsync(tab))
            {
                GeselecteerdTabblad = tab;
                return false;
            }
        }

        _instellingen = _instellingen with
        {
            OpenTabbladen = Tabbladen
                .Where(t => t.Module.Soort != ModuleSoort.Overzicht)
                .Select(t => new OpenTabblad(t.Module.Soort, t.Administratie?.Soort))
                .ToList(),
        };
        _opslag.Bewaar(_instellingen);

        foreach (var tab in Tabbladen.Where(t => t != _overzicht).ToList())
        {
            Tabbladen.Remove(tab);
            tab.Opruimen();
        }

        GeselecteerdTabblad = _overzicht;
        foreach (var administratie in Administraties)
        {
            administratie.PropertyChanged -= AdministratieGewijzigd;
        }

        Administraties.Clear();
        await _sessie.DisposeAsync();
        _sessie = null;
        DatabaseNaam = null;
        BouwModuleMenu();
        ShellLog.DatabaseGesloten(_logger);
        return true;
    }

    private async Task<bool> MagTabbladSluitenAsync(TabbladViewModel tab)
    {
        if (!tab.HeeftOnopgeslagenWijzigingen)
        {
            return true;
        }

        GeselecteerdTabblad = tab;
        return _dialogen.VraagOmOpslaan(tab.Titel) switch
        {
            OpslaanKeuze.Opslaan => await tab.BewaarAsync(),
            OpslaanKeuze.NietOpslaan => true,
            _ => false,
        };
    }

    private void AdministratieGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, ActieveAdministratie))
        {
            OnPropertyChanged(nameof(ActieveAdministratieNaam));
            OnPropertyChanged(nameof(Venstertitel));
        }
    }

    private void BouwModuleMenu()
    {
        ModuleMenu.Clear();
        foreach (var definitie in ModuleCatalogus.Alle.Where(m => m.Sneltoetsnummer is not null))
        {
            var sneltoets = $"Ctrl+{definitie.Sneltoetsnummer}";
            if (definitie.AlleenVoor is { } vast)
            {
                ModuleMenu.Add(new ModuleMenuItem(
                    definitie.Titel, definitie.Pictogram, sneltoets, OpenModuleCommand, new ModuleVerzoek(definitie.Soort, vast), []));
            }
            else if (definitie.PerAdministratie && Administraties.Count > 0)
            {
                var kinderen = Administraties
                    .Select(a => ModuleMenuItem.VoorAdministratie(a, OpenModuleCommand, new ModuleVerzoek(definitie.Soort, a.Soort)))
                    .ToList();
                ModuleMenu.Add(new ModuleMenuItem(definitie.Titel, definitie.Pictogram, sneltoets, null, null, kinderen));
            }
            else
            {
                ModuleMenu.Add(new ModuleMenuItem(
                    definitie.Titel, definitie.Pictogram, sneltoets, OpenModuleCommand, new ModuleVerzoek(definitie.Soort, null), []));
            }
        }
    }
}
