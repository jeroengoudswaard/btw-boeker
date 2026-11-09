using Microsoft.Extensions.Logging.Abstractions;
using Verenigd.App.Diensten;
using Verenigd.App.ViewModels;
using Verenigd.App.ViewModels.Inrichting;
using Verenigd.Application.Inrichting;
using Verenigd.Application.Instellingen;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.Tests.Inrichting;

/// <summary>De startwizard (besluiten 169–175): regisseur van de bestaande wizards en instellingen.</summary>
public sealed class StartwizardTests
{
    private const string Pad = @"C:\Data\verenigd.vdb";
    private const string Wachtwoord = "geheim wachtwoord";

    private readonly FakeDatabaseBeheer _beheer = new();
    private readonly FakeDialoogService _dialogen = new();
    private readonly InMemoryInstellingenOpslag _instellingen = new();

    private ShellViewModel MaakShell() =>
        new(_beheer, _instellingen, _dialogen, new FakeThemaService(), FakeBoekhouding.Hulpmiddelen(),
            new VasteTijd(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero)), NullLogger<ShellViewModel>.Instance);

    /// <summary>Een shell met een open database; de startwizard is al eens getoond.</summary>
    private async Task<ShellViewModel> MaakShellMetOpenDatabase()
    {
        _instellingen.Instellingen = new GebruikersInstellingen { StartwizardGetoond = true };
        _beheer.Voeg(Pad, Wachtwoord);
        _dialogen.BestaandDatabasebestand = Pad;
        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer(Wachtwoord, HerstelGevraagd: false));
        var shell = MaakShell();
        await shell.InitialiseerAsync();
        await shell.OpenDatabaseCommand.ExecuteAsync(null);
        Assert.True(shell.IsDatabaseOpen);
        return shell;
    }

    [Fact]
    public async Task Eerste_start_zonder_database_opent_de_startwizard_eenmalig()
    {
        Startstap? begin = null;
        _dialogen.Startwizard = w =>
        {
            begin = w.Huidig;
            return Task.CompletedTask;
        };

        await MaakShell().InitialiseerAsync();
        await MaakShell().InitialiseerAsync();

        Assert.Single(_dialogen.GetoondeStartwizards);
        Assert.Equal(Startstap.Welkom, begin);
        Assert.True(_instellingen.Instellingen.StartwizardGetoond);
    }

    [Fact]
    public async Task Database_aanmaken_namen_opslaan_en_later_verder()
    {
        _dialogen.NieuwDatabasebestand = Pad;
        _dialogen.NieuwWachtwoord = Wachtwoord;
        _dialogen.Startwizard = async w =>
        {
            await w.VolgendeCommand.ExecuteAsync(null);
            Assert.Equal(Startstap.Database, w.Huidig);
            Assert.False(w.VolgendeCommand.CanExecute(null)); // zonder database kan niets anders
            Assert.False(w.OverslaanCommand.CanExecute(null));

            await w.NieuweDatabaseCommand.ExecuteAsync(null);

            Assert.True(w.Status.DatabaseOpen);
            Assert.Equal(Startstap.Administraties, w.Huidig); // na het aanmaken direct door
            Assert.NotNull(_dialogen.GetoondeHerstelsleutel);
            var vereniging = w.Namen.Single(n => n.Soort == AdministratieSoort.Vereniging);
            vereniging.Naam = "Scouting Voorbeeld";
            await w.VolgendeCommand.ExecuteAsync(null);
            Assert.Equal(Startstap.Boekhouding, w.Huidig);
            Assert.Equal("●", w.Stappen.Single(s => s.Stap == Startstap.Boekhouding).Teken);

            await w.LaterVerderCommand.ExecuteAsync(null);
            Assert.True(w.IsGesloten);
        };
        var shell = MaakShell();

        await shell.InitialiseerAsync();

        Assert.True(shell.IsDatabaseOpen);
        Assert.Equal("Scouting Voorbeeld", shell.Administraties.Single(a => a.Soort == AdministratieSoort.Vereniging).Naam);
        Assert.Equal(new Startwizardvoortgang(Startstap.Boekhouding, false), _beheer.Startwizard.Voortgang);
        Assert.Empty(_dialogen.Bevestigingsvragen); // geen vraag om verder te gaan terwijl de wizard openstaat
    }

    [Fact]
    public async Task Openen_van_een_database_met_voortgang_biedt_aan_om_verder_te_gaan()
    {
        _beheer.Startwizard.Voortgang = new Startwizardvoortgang(Startstap.Bank, false);
        Startstap? begin = null;
        _dialogen.Startwizard = w =>
        {
            begin = w.Huidig;
            return Task.CompletedTask;
        };

        await MaakShellMetOpenDatabase();

        Assert.Contains(_dialogen.Bevestigingsvragen, v => v.Contains("Bank", StringComparison.Ordinal));
        Assert.Equal(Startstap.Bank, begin);
    }

    [Fact]
    public async Task Nee_op_verder_gaan_opent_de_wizard_niet()
    {
        _beheer.Startwizard.Voortgang = new Startwizardvoortgang(Startstap.Bank, false);
        _dialogen.BevestigAntwoord = false;

        await MaakShellMetOpenDatabase();

        Assert.Empty(_dialogen.GetoondeStartwizards);
    }

    [Fact]
    public async Task Via_het_menu_toont_de_wizard_wat_al_is_ingericht_en_voltooien_rondt_af()
    {
        var shell = await MaakShellMetOpenDatabase();
        _dialogen.Startwizard = async w =>
        {
            Assert.Equal(Startstap.Welkom, w.Huidig);
            await w.VolgendeCommand.ExecuteAsync(null);
            Assert.True(w.IsHuidigeStapKlaar); // "Al ingericht" (besluit 175)
            Assert.True(w.Stappen.Single(s => s.Stap == Startstap.Administraties).IsKlaar); // fake heeft eigen namen

            w.GaNaarCommand.Execute(w.Stappen.Single(s => s.Stap == Startstap.Klaar));

            Assert.Contains(w.Klaarregels, r => r.Tekst.Contains("boekhouding", StringComparison.OrdinalIgnoreCase) && !r.IsKlaar);
            Assert.False(w.VolgendeCommand.CanExecute(null));
            await w.VoltooienCommand.ExecuteAsync(null);
            Assert.True(w.IsGesloten);
        };

        await shell.StartwizardCommand.ExecuteAsync(null);

        Assert.Equal(new Startwizardvoortgang(null, true), _beheer.Startwizard.Voortgang);
    }

    [Fact]
    public async Task Boekhouding_inrichten_via_de_bestaande_route()
    {
        var shell = await MaakShellMetOpenDatabase();
        _dialogen.Startwizard = async w =>
        {
            w.GaNaarCommand.Execute(w.Stappen.Single(s => s.Stap == Startstap.Boekhouding));
            Assert.False(w.IsHuidigeStapKlaar);

            await w.BoekhoudingInrichtenCommand.ExecuteAsync(Boekhoudingkeuze.Leeg);

            Assert.True(w.IsHuidigeStapKlaar);
            Assert.True(w.Status.BoekhoudingVereniging);
        };

        await shell.StartwizardCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task Bankstap_kan_ook_voor_de_stichting()
    {
        var shell = await MaakShellMetOpenDatabase();
        _dialogen.Startwizard = async w =>
        {
            w.GaNaarCommand.Execute(w.Stappen.Single(s => s.Stap == Startstap.Boekhouding));
            w.BoekhoudingVoor = AdministratieSoort.Stichting;
            await w.BoekhoudingInrichtenCommand.ExecuteAsync(Boekhoudingkeuze.Leeg);
            Assert.True(w.Status.BoekhoudingStichting);

            w.GaNaarCommand.Execute(w.Stappen.Single(s => s.Stap == Startstap.Bank));
            Assert.False(w.KanBankInlezen); // de Vereniging heeft nog geen boekhouding
            w.BankVoor = AdministratieSoort.Stichting;
            Assert.True(w.KanBankInlezen);
            await w.BankInlezenCommand.ExecuteAsync(null);

            Assert.Contains(w.Klaarregels, r => r.IsKlaar && r.Tekst.Contains("Stichting", StringComparison.Ordinal));
        };

        await shell.StartwizardCommand.ExecuteAsync(null);

        Assert.Equal(AdministratieSoort.Stichting, Assert.IsType<ViewModels.Boekhouding.BoekhoudingViewModel>(shell.GeselecteerdTabblad).Administratie?.Soort);
    }

}
