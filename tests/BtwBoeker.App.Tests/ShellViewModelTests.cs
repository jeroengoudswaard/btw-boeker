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

        var tab = Assert.Single(shell.Paginas);
        Assert.Equal(ModuleSoort.Overzicht, tab.Module.Soort);
        Assert.False(shell.IsDatabaseOpen);
        Assert.False(shell.OpenModuleCommand.CanExecute(ModuleSoort.Boekhouding));
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

    /// <summary>Het welkomstscherm toont de laatst geopende database en opent hem met één klik.</summary>
    [Fact]
    public async Task Welkomstscherm_toont_de_laatst_geopende_database_en_opent_hem()
    {
        var pad = Path.Combine("Documenten", "BTW-Boeker", "vakantiehuis.btwdb");
        _beheer.Voeg(pad, Wachtwoord);
        _instellingen.Instellingen = new GebruikersInstellingen { LaatsteDatabase = pad };
        var shell = MaakShell();

        await shell.InitialiseerAsync(); // wachtwoordvraag geannuleerd

        Assert.False(shell.IsDatabaseOpen);
        Assert.True(shell.HeeftLaatsteDatabase);
        Assert.Equal("vakantiehuis.btwdb", shell.LaatsteDatabaseNaam);
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

    // ---- Paginas -------------------------------------------------------------------------

    [Fact]
    public async Task Sneltoets_opent_boekhouding_van_de_administratie()
    {
        var shell = await MaakShellMetOpenDatabase();

        shell.OpenModuleViaSneltoetsCommand.Execute("1"); // Boekhouding

        var boekhouding = Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdePagina);
        Assert.Same(shell.Administratie, boekhouding.Administratie);
    }

    /// <summary>Besluit 42: Instellingen bewaart direct; sluiten bewaart een getypte naam zonder te vragen.</summary>
    [Fact]
    public async Task Sluiten_bewaart_een_getypte_naam_zonder_te_vragen()
    {
        var (shell, instellingen) = await ShellMetGewijzigdeInstellingen();

        await shell.SluitDatabaseCommand.ExecuteAsync(null);

        Assert.Empty(_dialogen.OpslaanVragen);
        Assert.DoesNotContain(instellingen, shell.Paginas);
        Assert.False(shell.IsDatabaseOpen);
        Assert.Equal(1, _beheer.Repository.AantalKeerBewaard);
        Assert.Equal("Gewijzigd", _beheer.Repository.Opgeslagen.Naam);
    }

    [Fact]
    public async Task Sluiten_met_een_ongeldige_naam_sluit_zonder_bewaren()
    {
        var (shell, instellingen) = await ShellMetGewijzigdeInstellingen();
        instellingen.Naam = string.Empty;

        await shell.SluitDatabaseCommand.ExecuteAsync(null);

        Assert.Empty(_dialogen.OpslaanVragen);
        Assert.False(shell.IsDatabaseOpen);
        Assert.Equal(0, _beheer.Repository.AantalKeerBewaard);
    }

    [Fact]
    public async Task Een_andere_pagina_met_een_ongeldige_naam_meldt_dat_hij_niet_is_bewaard()
    {
        var (shell, instellingen) = await ShellMetGewijzigdeInstellingen();
        instellingen.Naam = string.Empty;

        shell.OpenModuleCommand.Execute(ModuleSoort.Boekhouding);

        Assert.Equal(Statussoort.Waarschuwing, shell.Meldingsbalk.Soort);
        Assert.Contains("niet bewaard", shell.Meldingsbalk.Tekst, StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, instellingen.Naam);
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
        shell.OpenModuleCommand.Execute(ModuleSoort.Instellingen);
        var instellingen = Assert.IsType<InstellingenViewModel>(shell.GeselecteerdePagina);
        instellingen.Naam = "Gewijzigd";
        return (shell, instellingen);
    }

    // ---- Sessie-overgangen -----------------------------------------------------------------

    [Fact]
    public async Task Laatste_pagina_wordt_bewaard_bij_sluiten_en_hersteld_bij_openen()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(ModuleSoort.Boekhouding);

        await shell.SluitDatabaseCommand.ExecuteAsync(null);

        Assert.Single(shell.Paginas);
        Assert.True(_beheer.LaatsteSessie!.Gesloten);

        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer(Wachtwoord, false));
        await shell.OpenDatabaseCommand.ExecuteAsync(null);

        Assert.Equal("Boekhouding", shell.GeselecteerdePagina!.Titel);
        Assert.Equal(AdministratieSoort.Standaard, shell.Administratie!.Soort);
    }

    [Fact]
    public async Task Afsluiten_bewaart_de_laatste_pagina_en_sluit_database()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(ModuleSoort.Boekhouding);

        var mag = await shell.MagAfsluitenAsync();

        Assert.True(mag);
        Assert.True(_beheer.LaatsteSessie!.Gesloten);
        Assert.Equal(new LaatstePagina(ModuleSoort.Boekhouding, (int)BoekhoudingSubtab.Journaal), _instellingen.Instellingen.LaatstePagina);
    }

    // ---- Thema, back-up en status ----------------------------------------------------------

    /// <summary>
    /// Besluit 42 (keuze 3A): de keuzerondjes op Instellingen zijn gebonden aan <c>GekozenThema</c> en <c>GekozenWeergave</c>,
    /// zodat ook kiezen met het toetsenbord of een schermlezer werkt, en het menu Beeld meeloopt.
    /// </summary>
    [Fact]
    public async Task Thema_en_weergave_kiezen_op_instellingen_past_toe_en_loopt_gelijk_met_het_menu()
    {
        var shell = MaakShell();
        await shell.InitialiseerAsync();

        shell.GekozenThema = Thema.Donker;
        shell.GekozenWeergave = Weergavemodus.Eenvoudig;

        Assert.Equal(Thema.Donker, shell.Thema);
        Assert.Equal(Thema.Donker, _thema.Toegepast[^1]);
        Assert.Equal(Thema.Donker, _instellingen.Instellingen.Thema);
        Assert.Equal(Weergavemodus.Eenvoudig, shell.Weergave);
        Assert.Equal(Weergavemodus.Eenvoudig, _instellingen.Instellingen.Weergave);

        shell.KiesThemaCommand.Execute(Thema.Licht); // via het menu Beeld
        Assert.Equal(Thema.Licht, shell.GekozenThema);
    }

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
    public async Task Titelbalk_toont_de_database_zonder_de_administratie()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(ModuleSoort.Boekhouding);

        Assert.Equal("btw-boeker.btwdb", shell.DatabaseNaam);
        Assert.Equal("Vakantiehuis De Duinen", shell.Administratie?.Naam);
        Assert.Equal("btw-boeker.btwdb – BTW-Boeker", shell.Venstertitel); // UX-review: één ding één keer
    }
}
