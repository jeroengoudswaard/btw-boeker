using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Verenigd.App.Diensten;
using Verenigd.App.ViewModels.Boekhouding;
using Verenigd.App.ViewModels.Contributie;
using Verenigd.App.ViewModels.Email;
using Verenigd.App.ViewModels.Inrichting;
using Verenigd.App.ViewModels.Leden;
using Verenigd.Application.Beveiliging;
using Verenigd.Application.Database;
using Verenigd.Application.Import;
using Verenigd.Application.Inrichting;
using Verenigd.Application.Instellingen;
using Verenigd.Application.Modules;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.ViewModels;

public sealed record ModuleVerzoek(ModuleSoort Module, AdministratieSoort? Administratie)
{
    /// <summary>Voor gebruik in XAML (Extra › Instellingen, Ctrl+,).</summary>
    public static ModuleVerzoek Instellingen { get; } = new(ModuleSoort.Instellingen, null);
}

/// <summary>
/// Hoofdvenster: database-sessie, navigatiebalk met administratiewisselaar (besluiten 168 en 177–183), menu's en statusregel.
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
    [NotifyCanExecuteChangedFor(nameof(OpslaanCommand), nameof(AfdrukkenCommand), nameof(ExporteerSjabloonCommand))]
    public partial TabbladViewModel? GeselecteerdTabblad { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDatabaseOpen), nameof(Venstertitel))]
    [NotifyCanExecuteChangedFor(
        nameof(OpenModuleCommand), nameof(OpenModuleViaSneltoetsCommand), nameof(SluitDatabaseCommand), nameof(MaakBackupCommand),
        nameof(ImporteerSol3Command), nameof(ImporteerBoekhoudingCommand), nameof(ImporteerBankafschriftCommand), nameof(ImporteerSjabloonCommand), nameof(ImporteerHistorieCommand), nameof(NieuweIncassorunCommand))]
    public partial string? DatabaseNaam { get; set; }

    [ObservableProperty]
    public partial Thema Thema { get; set; }

    /// <summary>Expert of Eenvoudig voor het tabblad Financiën (besluit 127); alleen visueel.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMenubalkZichtbaar))]
    public partial Weergavemodus Weergave { get; set; }

    [ObservableProperty]
    public partial string Statusmelding { get; set; } = "Gereed";

    /// <summary>Besluit 168: ✓ gelukt, ⚠ waarschuwing, ✕ fout; zonder teken bij een gewone melding.</summary>
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

    /// <summary>Besluit 177: de administratie uit de wisselaar bovenaan geldt voor de hele app.</summary>
    public AdministratieViewModel? ActieveAdministratie => GekozenAdministratie;

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
        Weergave = _instellingen.Weergave;
        _boekhouding.Woorden.Weergave = Weergave;
        BouwModuleMenu();

        if (_instellingen.LaatsteDatabase is { } pad && _beheer.Bestaat(pad))
        {
            await OpenAsync(pad);
        }
        else if (!_instellingen.StartwizardGetoond)
        {
            // Besluit 169: de eerste start opent de startwizard, één keer.
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
            ZetStatus(Statussoort.Gelukt, $"Back-up opgeslagen als {Path.GetFileName(doel)}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShellLog.Mislukt(_logger, ex, "Back-up maken");
            _dialogen.ToonFout("Back-up maken", $"De back-up kon niet worden opgeslagen.\n\n{ex.Message}");
        }
    }

    /// <summary>Bestand › Importeren › SOL3-leden. Na een geslaagde import wordt het tabblad Leden getoond en vernieuwd.</summary>
    [RelayCommand(CanExecute = nameof(IsDatabaseOpen))]
    private async Task ImporteerSol3Async()
    {
        if (_sessie is null)
        {
            return;
        }

        var bekend = (await _sessie.Leden.HaalAlleAsync()).Select(p => p.Id).ToHashSet();
        var wizard = new ImportWizardViewModel(new Sol3ImportDienst(_sessie, _tijd), _dialogen);
        if (!_dialogen.ToonImportWizard(wizard))
        {
            return;
        }

        if (wizard.Resultaat is { } resultaat)
        {
            ShellLog.Sol3Geimporteerd(_logger, resultaat.Nieuw, resultaat.Gewijzigd, resultaat.Uitgeschreven);
            ZetStatus(Statussoort.Gelukt, string.Create(
                CultureInfo.CurrentCulture,
                $"Import voltooid: {resultaat.Nieuw} nieuw, {resultaat.Gewijzigd} gewijzigd, {resultaat.Uitgeschreven} uitgeschreven."));
        }

        var verzoek = new ModuleVerzoek(ModuleSoort.Leden, null);
        if (Tabbladen.OfType<LedenViewModel>().FirstOrDefault() is { } leden)
        {
            GeselecteerdTabblad = leden;
            await leden.VernieuwCommand.ExecuteAsync(null);
        }
        else
        {
            OpenModule(verzoek);
            if (GeselecteerdTabblad is LedenViewModel nieuw && nieuw.VernieuwCommand.ExecutionTask is { } laden)
            {
                await laden;
            }
        }

        await BiedWelkomstmailAanAsync(bekend);
    }

    /// <summary>Besluit 205: na een import met nieuwe leden een welkomstmail aanbieden.</summary>
    private async Task BiedWelkomstmailAanAsync(HashSet<Guid> bekend)
    {
        var nieuw = (await _sessie!.Leden.HaalAlleAsync()).Where(p => !bekend.Contains(p.Id) && !p.IsUitgeschreven).ToList();
        var vraag = nieuw.Count == 1 ? "Er is 1 nieuw lid." : $"Er zijn {nieuw.Count} nieuwe leden.";
        if (nieuw.Count == 0 || !_dialogen.Bevestig("Welkomstmail", $"{vraag} Wilt u een welkomstmail opstellen?"))
        {
            return;
        }

        var vandaag = DateOnly.FromDateTime(_tijd.GetLocalNow().DateTime);
        await OpenEmailAsync(new EmailVoorstel(Domain.Communicatie.Emailsoort.Welkom,
            Application.Email.Ontvangers.VoorLeden(nieuw, Application.Email.Ontvangerkeuze.Standaard, vandaag, Verenigingsnaam),
            nieuw.Count == 1 ? "Welkomstmail voor 1 nieuw lid" : $"Welkomstmail voor {nieuw.Count} nieuwe leden"));
    }

    /// <summary>De naam in {vereniging} van een e-mail: de naam van de administratie die verstuurt.</summary>
    private string Administratienaam(AdministratieSoort soort) =>
        Administraties.FirstOrDefault(a => a.Soort == soort)?.Naam ?? (soort == AdministratieSoort.Stichting ? "de stichting" : "de vereniging");

    private string Verenigingsnaam => Administratienaam(AdministratieSoort.Vereniging);

    /// <summary>Opent E-mail › Opstellen met een bericht dat een andere pagina heeft voorbereid.</summary>
    private async Task OpenEmailAsync(EmailVoorstel voorstel)
    {
        var verzoek = new ModuleVerzoek(ModuleSoort.Email, null);
        if (!KanModuleOpenen(verzoek))
        {
            return;
        }

        OpenModule(verzoek);
        if (GeselecteerdTabblad is EmailViewModel email)
        {
            if (email.VernieuwCommand.ExecutionTask is { } laden)
            {
                await laden;
            }

            email.BereidVoor(voorstel);
            BouwNavigatie();
        }
    }

    /// <summary>
    /// Bestand › Importeren › Boekhouding uit Excel (besluit 62): opent Boekhouding voor de administratie van het actieve
    /// tabblad, anders de Vereniging (besluit 17), en start de importwizard.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsDatabaseOpen))]
    private async Task ImporteerBoekhoudingAsync()
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

            await boekhouding.OvernemenUitExcelCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// Bestand › Importeren › Bankafschrift (CAMT.053): opent Boekhouding voor de administratie van het actieve tabblad,
    /// anders de Vereniging (besluit 17), en start de bankimport.
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
    /// Bestand › Importeren › Rekeningschema-sjabloon (besluit 116): opent Boekhouding voor de administratie van het actieve
    /// tabblad, anders de Vereniging (besluit 17), en richt in met een sjabloonbestand.
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

    /// <summary>
    /// Bestand › Importeren › Historie uit Excel (besluit 125): opent Boekhouding voor de administratie van het actieve
    /// tabblad, anders de Vereniging (besluit 17), en neemt de historie over.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsDatabaseOpen))]
    private async Task ImporteerHistorieAsync()
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

            await boekhouding.HistorieInlezenCommand.ExecuteAsync(null);
        }
    }

    /// <summary>Bestand › Exporteren › Rekeningschema als sjabloon, van het actieve tabblad Boekhouding.</summary>
    [RelayCommand(CanExecute = nameof(KanAfdrukken))]
    private Task ExporteerSjabloonAsync() =>
        GeselecteerdTabblad is BoekhoudingViewModel boekhouding ? boekhouding.ExporteerSjabloonCommand.ExecuteAsync(null) : Task.CompletedTask;

    /// <summary>Bestand › Afdrukken (Ctrl+P): het rapport of de grootboekkaart van het tabblad Boekhouding.</summary>
    [RelayCommand(CanExecute = nameof(KanAfdrukken))]
    private Task AfdrukkenAsync() =>
        GeselecteerdTabblad is BoekhoudingViewModel boekhouding ? boekhouding.AfdrukkenCommand.ExecuteAsync(null) : Task.CompletedTask;

    private bool KanAfdrukken() => GeselecteerdTabblad is BoekhoudingViewModel;

    /// <summary>Bestand › Nieuwe incassorun: opent Contributie · Vereniging en start de wizard.</summary>
    [RelayCommand(CanExecute = nameof(IsDatabaseOpen))]
    private async Task NieuweIncassorunAsync()
    {
        var verzoek = new ModuleVerzoek(ModuleSoort.Contributie, AdministratieSoort.Vereniging);
        if (!KanModuleOpenen(verzoek))
        {
            return;
        }

        OpenModule(verzoek);
        if (GeselecteerdTabblad is ContributieViewModel contributie)
        {
            if (contributie.VernieuwCommand.ExecutionTask is { } laden)
            {
                await laden;
            }

            await contributie.NieuweRunCommand.ExecuteAsync(null);
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

    // ---- Startwizard (besluiten 169–175) ----------------------------------------------------

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

    Task IStartwizardRegie.ImporteerLedenAsync() => ImporteerSol3Command.ExecuteAsync(null);

    Task IStartwizardRegie.RichtBoekhoudingInAsync(AdministratieSoort administratie, Boekhoudingkeuze keuze) =>
        InBoekhoudingAsync(administratie, boekhouding => keuze switch
        {
            Boekhoudingkeuze.Excel => boekhouding.OvernemenUitExcelCommand.ExecuteAsync(null),
            Boekhoudingkeuze.Standaard => boekhouding.StandaardInrichtenCommand.ExecuteAsync(null),
            Boekhoudingkeuze.Sjabloon => boekhouding.SjabloonInlezenCommand.ExecuteAsync(null),
            _ => boekhouding.LeegBeginnenCommand.ExecuteAsync(null),
        });

    Task IStartwizardRegie.ImporteerBankafschriftAsync(AdministratieSoort administratie) =>
        InBoekhoudingAsync(administratie, boekhouding => boekhouding.BankInlezenCommand.ExecuteAsync(null));

    void IStartwizardRegie.OpenTarieven()
    {
        var verzoek = new ModuleVerzoek(ModuleSoort.Contributie, AdministratieSoort.Vereniging);
        if (KanModuleOpenen(verzoek))
        {
            OpenModule(verzoek);
            if (GeselecteerdTabblad is ContributieViewModel contributie)
            {
                contributie.Subtab = ContributieSubtab.Tarieven;
            }
        }
    }

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

        // Besluit 177: een pagina van een andere administratie opent, dan wisselt de keuze bovenaan mee.
        if (verzoek.Administratie is { } gevraagd && GekozenAdministratie?.Soort != gevraagd)
        {
            KiesAdministratieZonderWisselen(gevraagd);
        }

        // Besluiten 157 en 163: Modules › Begroting (Ctrl+5) opent Boekhouding op de begroting, bij de Stichting op het meerjarenplan.
        if (verzoek.Module == ModuleSoort.Begroting)
        {
            OpenModule(verzoek with { Module = ModuleSoort.Boekhouding });
            if (GeselecteerdTabblad is BoekhoudingViewModel boekhouding)
            {
                boekhouding.Subtab = verzoek.Administratie == AdministratieSoort.Stichting ? BoekhoudingSubtab.Meerjarenplan : BoekhoudingSubtab.Begroting;
            }

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
            ModuleSoort.Instellingen => new InstellingenViewModel(
                _sessie!.Administraties, Administraties, new IncassoInstellingenViewModel(_sessie.Contributie, _tijd),
                [.. Administraties.Select(a => new FactuurInstellingenViewModel(new Application.Facturen.FactuurDienst(_sessie, _tijd), _dialogen, a.Soort, a.Naam))],
                [.. Administraties.Select(a => new EmailInstellingenViewModel(
                    new Application.Email.EmailDienst(_sessie, _boekhouding.Email, _tijd), _boekhouding.Google, _dialogen, a.Soort, a.Naam))],
                new GroepenInstellingenViewModel(_sessie.Leden)),
            ModuleSoort.Contributie => new ContributieViewModel(_sessie!, _dialogen, _tijd, administratie!, _logger),
            ModuleSoort.Leden => new LedenViewModel(_sessie!.Leden, _tijd),
            ModuleSoort.Boekhouding => new BoekhoudingViewModel(_sessie!, _dialogen, _tijd, administratie!, _boekhouding, _logger),
            ModuleSoort.Email => new EmailViewModel(_sessie!, _dialogen, _tijd, _boekhouding, Administratienaam),
            ModuleSoort.Webapp => new Webapp.WebappViewModel(_sessie!, _dialogen, _tijd, _boekhouding.Webapp),
            _ => new ModuleInOntwikkelingViewModel(definitie, administratie),
        };
        tab.EmailAanbieden = OpenEmailAsync;
        Tabbladen.Add(tab);
        GeselecteerdTabblad = tab;
        switch (tab)
        {
            case LedenViewModel leden:
                leden.VernieuwCommand.Execute(null);
                break;
            case ContributieViewModel contributie:
                contributie.VernieuwCommand.Execute(null);
                break;
            case BoekhoudingViewModel boekhouding:
                boekhouding.VernieuwCommand.Execute(null);
                break;
            case InstellingenViewModel instellingen:
                instellingen.Incasso?.LaadCommand.Execute(null);
                foreach (var facturen in instellingen.Facturen)
                {
                    facturen.LaadCommand.Execute(null);
                }

                foreach (var email in instellingen.Emailkoppelingen)
                {
                    email.LaadCommand.Execute(null);
                }

                instellingen.Groepen?.LaadCommand.Execute(null);

                break;
            case EmailViewModel email:
                email.VernieuwCommand.Execute(null);
                break;
            case Webapp.WebappViewModel webapp:
                webapp.VernieuwCommand.Execute(null);
                break;
        }
    }

    /// <summary>
    /// Besluit 232: bij het openen van de database automatisch synchroniseren met de webapp, als de beheerder is ingelogd.
    /// Zonder internet of bij een gepauzeerd project alleen een waarschuwing in de statusregel; de rest van de app werkt gewoon.
    /// </summary>
    private async Task SynchroniseerWebappAsync(IDatabaseSessie sessie)
    {
        var dienst = new Application.Webapp.WebappDienst(sessie, _boekhouding.Webapp, _tijd);
        try
        {
            if (!(await dienst.LaadAsync()).IsGekoppeld)
            {
                return;
            }

            var resultaat = await dienst.SynchroniseerAsync();
            if (ReferenceEquals(_sessie, sessie))
            {
                ZetStatus(Statussoort.Gelukt, $"Webapp gesynchroniseerd: {resultaat.Leden} leden, {resultaat.OpenVoorstellen} voorstellen open.");
            }
        }
        catch (Application.Webapp.WebappException ex)
        {
            if (ReferenceEquals(_sessie, sessie))
            {
                ZetStatus(Statussoort.Waarschuwing, "Webapp: " + ex.Message);
            }
        }
        catch (ObjectDisposedException)
        {
            // De database is intussen gesloten.
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

    /// <summary>Ctrl+1...6. Financiële modules openen voor de gekozen administratie (besluit 177; anders de Vereniging).</summary>
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


    /// <summary>Ctrl+Tab: de volgende regel in de navigatiebalk.</summary>
    [RelayCommand]
    private void VolgendePagina() => SelecteerRelatief(+1);

    /// <summary>Ctrl+Shift+Tab: de vorige regel in de navigatiebalk.</summary>
    [RelayCommand]
    private void VorigePagina() => SelecteerRelatief(-1);

    private void SelecteerRelatief(int stap)
    {
        if (Navigatie.Count == 0)
        {
            return;
        }

        var index = GeselecteerdeNavigatie is null ? 0 : Navigatie.IndexOf(GeselecteerdeNavigatie);
        GeselecteerdeNavigatie = Navigatie[((index + stap) % Navigatie.Count + Navigatie.Count) % Navigatie.Count];
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
                SynchroniseerNavigatie();
                break;
            case nameof(IPaginameldingen.Melding) when sender is IPaginameldingen { Melding: { Length: > 0 } melding }:
                ZetStatus(Statussoort.Gelukt, melding);
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
                    sessie = await _beheer.OpenAsync(pad, invoer.Wachtwoord ?? "");
                }

                var zelfdeAlsVorige = string.Equals(pad, _instellingen.LaatsteDatabase, StringComparison.OrdinalIgnoreCase);
                await NaOpenenAsync(sessie, herstelPagina: zelfdeAlsVorige);
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
        foreach (var administratie in await sessie.Administraties.HaalAlleAsync())
        {
            var vm = new AdministratieViewModel(administratie);
            vm.PropertyChanged += AdministratieGewijzigd;
            Administraties.Add(vm);
        }

        var laatste = herstelPagina ? _instellingen.LaatstePagina : null;
        _instellingen = _instellingen with { LaatsteDatabase = sessie.Pad, LaatstePagina = laatste };
        _opslag.Bewaar(_instellingen);

        // Besluit 177: de wisselaar begint bij de laatst gekozen administratie, anders de Vereniging.
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
        _ = SynchroniseerWebappAsync(sessie);

        // Besluit 170: was de startwizard met "Later verder" gesloten, dan aanbieden om daar verder te gaan.
        if (!_startwizardActief && await sessie.Startwizard.LaadAsync() is { KanHervatten: true, Stap: { } stap }
            && _dialogen.Bevestig("Startwizard", $"De startwizard is nog niet afgerond; u was gebleven bij de stap '{Stapnaam(stap)}'. Wilt u daar verder gaan?"))
        {
            await ToonStartwizardAsync(stap);
        }
    }

    private static string Stapnaam(Startstap stap) => stap switch
    {
        Startstap.Administraties => "Vereniging",
        _ => stap.ToString(),
    };

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
            // Financiën volgt de weergave (besluit 127); de overige modules houden hun naam.
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
