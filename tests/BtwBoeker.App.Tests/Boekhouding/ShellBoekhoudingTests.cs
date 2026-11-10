using Microsoft.Extensions.Logging.Abstractions;
using BtwBoeker.App.ViewModels;
using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.App.Diensten;
using BtwBoeker.Application.Modules;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.App.Tests.Boekhouding;

public sealed class ShellBoekhoudingTests
{
    private const string Pad = @"C:\Data\scouting.btwdb";
    private const string Wachtwoord = "geheim-wachtwoord";
    private readonly FakeDatabaseBeheer _beheer = new();
    private readonly FakeDialoogService _dialogen = new();
    private readonly FakeAfdrukDienst _afdruk = new();

    private async Task<ShellViewModel> MaakShellMetOpenDatabase()
    {
        _beheer.Voeg(Pad, Wachtwoord);
        _dialogen.BestaandDatabasebestand = Pad;
        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer(Wachtwoord, HerstelGevraagd: false));
        var shell = new ShellViewModel(_beheer, new InMemoryInstellingenOpslag(), _dialogen, new FakeThemaService(),
            new BoekhoudingHulpmiddelen(new FakeSpreadsheetSchrijver(), _afdruk),
            new VasteTijd(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero)), NullLogger<ShellViewModel>.Instance);
        await shell.InitialiseerAsync();
        await shell.OpenDatabaseCommand.ExecuteAsync(null);
        return shell;
    }

    [Fact]
    public async Task Afdrukken_via_het_menu_werkt_alleen_op_het_tabblad_boekhouding()
    {
        var shell = await MaakShellMetOpenDatabase();
        Assert.False(shell.AfdrukkenCommand.CanExecute(null));

        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Boekhouding, AdministratieSoort.Vereniging));
        var tab = Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdTabblad);
        await tab.VernieuwCommand.ExecutionTask!;
        await tab.LeegBeginnenCommand.ExecuteAsync(null);

        Assert.True(shell.AfdrukkenCommand.CanExecute(null));
        await shell.AfdrukkenCommand.ExecuteAsync(null);
        Assert.Single(_afdruk.Afgedrukt);
    }

    [Fact]
    public async Task Bankafschrift_via_het_menu_opent_boekhouding_en_de_bankimport()
    {
        var shell = await MaakShellMetOpenDatabase();
        Assert.True(shell.ImporteerBankafschriftCommand.CanExecute(null));
        _dialogen.CamtBestanden = ["afschrift.xml"];
        var gestart = false;
        _dialogen.BankImport = _ =>
        {
            gestart = true;
            return Task.FromResult(false);
        };

        await shell.ImporteerBankafschriftCommand.ExecuteAsync(null);
        Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdTabblad);
        Assert.False(gestart); // nog niet ingericht: eerst uitleg, geen wizard

        await ((BoekhoudingViewModel)shell.GeselecteerdTabblad!).LeegBeginnenCommand.ExecuteAsync(null);
        await shell.ImporteerBankafschriftCommand.ExecuteAsync(null);
        Assert.True(gestart);
    }
}
