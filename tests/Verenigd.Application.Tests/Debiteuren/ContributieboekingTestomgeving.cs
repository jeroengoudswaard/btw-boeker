using Verenigd.Application.Administraties;
using Verenigd.Application.Bank;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Application.Debiteuren;
using Verenigd.Application.Import;
using Verenigd.Application.Incasso;
using Verenigd.Application.Leden;
using Verenigd.Application.Tests.Bank;
using Verenigd.Application.Tests.Boekhouding;
using Verenigd.Application.Tests.Incasso;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Tests.Debiteuren;

/// <summary>Contributie én boekhouding op verzonnen testdata, met een bankdagboek RABO en opbrengstrekeningen per speltak.</summary>
internal sealed class ContributieboekingTestomgeving
{
    public const Domain.Administraties.AdministratieSoort Ver = BoekhoudingTestdata.Ver;
    public static readonly Maand November = new(2026, 11);

    public ContributieboekingTestomgeving()
    {
        // Alleen november en later: geen naheffing van eerdere maanden in de tests.
        Incasso.Instellingen = new Incassoinstellingen(
            IncassantId.Maak("DE98ZZZ09999999999"), "Scouting Testgroep", Iban.Maak(IncassoTestdata.IbanA), 1, November);
        Boekhouding.Incassoruns = () => Incasso.Runs;
        Boekhouding.Herincassos = () => Incasso.Herincassos;
        Junioren = Boekhouding.Rekening(8030, "Contributie Junioren", RekeningSoort.Resultaat, null);
        Rabo = Boekhouding.Bankdagboek("RABO", CamtVoorbeeld.Betaal);
        Incasso.Welpen.KoppelOpbrengstrekening(Boekhouding.Welpen.Id);
        Incasso.Junioren.KoppelOpbrengstrekening(Junioren.Id);
        Anna = Incasso.Lid("100001", "Anna", new DateOnly(2026, 1, 1), categorie: Incasso.Welpen);
        Bob = Incasso.Lid("100002", "Bob", new DateOnly(2026, 1, 1), groep: "Junioren", categorie: Incasso.Junioren, iban: IncassoTestdata.IbanB);
        Sessie = new TestSessie(this);
        var tijd = new BoekhoudingDienstTests.VasteTijd(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero));
        Incassodienst = new IncassoDienst(Sessie, tijd);
        Dienst = new ContributieboekingDienst(Sessie, tijd);
        Bankdienst = new BankDienst(Sessie, tijd);
        Boekhoudingdienst = new BoekhoudingDienst(Sessie, tijd);
    }

    public IncassoTestdata Incasso { get; } = new();
    public BoekhoudingTestdata Boekhouding { get; } = new();
    public Grootboekrekening Junioren { get; }
    public Dagboek Rabo { get; }
    public Domain.Leden.Persoon Anna { get; }
    public Domain.Leden.Persoon Bob { get; }
    public TestSessie Sessie { get; }
    public IncassoDienst Incassodienst { get; }
    public ContributieboekingDienst Dienst { get; }
    public BankDienst Bankdienst { get; }
    public BoekhoudingDienst Boekhoudingdienst { get; }

    /// <summary>Maakt de run van <paramref name="maand"/> definitief (Anna € 10,00 Welpen, Bob € 15,00 Junioren in 2026).</summary>
    public async Task<Incassorun> RunAsync(Maand? maand = null)
    {
        var voorstel = await Incassodienst.BerekenAsync(maand ?? November);
        return await Incassodienst.MaakDefinitiefAsync(voorstel);
    }

    public Task<BankimportResultaat> BankAsync(params VbTransactie[] transacties)
    {
        var bestand = Camt053Lezer.Lees(CamtVoorbeeld.Stroom(CamtVoorbeeld.Xml(CamtVoorbeeld.Betaal, "08",
            new VbAfschrift(new DateOnly(2026, 11, 2), 0, transacties))));
        return Bankdienst.ImporteerAsync(Ver, BankimportPlanner.Plan(Boekhouding.Stand, [bestand]), new Dictionary<string, NieuweBankrekening>());
    }

    public Vorderingstand Vordering(Domain.Leden.Persoon lid) =>
        Contributievorderingen.Bereken(Boekhouding.Stand).Single(v => v.Post.Regels.Any(r => r.Lidnummer == lid.Lidnummer));

    internal sealed class TestSessie(ContributieboekingTestomgeving omgeving) : IDatabaseSessie
    {
        public string Pad { get; } = Path.Combine(Path.GetTempPath(), "test.vdb");
        public List<string> Backups { get; } = [];
        public IAdministratieRepository Administraties => throw new NotSupportedException();
        public ILedenRepository Leden => throw new NotSupportedException();
        public ILedenImportOpslag LedenImport => throw new NotSupportedException();
        public IContributieRepository Contributie { get; } = new IncassoDienstTests.FakeRepository(omgeving.Incasso);
        public IBoekhoudingRepository Boekhouding => omgeving.Boekhouding;
        public Verenigd.Application.Inrichting.IStartwizardOpslag Startwizard => throw new NotSupportedException();
        public Verenigd.Application.Email.IEmailOpslag Email => throw new NotSupportedException();
        public Verenigd.Application.Webapp.IWebappOpslag Webapp => throw new NotSupportedException();

        public Task MaakBackupAsync(string doelPad, CancellationToken annulering = default)
        {
            Backups.Add(doelPad);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
