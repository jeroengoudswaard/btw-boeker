using Microsoft.Extensions.Logging.Abstractions;
using BtwBoeker.App.Diensten;
using BtwBoeker.App.ViewModels;
using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.App.Tests.Boekhouding;

/// <summary>Tabblad Boekhouding op een in-memory grootboek, ingericht met een verzonnen voorstel.</summary>
internal sealed class BoekhoudingTestomgeving : IDisposable
{
    public BoekhoudingTestomgeving()
    {
        Sessie = new FakeSessie(Path.Combine(Path.GetTempPath(), "boekhouding-test.btwdb"), new InMemoryAdministratieRepository());
        Hulpmiddelen = new BoekhoudingHulpmiddelen(Schrijver, Afdruk);
    }

    public FakeSessie Sessie { get; }
    public FakeDialoogService Dialogen { get; } = new();
    public FakeSpreadsheetSchrijver Schrijver { get; } = new();
    public FakeAfdrukDienst Afdruk { get; } = new();
    public BoekhoudingHulpmiddelen Hulpmiddelen { get; }
    public VasteTijd Tijd { get; } = new(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));
    public InMemoryBoekhoudingOpslag Opslag => (InMemoryBoekhoudingOpslag)Sessie.Boekhouding;

    public static AdministratieViewModel Vereniging { get; } = new(
        new Administratie(AdministratieSoort.Vereniging, "Vereniging", Administratie.Standaardkleur(AdministratieSoort.Vereniging)));

    public BoekhoudingViewModel Maak(AdministratieViewModel? administratie = null) =>
        new(Sessie, Dialogen, Tijd, administratie ?? Vereniging, Hulpmiddelen, NullLogger.Instance);

    /// <summary>Schrijft een verzonnen CAMT.053-bestand naar een tijdelijk bestand; wordt bij <see cref="Dispose"/> opgeruimd.</summary>
    public string CamtBestand(string iban, params Application.Tests.Bank.VbAfschrift[] afschriften)
    {
        var pad = Path.Combine(Path.GetTempPath(), $"camt-{Guid.NewGuid():N}.xml");
        File.WriteAllText(pad, Application.Tests.Bank.CamtVoorbeeld.Xml(iban, "08", afschriften));
        _bestanden.Add(pad);
        return pad;
    }

    public void Dispose()
    {
        foreach (var pad in _bestanden)
        {
            File.Delete(pad);
        }
    }

    private readonly List<string> _bestanden = [];

    /// <summary>Ingericht met daarnaast 1150 Spaarrekening en 1298 Kruisposten.</summary>
    public async Task<BoekhoudingViewModel> OpenMetBankrekeningenAsync()
    {
        var dienst = new BoekhoudingDienst(Sessie, Tijd);
        await dienst.ImporteerAsync(AdministratieSoort.Vereniging, Application.Tests.Boekhouding.Testvoorstel.Standaard());
        await dienst.BewaarRekeningAsync(AdministratieSoort.Vereniging, new RekeningInvoer(null, 1150, "Spaarrekening", null, Domain.Financien.Boekhouding.RekeningSoort.Balans, Domain.Financien.Boekhouding.RekeningRol.Geen, true));
        await dienst.BewaarRekeningAsync(AdministratieSoort.Vereniging, new RekeningInvoer(null, 1298, "Kruisposten", null, Domain.Financien.Boekhouding.RekeningSoort.Balans, Domain.Financien.Boekhouding.RekeningRol.Kruisposten, true));
        var vm = Maak();
        await vm.VernieuwCommand.ExecuteAsync(null);
        return vm;
    }

    public async Task<BoekhoudingViewModel> OpenIngerichtAsync()
    {
        var dienst = new BoekhoudingDienst(Sessie, Tijd);
        await dienst.ImporteerAsync(AdministratieSoort.Vereniging, Application.Tests.Boekhouding.Testvoorstel.Standaard());
        var vm = Maak();
        await vm.VernieuwCommand.ExecuteAsync(null);
        return vm;
    }
}
