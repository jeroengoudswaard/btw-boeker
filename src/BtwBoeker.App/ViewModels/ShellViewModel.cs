using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using BtwBoeker.App.Diensten;
using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.App.ViewModels.Inrichting;
using BtwBoeker.Application.Beveiliging;
using BtwBoeker.Application.Database;
using BtwBoeker.Application.Inrichting;
using BtwBoeker.Application.Instellingen;
using BtwBoeker.Application.Modules;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.App.ViewModels;

public sealed record ModuleVerzoek(ModuleSoort Module, AdministratieSoort? Administratie)
{
    /// <summary>Voor gebruik in XAML (Extra › Instellingen, Ctrl+,).</summary>
    public static ModuleVerzoek Instellingen { get; } = new(ModuleSoort.Instellingen, null);
}

/// <summary>
/// Hoofdvenster: database-sessie, navigatiebalk, menu's en statusregel.
/// De geopende pagina's blijven bewaard in <see cref="Tabbladen"/> (de naam stamt uit de tijd van de tabbladen).
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IStartwizardRegie
{
    private readonly IDatabaseBeheer _beheer;
    private readonly IInstellingenOpslag _opslag;
    private readonly IDialoogService _dialogen;
    private readonly IThemaService _thema;
    private readonly BoekhoudingHulpmiddelen _boekhouding;
    private readonly TimeProvider _tijd;
    private readonly ILogger<ShellViewModel> _logger;
    private readonly OverzichtViewModel _overzicht;
    private GebruikersInstellingen _instellingen = new();
    private IDatabaseSessie? _sessie;
    private bool _startwizardActief;

    public ShellViewModel(
        IDatabaseBeheer beheer,
        IInstellingenOpslag opslag,
        IDialoogService dialogen,
        IThemaService thema,
        BoekhoudingHulpmiddelen boekhouding,
        TimeProvider tijd,
        ILogger<ShellViewModel> logger)
    {
        _beheer = beheer;
        _opslag = opslag;
        _dialogen = dialogen;
        _thema = thema;
        _boekhouding = boekhouding;
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
    [NotifyCanExecuteChangedFor(nameof(OpslaanCommand), nameof(AfdrukkenCommand), nameof(ExporteerSjabloonCommand), nameof(ExporteerXafCommand))]
    public partial TabbladViewModel? GeselecteerdTabblad { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDatabaseOpen), nameof(Venstertitel), nameof(HeeftLaatsteDatabase), nameof(LaatsteDatabaseNaam), nameof(LaatsteDatabaseMap))]
    [NotifyCanExecuteChangedFor(
        nameof(OpenModuleCommand), nameof(OpenModuleViaSneltoetsCommand), nameof(SluitDatabaseCommand), nameof(MaakBackupCommand),
        nameof(ImporteerBankafschriftCommand), nameof(ImporteerSjabloonCommand))]
    public partial string? DatabaseNaam { get; set; }

    [ObservableProperty]
    public partial Thema Thema { get; set; }

    /// <summary>Expert of Eenvoudig voor het tabblad Financiën; alleen visueel.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMenubalkZichtbaar))]
    public partial Weergavemodus Weergave { get; set; }

    [ObservableProperty]
    public partial string Statusmelding { get; set; } = "Gereed";

    /// <summary>✓ gelukt, ⚠ waarschuwing, ✕ fout; zonder teken bij een gewone melding.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Statusteken))]
    public partial Statussoort Statussoort { get; private set; }

    public string Statusteken => Statussoort switch
    {
        Statussoort.Gelukt => "✓",
        Statussoort.Waarschuwing => "⚠",
        Statussoort.Fout => "✕",
        _ => string.Empty,
    };

    public bool IsDatabaseOpen => DatabaseNaam is not null;

    /// <summary>De laatst gebruikte database onder de tegels van het welkomstscherm, als hij nog bestaat.</summary>
    public bool HeeftLaatsteDatabase => !IsDatabaseOpen && _instellingen.LaatsteDatabase is { } pad && _beheer.Bestaat(pad);

    public string? LaatsteDatabaseNaam => HeeftLaatsteDatabase ? Path.GetFileName(_instellingen.LaatsteDatabase) : null;

    /// <summary>De twee mappen boven het bestand, bijvoorbeeld "Documenten › BTW-Boeker".</summary>
    public string? LaatsteDatabaseMap
    {
        get
        {
            if (!HeeftLaatsteDatabase || Path.GetDirectoryName(_instellingen.LaatsteDatabase) is not { Length: > 0 } map)
            {
                return null;
            }

            var delen = map.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" › ", delen.TakeLast(2));
        }
    }

    /// <summary>Besluit 26: de ene administratie van de database geldt voor de hele app.</summary>
    public AdministratieViewModel? ActieveAdministratie => GekozenAdministratie;

    public string? ActieveAdministratieNaam => ActieveAdministratie?.Naam;

    public string Boekjaartekst => $"Boekjaar {_tijd.GetLocalNow().Year.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Windows-conventie: "Document - Applicatie".</summary>
    public string Venstertitel => (DatabaseNaam, ActieveAdministratieNaam) switch
    {
        (null, _) => "BTW-Boeker",
        (var db, null) => $"{db} - BTW-Boeker",
        (var db, var adm) => $"{adm} - {db} - BTW-Boeker",
    };

    public async Task InitialiseerAsync()
    {
        _instellingen = _opslag.Laad();
        Thema = _instellingen.Thema;
        _thema.PasToe(Thema);
        Weergave = _instellingen.Weergave;
        _boekhouding.Woorden.Weergave = Weergave;
        BouwModuleMenu();

        if (_instellingen.LaatsteDatabase is { } pad && _beheer.Bestaat(pad))
        {
            await OpenAsync(pad);
            OnPropertyChanged(nameof(HeeftLaatsteDatabase));
            OnPropertyChanged(nameof(LaatsteDatabaseNaam));
            OnPropertyChanged(nameof(LaatsteDatabaseMap));
        }
        else if (!_instellingen.StartwizardGetoond)
        {
            // De eerste start opent de startwizard, één keer.
            _instellingen = _instellingen with { StartwizardGetoond = true };
            _opslag.Bewaar(_instellingen);
            await ToonStartwizardAsync(Startstap.Welkom);
        }
    }

    /// <summary>Aangeroepen bij het sluiten van het hoofdvenster.</summary>
    /// <returns><c>false</c> als de gebruiker het afsluiten annuleert.</returns>
    public Task<bool> MagAfsluitenAsync() => SluitSessieAsync();

    // ---- Bestand ---------------------------------------------------------------------------

    [RelayCommand]
    private async Task OpenLaatsteDatabaseAsync()
    {
        if (HeeftLaatsteDatabase && _instellingen.LaatsteDatabase is { } pad)
        {
            await OpenAsync(pad);
        }
    }

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
            Herstelsleutel herstelsleutel;
            using (Meetpunt.Start(_logger, "Aanmaken database"))
            {
                herstelsleutel = await _beheer.MaakAanAsync(pad, wachtwoord);
            }

            ShellLog.DatabaseAangemaakt(_logger, Path.GetFileName(pad));
            _dialogen.ToonHerstelsleutel(herstelsleutel);
            _instellingen = _instellingen with { LaatstePagina = null };
            await NaOpenenAsync(await _beheer.OpenAsync(pad, wachtwoord), herstelPagina: false);
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
            ZetStatus(Statussoort.Gelukt, "Opgeslagen.");
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
            $"{Path.GetFileNameWithoutExtension(_sessie.Pad)}-{_tijd.GetLocalNow():yyyy-MM-dd}.btwbackup");
        var doel = _dialogen.KiesBackupDoel(voorstel);
        if (doel is null)
        {
            return;
        }

        try
        {
            await _sessie.MaakBackupAsync(doel);
            ShellLog.BackupGemaakt(_logger);
            ZetStatus(Statussoort.Gelukt, $"Back-up opgeslagen als {Path.GetFileName(doel)}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShellLog.Mislukt(_logger, ex, "Back-up maken");
            _dialogen.ToonFout("Back-up maken", $"De back-up kon niet worden opgeslagen.\n\n{ex.Message}");
        }
    }

    /// <summary>
    /// Bestand › Importeren › Bankafschrift (CAMT.053): opent Boekhouding en start de bankimport.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsDatabaseOpen))]
    private async Task ImporteerBankafschriftAsync()
    {
        var verzoek = new ModuleVerzoek(ModuleSoort.Boekhouding, ActieveAdministratie?.Soort ?? AdministratieSoort.Vereniging);
        if (!KanModuleOpenen(verzoek))
        {
            return;
        }

        OpenModule(verzoek);
        if (GeselecteerdTabblad is BoekhoudingViewModel boekhouding)
        {
            if (boekhouding.VernieuwCommand.ExecutionTask is { } laden)
            {
                await laden;
            }

            await boekhouding.BankInlezenCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// Bestand › Importeren › Rekeningschema-sjabloon: opent Boekhouding voor de administratie van het actieve
    /// tabblad, en richt in met een sjabloonbestand.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsDatabaseOpen))]
    private async Task ImporteerSjabloonAsync()
    {
        var verzoek = new ModuleVerzoek(ModuleSoort.Boekhouding, ActieveAdministratie?.Soort ?? AdministratieSoort.Vereniging);
        if (!KanModuleOpenen(verzoek))
        {
            return;
        }

        OpenModule(verzoek);
        if (GeselecteerdTabblad is BoekhoudingViewModel boekhouding)
        {
            if (boekhouding.VernieuwCommand.ExecutionTask is { } laden)
            {
                await laden;
            }

            await boekhouding.SjabloonInlezenCommand.ExecuteAsync(null);
        }
    }

    /// <summary>Bestand › Exporteren › Rekeningschema als sjabloon, van het actieve tabblad Boekhouding.</summary>
    [RelayCommand(CanExecute = nameof(KanAfdrukken))]
    private Task ExporteerSjabloonAsync() =>
        GeselecteerdTabblad is BoekhoudingViewModel boekhouding ? boekhouding.ExporteerSjabloonCommand.ExecuteAsync(null) : Task.CompletedTask;

    /// <summary>Bestand › Exporteren › Auditfile (XAF): het gekozen boekjaar van het tabblad Boekhouding.</summary>
    [RelayCommand(CanExecute = nameof(KanAfdrukken))]
    private Task ExporteerXafAsync() =>
        GeselecteerdTabblad is BoekhoudingViewModel boekhouding ? boekhouding.ExporteerXafCommand.ExecuteAsync(null) : Task.CompletedTask;

    /// <summary>Bestand › Afdrukken (Ctrl+P): het rapport of de grootboekkaart van het tabblad Boekhouding.</summary>
    [RelayCommand(CanExecute = nameof(KanAfdrukken))]
    private Task AfdrukkenAsync() =>
        GeselecteerdTabblad is BoekhoudingViewModel boekhouding ? boekhouding.AfdrukkenCommand.ExecuteAsync(null) : Task.CompletedTask;

    private bool KanAfdrukken() => GeselecteerdTabblad is BoekhoudingViewModel;

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

    // ---- Startwizard ----------------------------------------------------

    /// <summary>Bestand › Startwizard: verder waar de gebruiker gebleven was, anders vanaf Welkom.</summary>
    [RelayCommand]
    private async Task StartwizardAsync()
    {
        var voortgang = _sessie is null ? Startwizardvoortgang.Nieuw : await _sessie.Startwizard.LaadAsync();
        await ToonStartwizardAsync(voortgang is { KanHervatten: true, Stap: { } stap } ? stap : Startstap.Welkom);
    }

    private async Task ToonStartwizardAsync(Startstap begin)
    {
        if (_startwizardActief)
        {
            return;
        }

        _startwizardActief = true;
        try
        {
            var wizard = new StartwizardViewModel(this, _tijd, begin);
            await wizard.InitialiseerAsync();
            _dialogen.ToonStartwizard(wizard);
        }
        finally
        {
            _startwizardActief = false;
        }
    }

    IDatabaseSessie? IStartwizardRegie.Sessie => _sessie;

    IReadOnlyList<AdministratieViewModel> IStartwizardRegie.Administraties => Administraties;

    Task IStartwizardRegie.NieuweDatabaseAsync() => NieuweDatabaseCommand.ExecuteAsync(null);

    Task IStartwizardRegie.OpenDatabaseAsync() => OpenDatabaseCommand.ExecuteAsync(null);

    Task IStartwizardRegie.RichtBoekhoudingInAsync(AdministratieSoort administratie, Boekhoudingkeuze keuze) =>
        InBoekhoudingAsync(administratie, boekhouding => keuze switch
        {
            Boekhoudingkeuze.Sjabloon => boekhouding.SjabloonInlezenCommand.ExecuteAsync(null),
            _ => boekhouding.LeegBeginnenCommand.ExecuteAsync(null),
        });

    Task IStartwizardRegie.ImporteerBankafschriftAsync(AdministratieSoort administratie) =>
        InBoekhoudingAsync(administratie, boekhouding => boekhouding.BankInlezenCommand.ExecuteAsync(null));

    /// <summary>Opent Boekhouding voor een administratie, wacht tot die geladen is en voert dan een actie uit.</summary>
    private async Task InBoekhoudingAsync(AdministratieSoort administratie, Func<BoekhoudingViewModel, Task> actie)
    {
        var verzoek = new ModuleVerzoek(ModuleSoort.Boekhouding, administratie);
        if (!KanModuleOpenen(verzoek))
        {
            return;
        }

        OpenModule(verzoek);
        if (GeselecteerdTabblad is BoekhoudingViewModel boekhouding)
        {
            if (boekhouding.VernieuwCommand.ExecutionTask is { } laden)
            {
                await laden;
            }

            await actie(boekhouding);
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

    /// <summary>Beeld › Weergave: direct wisselen, zonder herstart; de keuze wordt onthouden.</summary>
    [RelayCommand]
    private void KiesWeergave(Weergavemodus weergave)
    {
        Weergave = weergave;
        _boekhouding.Woorden.Weergave = weergave;
        _instellingen = _instellingen with { Weergave = weergave };
        _opslag.Bewaar(_instellingen);
        BouwModuleMenu();
        BouwNavigatie();
    }

    // ---- Tabbladen -------------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(KanModuleOpenen))]
    private void OpenModule(ModuleVerzoek verzoek)
    {
        if (verzoek.Module == ModuleSoort.Overzicht)
        {
            GeselecteerdTabblad = _overzicht;
            _ = VernieuwStartkaartenAsync();
            return;
        }

        // Een pagina van een andere administratie opent, dan wisselt de keuze bovenaan mee.
        if (verzoek.Administratie is { } gevraagd && GekozenAdministratie?.Soort != gevraagd)
        {
            KiesAdministratieZonderWisselen(gevraagd);
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
            ModuleSoort.Boekhouding => new BoekhoudingViewModel(_sessie!, _dialogen, _tijd, administratie!, _boekhouding, _logger),
            _ => new ModuleInOntwikkelingViewModel(definitie, administratie),
        };

        Tabbladen.Add(tab);
        GeselecteerdTabblad = tab;
        switch (tab)
        {
            case BoekhoudingViewModel boekhouding:
                boekhouding.VernieuwCommand.Execute(null);
                break;
            case InstellingenViewModel instellingen:
                break;
        }
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

    /// <summary>Ctrl+1 (Boekhouding). Financiële modules openen voor de gekozen administratie.</summary>
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


    /// <summary>Ctrl+Tab: de volgende pagina; een gesloten modulekop opent de standaardpagina van die module.</summary>
    [RelayCommand]
    private void VolgendePagina() => SelecteerRelatief(+1);

    /// <summary>Ctrl+Shift+Tab: de vorige regel in de navigatiebalk.</summary>
    [RelayCommand]
    private void VorigePagina() => SelecteerRelatief(-1);

    private void SelecteerRelatief(int stap)
    {
        var actieveModule = GeselecteerdTabblad?.Module.Soort;
        var paginas = Navigatie
            .Where(n => !n.IsMeer && (!n.HeeftOnderdelen || n.Module != actieveModule))
            .ToList();
        if (paginas.Count == 0)
        {
            return;
        }

        var index = GeselecteerdeNavigatie is null ? 0 : paginas.IndexOf(GeselecteerdeNavigatie);
        if (index < 0)
        {
            index = 0;
        }

        GeselecteerdeNavigatie = paginas[((index + stap) % paginas.Count + paginas.Count) % paginas.Count];
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

        BouwNavigatie();
    }

    private void ActiefTabbladGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TabbladViewModel.KanOpslaan):
                OpslaanCommand.NotifyCanExecuteChanged();
                break;
            case nameof(TabbladViewModel.Onderdeel):
                // Een onderdeel onder Meer… moet zichtbaar worden; anders alleen markeren.
                BouwNavigatie();
                break;
            case nameof(IPaginameldingen.Melding) when sender is IPaginameldingen { Melding: { Length: > 0 } melding }:
                ZetStatus(Statussoort.Gelukt, melding);
                break;
            case nameof(IPaginameldingen.Waarschuwing) when sender is IPaginameldingen { Waarschuwing: { Length: > 0 } waarschuwing }:
                ZetStatus(Statussoort.Waarschuwing, waarschuwing);
                break;
            case nameof(IPaginameldingen.Foutmelding) when sender is IPaginameldingen { Foutmelding: { Length: > 0 } fout }:
                ZetStatus(Statussoort.Fout, fout);
                break;
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
                    using (Meetpunt.Start(_logger, "Openen database (sleutel en migraties)"))
                    {
                        sessie = await _beheer.OpenAsync(pad, invoer.Wachtwoord ?? "");
                    }

                }

                var zelfdeAlsVorige = string.Equals(pad, _instellingen.LaatsteDatabase, StringComparison.OrdinalIgnoreCase);
                using (Meetpunt.Start(_logger, "Na openen (eerste pagina)"))
                {
                    await NaOpenenAsync(sessie, herstelPagina: zelfdeAlsVorige);
                }

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

    private async Task NaOpenenAsync(IDatabaseSessie sessie, bool herstelPagina)
    {
        _sessie = sessie;
        _uiContext = SynchronizationContext.Current;
        sessie.Gewijzigd += SessieGewijzigd;
        foreach (var administratie in await sessie.Administraties.HaalAlleAsync())
        {
            var vm = new AdministratieViewModel(administratie);
            vm.PropertyChanged += AdministratieGewijzigd;
            Administraties.Add(vm);
        }

        var laatste = herstelPagina ? _instellingen.LaatstePagina : null;
        _instellingen = _instellingen with { LaatsteDatabase = sessie.Pad, LaatstePagina = laatste };
        _opslag.Bewaar(_instellingen);

        // Besluit 26: de ene administratie (een oudere laatste keuze wordt genegeerd).
        KiesAdministratieZonderWisselen(laatste?.Administratie ?? AdministratieSoort.Vereniging);
        DatabaseNaam = Path.GetFileName(sessie.Pad);
        BouwModuleMenu();
        GeselecteerdTabblad = _overzicht;
        if (laatste is { Module: not ModuleSoort.Overzicht } && KanModuleOpenen(new ModuleVerzoek(laatste.Module, laatste.Administratie)))
        {
            OpenModule(new ModuleVerzoek(laatste.Module, laatste.Administratie));
            if (laatste.Onderdeel is { } onderdeel && GeselecteerdTabblad is { } pagina)
            {
                pagina.Onderdeel = onderdeel;
            }
        }
        else
        {
            _ = VernieuwStartkaartenAsync();
        }

        ZetStatus(Statussoort.Info, "Gereed");
        ShellLog.DatabaseGeopend(_logger, DatabaseNaam);
        // Was de startwizard met "Later verder" gesloten, dan aanbieden om daar verder te gaan.
        if (!_startwizardActief && await sessie.Startwizard.LaadAsync() is { KanHervatten: true, Stap: { } stap }
            && _dialogen.Bevestig("Startwizard", $"De startwizard is nog niet afgerond; u was gebleven bij de stap '{Stapnaam(stap)}'. Wilt u daar verder gaan?"))
        {
            await ToonStartwizardAsync(stap);
        }
    }

    private static string Stapnaam(Startstap stap) => stap.ToString();

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
            LaatstePagina = GeselecteerdTabblad is { Module.Soort: not ModuleSoort.Overzicht } pagina
                ? new LaatstePagina(pagina.Module.Soort, pagina.Administratie?.Soort, pagina.Onderdeel)
                : null,
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
        KiesAdministratieZonderWisselen(null);
        _overzicht.Kaarten.Clear();
        _sessie.Gewijzigd -= SessieGewijzigd;
        await _sessie.DisposeAsync();
        _sessie = null;
        DatabaseNaam = null;
        BouwModuleMenu();
        BouwNavigatie();
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
        foreach (var module in ModuleCatalogus.Alle.Where(m => m.Sneltoetsnummer is not null))
        {
            // Financiën volgt de weergave; de overige modules houden hun naam.
            var definitie = module.Soort == ModuleSoort.Boekhouding ? module with { Titel = _boekhouding.Woorden["Boekhouding"] } : module;
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
