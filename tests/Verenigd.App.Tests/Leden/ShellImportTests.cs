using Microsoft.Extensions.Logging.Abstractions;
using Verenigd.App.Diensten;
using Verenigd.App.ViewModels;
using Verenigd.App.ViewModels.Leden;
using Verenigd.Application.Modules;
using static Verenigd.App.Tests.Leden.LedenTestdata;

namespace Verenigd.App.Tests.Leden;

public sealed class ShellImportTests
{
    private const string Pad = @"C:\Data\verenigd.vdb";
    private const string Wachtwoord = "geheim wachtwoord";

    private readonly FakeDatabaseBeheer _beheer = new();
    private readonly FakeDialoogService _dialogen = new();

    private async Task<ShellViewModel> MaakShellMetOpenDatabase()
    {
        _beheer.Voeg(Pad, Wachtwoord);
        _dialogen.BestaandDatabasebestand = Pad;
        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer(Wachtwoord, HerstelGevraagd: false));
        var shell = new ShellViewModel(_beheer, new InMemoryInstellingenOpslag(), _dialogen, new FakeThemaService(), FakeBoekhouding.Hulpmiddelen(),
            new VasteTijd(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero)), NullLogger<ShellViewModel>.Instance);
        await shell.InitialiseerAsync();
        await shell.OpenDatabaseCommand.ExecuteAsync(null);
        return shell;
    }

    [Fact]
    public async Task Importeren_kan_alleen_met_open_database()
    {
        var shell = new ShellViewModel(_beheer, new InMemoryInstellingenOpslag(), _dialogen, new FakeThemaService(), FakeBoekhouding.Hulpmiddelen(),
            TimeProvider.System, NullLogger<ShellViewModel>.Instance);

        Assert.False(shell.ImporteerSol3Command.CanExecute(null));
        Assert.True((await MaakShellMetOpenDatabase()).ImporteerSol3Command.CanExecute(null));
    }

    [Fact]
    public async Task Geannuleerde_import_opent_geen_tabblad()
    {
        var shell = await MaakShellMetOpenDatabase();

        await shell.ImporteerSol3Command.ExecuteAsync(null);

        Assert.DoesNotContain(shell.Tabbladen, t => t.Module.Soort == ModuleSoort.Leden);
    }

    [Fact]
    public async Task Geslaagde_import_opent_en_vernieuwt_tabblad_leden()
    {
        var shell = await MaakShellMetOpenDatabase();
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Leden, null));
        var leden = Assert.IsType<LedenViewModel>(shell.GeselecteerdTabblad);
        await leden.VernieuwCommand.ExecutionTask!;
        Assert.Empty(leden.Leden);

        _dialogen.ImportWizard = async _ =>
        {
            await VulAsync(_beheer.Leden, Lid("100000001", "Anna", "Vries"));
            return true;
        };
        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Overzicht, null));
        _dialogen.BevestigAntwoord = false; // geen welkomstmail
        await shell.ImporteerSol3Command.ExecuteAsync(null);

        Assert.Same(leden, shell.GeselecteerdTabblad);
        Assert.Single(leden.Leden);
        Assert.Contains("1 nieuw lid", Assert.Single(_dialogen.Bevestigingsvragen), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Import_met_nieuwe_leden_biedt_een_welkomstmail_aan_voor_alleen_die_leden()
    {
        var shell = await MaakShellMetOpenDatabase();
        await VulAsync(_beheer.Leden, Lid("100000001", "Anna", "Vries"));
        _dialogen.ImportWizard = async _ =>
        {
            await VulAsync(_beheer.Leden, Lid("100000001", "Anna", "Vries"), Lid("100000002", "Kees", "Jansen", lidtype: Verenigd.Domain.Leden.Lidtype.Kaderlid));
            return true;
        };

        await shell.ImporteerSol3Command.ExecuteAsync(null);

        var email = Assert.IsType<ViewModels.Email.EmailViewModel>(shell.GeselecteerdTabblad);
        Assert.True(email.IsVoorbereid);
        Assert.Equal("kees@example.org", Assert.Single(email.Ontvangers).Adres);
        Assert.Equal(Verenigd.Domain.Communicatie.Emailsoort.Welkom, email.GekozenSjabloon?.Soort);
    }

    [Fact]
    public async Task Import_zonder_nieuwe_leden_vraagt_niets()
    {
        var shell = await MaakShellMetOpenDatabase();
        await VulAsync(_beheer.Leden, Lid("100000001", "Anna", "Vries"));
        _dialogen.ImportWizard = async _ =>
        {
            await VulAsync(_beheer.Leden, Lid("100000001", "Anna", "Vries"));
            return true;
        };

        await shell.ImporteerSol3Command.ExecuteAsync(null);

        Assert.Empty(_dialogen.Bevestigingsvragen);
        Assert.IsType<LedenViewModel>(shell.GeselecteerdTabblad);
    }
}
