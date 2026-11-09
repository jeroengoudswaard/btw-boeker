using Microsoft.Extensions.Logging.Abstractions;
using BtwBoeker.App.Diensten;
using BtwBoeker.App.ViewModels;
using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.Application.Instellingen;
using BtwBoeker.Application.Modules;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.App.Tests;

public sealed class ShellViewModelTests
{
    // Platformonafhankelijk, zodat Path.GetFileName ook op Linux "btw-boeker.btwdb" geeft (de tests draaien ook in de cloud).
    private static readonly string Pad = Path.Combine(Path.GetTempPath(), "Data", "btw-boeker.btwdb");
    private const string Wachtwoord = "geheim wachtwoord";

    private readonly FakeDatabaseBeheer _beheer = new();
    private readonly FakeDialoogService _dialogen = new();
    private readonly InMemoryInstellingenOpslag _instellingen = new();
    private readonly FakeThemaService _thema = new();

    private ShellViewModel MaakShell() =>
        new(_beheer, _instellingen, _dialogen, _thema, FakeBoekhouding.Hulpmiddelen(), new VasteTijd(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<ShellViewModel>.Instance);

    private async Task<ShellViewModel> MaakShellMetOpenDatabase()
    {
        _beheer.Voeg(Pad, Wachtwoord);
        _dialogen.BestaandDatabasebestand = Pad;
        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer(Wachtwoord, HerstelGevraagd: false));
        var shell = MaakShell();
        await shell.InitialiseerAsync();
        await shell.OpenDatabaseCommand.ExecuteAsync(null);
        Assert.True(shell.IsDatabaseOpen);
        return shell;
    }

    // ---- Opstarten -------------------------------------------------------------------------

    [Fact]
    public async Task Zonder_database_is_alleen_overzicht_open_en_zijn_modules_uitgeschakeld()
    {
        var shell = MaakShell();
        await shell.InitialiseerAsync();

        var tab = Assert.Single(shell.Tabbladen);
        Assert.Equal(ModuleSoort.Overzicht, tab.Module.Soort);
        Assert.False(shell.IsDatabaseOpen);
        Assert.False(shell.OpenModuleCommand.CanExecute(new ModuleVerzoek(ModuleSoort.Boekhouding, null)));
    }

    [Fact]
    public async Task Initialiseren_past_bewaard_thema_toe()
    {
        _instellingen.Instellingen = new GebruikersInstellingen { Thema = Thema.Donker };
        var shell = MaakShell();

        await shell.InitialiseerAsync();

        Assert.Equal([Thema.Donker], _thema.Toegepast);
        Assert.Equal(Thema.Donker, shell.Thema);
    }

    [Fact]
    public async Task Initialiseren_opent_laatst_gebruikte_database_na_wachtwoord()
    {
        _beheer.Voeg(Pad, Wachtwoord);
        _instellingen.Instellingen = new GebruikersInstellingen { LaatsteDatabase = Pad };
        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer(Wachtwoord, HerstelGevraagd: false));
        var shell = MaakShell();

        await shell.InitialiseerAsync();

        Assert.True(shell.IsDatabaseOpen);
        Assert.Equal("btw-boeker.btwdb", shell.DatabaseNaam);
    }

    /// <summary>Besluit 241: het welkomstscherm toont de laatst geopende database en opent hem met één klik.</summary>
    [Fact]
    public async Task Welkomstscherm_toont_de_laatst_geopende_database_en_opent_hem()
    {
        var pad = Path.Combine("Documenten", "BTW-Boeker", "scouting.btwdb");
        _beheer.Voeg(pad, Wachtwoord);
        _instellingen.Instellingen = new GebruikersInstellingen { LaatsteDatabase = pad };
        var shell = MaakShell();

        await shell.InitialiseerAsync(); // wachtwoordvraag geannuleerd

        Assert.False(shell.IsDatabaseOpen);
        Assert.True(shell.HeeftLaatsteDatabase);
        Assert.Equal("scouting.btwdb", shell.LaatsteDatabaseNaam);
        Assert.Equal("Documenten › BTW-Boeker", shell.LaatsteDatabaseMap);

        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer(Wachtwoord, HerstelGevraagd: false));
        await shell.OpenLaatsteDatabaseCommand.ExecuteAsync(null);

        Assert.True(shell.IsDatabaseOpen);
        Assert.False(shell.HeeftLaatsteDatabase);
    }

    [Fact]
    public async Task Zonder_bestaande_laatste_database_geen_regel_op_het_welkomstscherm()
    {
        _instellingen.Instellingen = new GebruikersInstellingen { LaatsteDatabase = Path.Combine("weg", "oud.btwdb") };
        var shell = MaakShell();

        await shell.InitialiseerAsync();

        Assert.False(shell.HeeftLaatsteDatabase);
    }

    [Fact]
    public async Task Verdwenen_laatste_database_wordt_niet_geopend()
    {
        _instellingen.Instellingen = new GebruikersInstellingen { LaatsteDatabase = Pad };
        var shell = MaakShell();

        await shell.InitialiseerAsync();

        Assert.False(shell.IsDatabaseOpen);
        Assert.Empty(_dialogen.MeldingenBijWachtwoordvraag);
    }

    // ---- Database openen / aanmaken --------------------------------------------------------

    [Fact]
    public async Task Onjuist_wachtwoord_vraagt_opnieuw_met_foutmelding()
    {
        _beheer.Voeg(Pad, Wachtwoord);
        _dialogen.BestaandDatabasebestand = Pad;
        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer("fout", false));
        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer(Wachtwoord, false));
        var shell = MaakShell();

        await shell.OpenDatabaseCommand.ExecuteAsync(null);

        Assert.True(shell.IsDatabaseOpen);
        Assert.Null(_dialogen.MeldingenBijWachtwoordvraag[0]);
        Assert.NotNull(_dialogen.MeldingenBijWachtwoordvraag[1]);
    }

    [Fact]
    public async Task Annuleren_van_wachtwoord_laat_database_dicht()
    {
        _beheer.Voeg(Pad, Wachtwoord);
        _dialogen.BestaandDatabasebestand = Pad;
        var shell = MaakShell();

        await shell.OpenDatabaseCommand.ExecuteAsync(null);

        Assert.False(shell.IsDatabaseOpen);
    }

    [Fact]
    public async Task Wachtwoord_vergeten_opent_met_herstelsleutel()
    {
        _beheer.Voeg(Pad, Wachtwoord);
        _dialogen.BestaandDatabasebestand = Pad;
        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer(null, HerstelGevraagd: true));
        _dialogen.Herstel = new HerstelInvoer(_beheer.Herstelsleutel, "nieuw lang wachtwoord");
        var shell = MaakShell();

        await shell.OpenDatabaseCommand.ExecuteAsync(null);

        Assert.True(shell.IsDatabaseOpen);
    }

    [Fact]
    public async Task Nieuwe_database_toont_herstelsleutel_en_opent_direct()
    {
        _dialogen.NieuwDatabasebestand = Pad;
        _dialogen.NieuwWachtwoord = Wachtwoord;
        var shell = MaakShell();

        await shell.NieuweDatabaseCommand.ExecuteAsync(null);

        Assert.Same(_beheer.Herstelsleutel, _dialogen.GetoondeHerstelsleutel);
        Assert.True(shell.IsDatabaseOpen);
        Assert.Equal(Pad, _instellingen.Instellingen.LaatsteDatabase);
    }

    [Fact]
    public async Task Fout_bij_openen_wordt_getoond()
    {
        _dialogen.BestaandDatabasebestand = @"C:\bestaat\niet.btwdb";
        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer(Wachtwoord, false));
        var shell = MaakShell();

        await shell.OpenDatabaseCommand.ExecuteAsync(null);

        Assert.False(shell.IsDatabaseOpen);
        Assert.Single(_dialogen.Fouten);
    }

    // ---- Tabbladen -------------------------------------------------------------------------

    [Fact]
    public async Task Zelfde_module_voor_beide_administraties_geeft_twee_herkenbare_tabbladen()
    {
        var shell = await MaakShellMetOpenDatabase();

        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Boekhouding, AdministratieSoort.Vereniging));
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Boekhouding, AdministratieSoort.Stichting));

        Assert.Equal(3, shell.Tabbladen.Count);
        Assert.Equal("Boekhouding · Scouting Vereniging", shell.Tabbladen[1].Titel);
        Assert.Equal("Boekhouding · Scouting Stichting", shell.Tabbladen[2].Titel);
        Assert.NotEqual(shell.Tabbladen[1].Administratie!.Accentkleur, shell.Tabbladen[2].Administratie!.Accentkleur);
    }

    [Fact]
    public async Task Financiele_module_zonder_administratie_wordt_geweigerd()
    {
        var shell = await MaakShellMetOpenDatabase();

        Assert.False(shell.OpenModuleCommand.CanExecute(new ModuleVerzoek(ModuleSoort.Boekhouding, null)));
    }

    [Fact]
    public async Task Sneltoets_gebruikt_administratie_van_actief_tabblad()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Boekhouding, AdministratieSoort.Stichting));

        shell.OpenModuleViaSneltoetsCommand.Execute("2"); // bij de Stichting: het meerjarenplan

        var boekhouding = Assert.IsType<ViewModels.Boekhouding.BoekhoudingViewModel>(shell.GeselecteerdTabblad);
        Assert.Equal(AdministratieSoort.Stichting, boekhouding.Administratie!.Soort);
        Assert.Equal(ViewModels.Boekhouding.BoekhoudingSubtab.Meerjarenplan, boekhouding.Subtab);
        Assert.Single(shell.Tabbladen, t => t.Module.Soort == ModuleSoort.Boekhouding);
    }

    [Fact]
    public async Task Module_begroting_opent_boekhouding_op_het_subtabblad_begroting()
    {
        var shell = await MaakShellMetOpenDatabase();

        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Begroting, AdministratieSoort.Vereniging));

        var boekhouding = Assert.IsType<ViewModels.Boekhouding.BoekhoudingViewModel>(shell.GeselecteerdTabblad);
        Assert.Equal(ViewModels.Boekhouding.BoekhoudingSubtab.Begroting, boekhouding.Subtab);
        Assert.DoesNotContain(shell.Tabbladen, t => t.Module.Soort == ModuleSoort.Begroting);
    }

    [Fact]
    public async Task Sneltoets_zonder_actieve_administratie_kiest_vereniging()
    {
        var shell = await MaakShellMetOpenDatabase();

        shell.OpenModuleViaSneltoetsCommand.Execute("1"); // Boekhouding

        Assert.Equal(AdministratieSoort.Vereniging, shell.GeselecteerdTabblad!.Administratie!.Soort);
    }

    [Fact]
    public async Task Onopgeslagen_wijzigingen_annuleren_houdt_de_database_open()
    {
        var (shell, instellingen) = await ShellMetGewijzigdeInstellingen();
        _dialogen.OpslaanAntwoord = OpslaanKeuze.Annuleren;

        await shell.SluitDatabaseCommand.ExecuteAsync(null);

        Assert.True(shell.IsDatabaseOpen);
        Assert.Same(instellingen, shell.GeselecteerdTabblad);
        Assert.Equal(["Instellingen"], _dialogen.OpslaanVragen);
    }

    [Fact]
    public async Task Onopgeslagen_wijzigingen_opslaan_bewaart_en_sluit()
    {
        var (shell, instellingen) = await ShellMetGewijzigdeInstellingen();
        _dialogen.OpslaanAntwoord = OpslaanKeuze.Opslaan;

        await shell.SluitDatabaseCommand.ExecuteAsync(null);

        Assert.DoesNotContain(instellingen, shell.Tabbladen);
        Assert.False(shell.IsDatabaseOpen);
        Assert.Equal(1, _beheer.Repository.AantalKeerBewaard);
    }

    [Fact]
    public async Task Onopgeslagen_wijzigingen_niet_opslaan_sluit_zonder_bewaren()
    {
        var (shell, instellingen) = await ShellMetGewijzigdeInstellingen();
        _dialogen.OpslaanAntwoord = OpslaanKeuze.NietOpslaan;

        await shell.SluitDatabaseCommand.ExecuteAsync(null);

        Assert.DoesNotContain(instellingen, shell.Tabbladen);
        Assert.Equal(0, _beheer.Repository.AantalKeerBewaard);
    }

    [Fact]
    public async Task Ctrl_S_slaat_actief_tabblad_op()
    {
        var (shell, _) = await ShellMetGewijzigdeInstellingen();

        Assert.True(shell.OpslaanCommand.CanExecute(null));
        await shell.OpslaanCommand.ExecuteAsync(null);

        Assert.Equal(1, _beheer.Repository.AantalKeerBewaard);
        Assert.False(shell.OpslaanCommand.CanExecute(null));
    }

    private async Task<(ShellViewModel Shell, InstellingenViewModel Instellingen)> ShellMetGewijzigdeInstellingen()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Instellingen, null));
        var instellingen = Assert.IsType<InstellingenViewModel>(shell.GeselecteerdTabblad);
        instellingen.Regels[0].Naam = "Gewijzigd";
        return (shell, instellingen);
    }

    // ---- Sessie-overgangen -----------------------------------------------------------------

    [Fact]
    public async Task Laatste_pagina_wordt_bewaard_bij_sluiten_en_hersteld_bij_openen()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Boekhouding, AdministratieSoort.Stichting));

        await shell.SluitDatabaseCommand.ExecuteAsync(null);

        Assert.Single(shell.Tabbladen);
        Assert.True(_beheer.LaatsteSessie!.Gesloten);

        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer(Wachtwoord, false));
        await shell.OpenDatabaseCommand.ExecuteAsync(null);

        Assert.Equal("Boekhouding · Scouting Stichting", shell.GeselecteerdTabblad!.Titel);
        Assert.Equal(AdministratieSoort.Stichting, shell.GekozenAdministratie!.Soort);
    }

    [Fact]
    public async Task Afsluiten_bewaart_de_laatste_pagina_en_sluit_database()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Boekhouding, AdministratieSoort.Vereniging));

        var mag = await shell.MagAfsluitenAsync();

        Assert.True(mag);
        Assert.True(_beheer.LaatsteSessie!.Gesloten);
        Assert.Equal(new LaatstePagina(ModuleSoort.Boekhouding, AdministratieSoort.Vereniging, (int)BoekhoudingSubtab.Journaal), _instellingen.Instellingen.LaatstePagina);
    }

    // ---- Thema, back-up en status ----------------------------------------------------------

    [Fact]
    public async Task Thema_kiezen_past_toe_en_bewaart()
    {
        var shell = MaakShell();
        await shell.InitialiseerAsync();

        shell.KiesThemaCommand.Execute(Thema.Licht);

        Assert.Equal(Thema.Licht, _thema.Toegepast[^1]);
        Assert.Equal(Thema.Licht, _instellingen.Instellingen.Thema);
    }

    [Fact]
    public async Task Backup_maken_schrijft_naar_gekozen_bestand()
    {
        var shell = await MaakShellMetOpenDatabase();
        _dialogen.BackupDoel = @"C:\Backups\btw-boeker.btwbackup";

        await shell.MaakBackupCommand.ExecuteAsync(null);

        Assert.Equal([@"C:\Backups\btw-boeker.btwbackup"], _beheer.LaatsteSessie!.Backups);
    }

    [Fact]
    public async Task Backup_terugzetten_gebruikt_gekozen_bron_en_doel()
    {
        var shell = MaakShell();
        await shell.InitialiseerAsync();
        _dialogen.BackupBron = @"C:\Backups\btw-boeker.btwbackup";
        _dialogen.NieuwDatabasebestand = @"C:\Data\hersteld.btwdb";
        _dialogen.BevestigAntwoord = false; // niet direct openen

        await shell.HerstelBackupCommand.ExecuteAsync(null);

        Assert.Equal([(@"C:\Backups\btw-boeker.btwbackup", @"C:\Data\hersteld.btwdb")], _beheer.Teruggezet);
    }

    [Fact]
    public async Task Statusbalk_toont_database_administratie_en_boekjaar()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Boekhouding, AdministratieSoort.Stichting));

        Assert.Equal("btw-boeker.btwdb", shell.DatabaseNaam);
        Assert.Equal("Scouting Stichting", shell.ActieveAdministratieNaam);
        Assert.Equal("Boekjaar 2026", shell.Boekjaartekst);
        Assert.Contains("Scouting Stichting", shell.Venstertitel, StringComparison.Ordinal);
    }
}
