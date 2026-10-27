using Microsoft.Extensions.Logging.Abstractions;
using Verenigd.App.Diensten;
using Verenigd.App.ViewModels;
using Verenigd.App.ViewModels.Boekhouding;
using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.Tests.Boekhouding;

/// <summary>Tabblad Boekhouding op een in-memory grootboek, met een verzonnen Voorblad als Excel-bron.</summary>
internal sealed class BoekhoudingTestomgeving
{
    public BoekhoudingTestomgeving()
    {
        Sessie = new FakeSessie(Path.Combine(Path.GetTempPath(), "boekhouding-test.vdb"), new InMemoryAdministratieRepository(), new InMemoryLedenOpslag());
        Hulpmiddelen = new BoekhoudingHulpmiddelen(Lezer, Schrijver, Afdruk);
    }

    public FakeSessie Sessie { get; }
    public FakeDialoogService Dialogen { get; } = new();
    public FakeWerkbladLezer Lezer { get; } = new() { Blad = Voorblad() };
    public FakeSpreadsheetSchrijver Schrijver { get; } = new();
    public FakeAfdrukDienst Afdruk { get; } = new();
    public BoekhoudingHulpmiddelen Hulpmiddelen { get; }
    public VasteTijd Tijd { get; } = new(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));
    public InMemoryBoekhoudingOpslag Opslag => (InMemoryBoekhoudingOpslag)Sessie.Boekhouding;

    public static AdministratieViewModel Vereniging { get; } = new(
        new Administratie(AdministratieSoort.Vereniging, "Vereniging", Administratie.Standaardkleur(AdministratieSoort.Vereniging)));

    public static Werkblad Voorblad(double bank = 799.5)
    {
        var blad = new Werkblad();
        blad.Zet(1, 1, 2025.0);
        blad.Zet(3, 2, "Vermogen");
        blad.Zet(4, 1, 600.0); blad.Zet(4, 2, "Algemene Reserve"); blad.Zet(4, 10, 1000.0);
        blad.Zet(7, 2, "Kas Bank");
        blad.Zet(8, 1, 1000.0); blad.Zet(8, 2, "Kas"); blad.Zet(8, 9, 200.5);
        blad.Zet(9, 1, 1100.0); blad.Zet(9, 2, "Bank"); blad.Zet(9, 9, bank);
        blad.Zet(13, 2, "Terreinkosten");
        blad.Zet(14, 1, 4115.0); blad.Zet(14, 2, "Energie"); blad.Zet(14, 7, 300.0);
        blad.Zet(17, 2, "Contributie");
        blad.Zet(18, 1, 8020.0); blad.Zet(18, 2, "Contributie Welpen"); blad.Zet(18, 8, 500.0);
        blad.Zet(22, 3, 0.0, "=SUM(C3:C21)");
        return blad;
    }

    public BoekhoudingViewModel Maak(AdministratieViewModel? administratie = null) =>
        new(Sessie, Dialogen, Tijd, administratie ?? Vereniging, Hulpmiddelen, NullLogger.Instance);

    public async Task<BoekhoudingViewModel> OpenIngerichtAsync()
    {
        var dienst = new BoekhoudingDienst(Sessie, Tijd);
        await dienst.ImporteerAsync(AdministratieSoort.Vereniging, VoorbladInlezer.Lees(Voorblad()));
        var vm = Maak();
        await vm.VernieuwCommand.ExecuteAsync(null);
        return vm;
    }
}
