using Microsoft.Extensions.Logging.Abstractions;
using Verenigd.App.Diensten;
using Verenigd.App.ViewModels;
using Verenigd.App.ViewModels.Contributie;
using Verenigd.Application.Modules;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.Tests.Contributie;

public sealed class ShellIncassoTests
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
            new VasteTijd(new DateTimeOffset(2027, 2, 20, 12, 0, 0, TimeSpan.Zero)), NullLogger<ShellViewModel>.Instance);
        await shell.InitialiseerAsync();
        await shell.OpenDatabaseCommand.ExecuteAsync(null);
        return shell;
    }

    [Fact]
    public async Task Ctrl_3_opent_contributie_van_de_vereniging()
    {
        var shell = await MaakShellMetOpenDatabase();

        shell.OpenModuleViaSneltoetsCommand.Execute("3");

        var tab = Assert.IsType<ContributieViewModel>(shell.GeselecteerdTabblad);
        Assert.Equal(AdministratieSoort.Vereniging, tab.Administratie?.Soort);
    }

    [Fact]
    public async Task Nieuwe_incassorun_opent_tabblad_en_wizard()
    {
        var shell = await MaakShellMetOpenDatabase();
        IncassoWizardViewModel? getoond = null;
        _dialogen.IncassoWizard = wizard =>
        {
            getoond = wizard;
            return Task.FromResult(false);
        };

        await shell.NieuweIncassorunCommand.ExecuteAsync(null);

        Assert.Equal(ModuleSoort.Contributie, shell.GeselecteerdTabblad?.Module.Soort);
        Assert.NotNull(getoond);
    }

    [Fact]
    public async Task Instellingen_bevatten_incassogegevens()
    {
        var shell = await MaakShellMetOpenDatabase();

        shell.OpenModuleCommand.Execute(ModuleVerzoek.Instellingen);

        var instellingen = Assert.IsType<InstellingenViewModel>(shell.GeselecteerdTabblad);
        Assert.NotNull(instellingen.Incasso);
        await instellingen.Incasso.LaadCommand.ExecutionTask!;
        Assert.False(instellingen.HeeftOnopgeslagenWijzigingen);
    }
}
