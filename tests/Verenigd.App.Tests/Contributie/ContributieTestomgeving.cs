using Microsoft.Extensions.Logging.Abstractions;
using Verenigd.App.Tests.Leden;
using Verenigd.App.ViewModels;
using Verenigd.App.ViewModels.Contributie;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Leden;
using static Verenigd.App.Tests.Leden.LedenTestdata;

namespace Verenigd.App.Tests.Contributie;

/// <summary>Een tabblad Contributie op in-memory gegevens met drie verzonnen leden.</summary>
internal sealed class ContributieTestomgeving
{
    public const string IbanA = "NL91ABNA0417164300";

    public ContributieTestomgeving()
    {
        Sessie = new FakeSessie(Path.Combine(Path.GetTempPath(), "contributie-test.vdb"), new InMemoryAdministratieRepository(), Leden, Opslag);
        Opslag.Leden = Leden;
    }

    public InMemoryLedenOpslag Leden { get; } = new();
    public InMemoryContributieOpslag Opslag { get; } = new();
    public FakeSessie Sessie { get; }
    public FakeDialoogService Dialogen { get; } = new();
    public VasteTijd Tijd { get; } = new(new DateTimeOffset(2027, 2, 20, 10, 0, 0, TimeSpan.Zero));

    public static AdministratieViewModel Vereniging { get; } = new(
        new Administratie(AdministratieSoort.Vereniging, "Vereniging", Administratie.Standaardkleur(AdministratieSoort.Vereniging)));

    public async Task VulLedenAsync()
    {
        await VulAsync(Leden,
            Lid("100000001", "Anna", "Vries"),
            Lid("100000002", "Bo", "Jansen", groep: "Junioren", lidtype: Lidtype.Kaderlid, rol: "Leider"),
            Lid("100000003", "Cas", "Bakker", groep: "Junioren"));
        foreach (var groep in Leden.Stand.Groepen.Where(g => g.Naam.Contains("Migratie", StringComparison.Ordinal)))
        {
            groep.WijzigSoort(GroepSoort.Technisch);
        }
    }

    public async Task RichtIncassoInAsync()
    {
        await Opslag.BewaarInstellingenAsync(new(
            "DE98ZZZ09999999999", "Scouting Testgroep", IbanA, 1, new Maand(2027, 1), Incassoinstellingen.StandaardSjabloon));
    }

    public async Task<ContributieViewModel> OpenAsync()
    {
        var vm = new ContributieViewModel(Sessie, Dialogen, Tijd, Vereniging, NullLogger.Instance);
        await vm.VernieuwCommand.ExecuteAsync(null);
        return vm;
    }
}
