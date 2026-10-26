using Microsoft.Extensions.Logging.Abstractions;
using Verenigd.App.Diensten;
using Verenigd.App.ViewModels;
using Verenigd.Application.Instellingen;
using Verenigd.Application.Modules;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.Tests;

public sealed class ShellViewModelTests
{
    private const string Pad = @"C:\Data\verenigd.vdb";
    private const string Wachtwoord = "geheim wachtwoord";

    private readonly FakeDatabaseBeheer _beheer = new();
    private readonly FakeDialoogService _dialogen = new();
    private readonly InMemoryInstellingenOpslag _instellingen = new();
    private readonly FakeThemaService _thema = new();

    private ShellViewModel MaakShell() =>
        new(_beheer, _instellingen, _dialogen, _thema, new VasteTijd(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero)),
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
        Assert.False(shell.OpenModuleCommand.CanExecute(new ModuleVerzoek(ModuleSoort.Leden, null)));
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
        Assert.Equal("verenigd.vdb", shell.DatabaseNaam);
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
        _dialogen.BestaandDatabasebestand = @"C:\bestaat\niet.vdb";
        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer(Wachtwoord, false));
        var shell = MaakShell();

        await shell.OpenDatabaseCommand.ExecuteAsync(null);

        Assert.False(shell.IsDatabaseOpen);
        Assert.Single(_dialogen.Fouten);
    }

    // ---- Tabbladen -------------------------------------------------------------------------

    [Fact]
    public async Task Module_openen_voegt_tabblad_toe_en_selecteert_het()
    {
        var shell = await MaakShellMetOpenDatabase();

        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Leden, null));

        Assert.Equal(2, shell.Tabbladen.Count);
        Assert.Equal(ModuleSoort.Leden, shell.GeselecteerdTabblad!.Module.Soort);
    }

    [Fact]
    public async Task Al_geopende_module_springt_naar_bestaand_tabblad()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Leden, null));
        var leden = shell.GeselecteerdTabblad;
        shell.GeselecteerdTabblad = shell.Tabbladen[0];

        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Leden, null));

        Assert.Equal(2, shell.Tabbladen.Count);
        Assert.Same(leden, shell.GeselecteerdTabblad);
    }

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

        shell.OpenModuleViaSneltoetsCommand.Execute("5"); // Begroting

        Assert.Equal(ModuleSoort.Begroting, shell.GeselecteerdTabblad!.Module.Soort);
        Assert.Equal(AdministratieSoort.Stichting, shell.GeselecteerdTabblad.Administratie!.Soort);
    }

    [Fact]
    public async Task Sneltoets_zonder_actieve_administratie_kiest_vereniging()
    {
        var shell = await MaakShellMetOpenDatabase();

        shell.OpenModuleViaSneltoetsCommand.Execute("4"); // Boekhouding

        Assert.Equal(AdministratieSoort.Vereniging, shell.GeselecteerdTabblad!.Administratie!.Soort);
    }

    [Fact]
    public async Task Contributie_kan_niet_voor_de_stichting_geopend_worden()
    {
        var shell = await MaakShellMetOpenDatabase();

        Assert.False(shell.OpenModuleCommand.CanExecute(new ModuleVerzoek(ModuleSoort.Contributie, AdministratieSoort.Stichting)));
        Assert.True(shell.OpenModuleCommand.CanExecute(new ModuleVerzoek(ModuleSoort.Contributie, AdministratieSoort.Vereniging)));
    }

    [Fact]
    public async Task Sneltoets_contributie_vanuit_stichting_opent_contributie_van_vereniging()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Boekhouding, AdministratieSoort.Stichting));

        shell.OpenModuleViaSneltoetsCommand.Execute("3");

        Assert.Equal(ModuleSoort.Contributie, shell.GeselecteerdTabblad!.Module.Soort);
        Assert.Equal(AdministratieSoort.Vereniging, shell.GeselecteerdTabblad.Administratie!.Soort);
    }

    [Fact]
    public async Task Modulemenu_toont_contributie_zonder_keuze_en_boekhouding_met_keuze()
    {
        var shell = await MaakShellMetOpenDatabase();

        var contributie = shell.ModuleMenu.Single(m => m.Titel == "Contributie");
        var boekhouding = shell.ModuleMenu.Single(m => m.Titel == "Boekhouding");

        Assert.Empty(contributie.Kinderen);
        Assert.Equal(new ModuleVerzoek(ModuleSoort.Contributie, AdministratieSoort.Vereniging), contributie.Parameter);
        Assert.Equal(2, boekhouding.Kinderen.Count);
    }

    [Fact]
    public async Task Bewaard_tabblad_contributie_stichting_wordt_niet_hersteld()
    {
        _beheer.Voeg(Pad, Wachtwoord);
        _instellingen.Instellingen = new GebruikersInstellingen
        {
            LaatsteDatabase = Pad,
            OpenTabbladen = [new OpenTabblad(ModuleSoort.Contributie, AdministratieSoort.Stichting)],
        };
        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer(Wachtwoord, false));
        var shell = MaakShell();

        await shell.InitialiseerAsync();

        Assert.Single(shell.Tabbladen);
    }

    [Fact]
    public async Task Tabblad_sluiten_selecteert_buurman()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Leden, null));
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Email, null));
        var leden = shell.Tabbladen[1];
        shell.GeselecteerdTabblad = leden;

        await shell.SluitTabbladCommand.ExecuteAsync(leden);

        Assert.Equal(2, shell.Tabbladen.Count);
        Assert.Equal(ModuleSoort.Email, shell.GeselecteerdTabblad!.Module.Soort);
    }

    [Fact]
    public async Task Overzicht_kan_niet_gesloten_worden()
    {
        var shell = await MaakShellMetOpenDatabase();

        Assert.False(shell.SluitTabbladCommand.CanExecute(shell.Tabbladen[0]));
    }

    [Fact]
    public async Task Volgend_en_vorig_tabblad_lopen_rond()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Leden, null));

        shell.VolgendTabbladCommand.Execute(null);
        Assert.Equal(ModuleSoort.Overzicht, shell.GeselecteerdTabblad!.Module.Soort);

        shell.VorigTabbladCommand.Execute(null);
        Assert.Equal(ModuleSoort.Leden, shell.GeselecteerdTabblad!.Module.Soort);
    }

    [Fact]
    public async Task Onopgeslagen_wijzigingen_annuleren_houdt_tabblad_open()
    {
        var (shell, instellingen) = await ShellMetGewijzigdeInstellingen();
        _dialogen.OpslaanAntwoord = OpslaanKeuze.Annuleren;

        await shell.SluitTabbladCommand.ExecuteAsync(instellingen);

        Assert.Contains(instellingen, shell.Tabbladen);
        Assert.Equal(["Instellingen"], _dialogen.OpslaanVragen);
    }

    [Fact]
    public async Task Onopgeslagen_wijzigingen_opslaan_bewaart_en_sluit()
    {
        var (shell, instellingen) = await ShellMetGewijzigdeInstellingen();
        _dialogen.OpslaanAntwoord = OpslaanKeuze.Opslaan;

        await shell.SluitTabbladCommand.ExecuteAsync(instellingen);

        Assert.DoesNotContain(instellingen, shell.Tabbladen);
        Assert.Equal(1, _beheer.Repository.AantalKeerBewaard);
    }

    [Fact]
    public async Task Onopgeslagen_wijzigingen_niet_opslaan_sluit_zonder_bewaren()
    {
        var (shell, instellingen) = await ShellMetGewijzigdeInstellingen();
        _dialogen.OpslaanAntwoord = OpslaanKeuze.NietOpslaan;

        await shell.SluitTabbladCommand.ExecuteAsync(instellingen);

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
    public async Task Open_tabbladen_worden_bewaard_bij_sluiten_en_hersteld_bij_openen()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Leden, null));
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Boekhouding, AdministratieSoort.Stichting));

        await shell.SluitDatabaseCommand.ExecuteAsync(null);

        Assert.Single(shell.Tabbladen);
        Assert.True(_beheer.LaatsteSessie!.Gesloten);

        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer(Wachtwoord, false));
        await shell.OpenDatabaseCommand.ExecuteAsync(null);

        Assert.Equal(
            ["Overzicht", "Leden", "Boekhouding · Scouting Stichting"],
            shell.Tabbladen.Select(t => t.Titel));
    }

    [Fact]
    public async Task Afsluiten_bewaart_tabbladen_en_sluit_database()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Leden, null));

        var mag = await shell.MagAfsluitenAsync();

        Assert.True(mag);
        Assert.True(_beheer.LaatsteSessie!.Gesloten);
        Assert.Contains(new OpenTabblad(ModuleSoort.Leden, null), _instellingen.Instellingen.OpenTabbladen);
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
        _dialogen.BackupDoel = @"C:\Backups\verenigd.vbak";

        await shell.MaakBackupCommand.ExecuteAsync(null);

        Assert.Equal([@"C:\Backups\verenigd.vbak"], _beheer.LaatsteSessie!.Backups);
    }

    [Fact]
    public async Task Backup_terugzetten_gebruikt_gekozen_bron_en_doel()
    {
        var shell = MaakShell();
        await shell.InitialiseerAsync();
        _dialogen.BackupBron = @"C:\Backups\verenigd.vbak";
        _dialogen.NieuwDatabasebestand = @"C:\Data\hersteld.vdb";
        _dialogen.BevestigAntwoord = false; // niet direct openen

        await shell.HerstelBackupCommand.ExecuteAsync(null);

        Assert.Equal([(@"C:\Backups\verenigd.vbak", @"C:\Data\hersteld.vdb")], _beheer.Teruggezet);
    }

    [Fact]
    public async Task Statusbalk_toont_database_administratie_en_boekjaar()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Boekhouding, AdministratieSoort.Stichting));

        Assert.Equal("verenigd.vdb", shell.DatabaseNaam);
        Assert.Equal("Scouting Stichting", shell.ActieveAdministratieNaam);
        Assert.Equal("Boekjaar 2026", shell.Boekjaartekst);
        Assert.Contains("Scouting Stichting", shell.Venstertitel, StringComparison.Ordinal);
    }
}
