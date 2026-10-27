using Verenigd.Application.Bank;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Facturen;
using Verenigd.Application.Tests.Bank;
using Verenigd.Application.Tests.Boekhouding;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Domain.Financien.Facturen;
using Verenigd.Domain.Financien.Relaties;

namespace Verenigd.Application.Tests.Facturen;

public sealed class FactuurDienstTests : IAsyncDisposable
{
    private const Domain.Administraties.AdministratieSoort Ver = BoekhoudingTestdata.Ver;
    private readonly BoekhoudingTestdata _data = new();
    private readonly BoekhoudingDienstTests.FakeSessie _sessie;
    private readonly FactuurDienst _dienst;
    private readonly BankDienst _bank;
    private readonly BoekhoudingDienst _boekhouding;
    private readonly Grootboekrekening _verhuur;

    public FactuurDienstTests()
    {
        _sessie = new BoekhoudingDienstTests.FakeSessie(_data);
        var tijd = new BoekhoudingDienstTests.VasteTijd(new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero));
        _dienst = new FactuurDienst(_sessie, tijd);
        _bank = new BankDienst(_sessie, tijd);
        _boekhouding = new BoekhoudingDienst(_sessie, tijd);
        _verhuur = _data.Rekening(8400, "Verhuur", RekeningSoort.Resultaat, null);
        _data.Bankdagboek("RABO", CamtVoorbeeld.Betaal);
    }

    public ValueTask DisposeAsync() => _sessie.DisposeAsync();

    private static RelatieGegevens Kerk(string? iban = CamtVoorbeeld.Tegen) =>
        new("Stichting De Kerk", null, "Kerkstraat 1", "1234 AB", "Dorp", null, null, iban, true, false, null);

    private async Task<Guid> ConceptAsync(Guid relatie, long prijs = 25000, decimal aantal = 1) =>
        await _dienst.BewaarConceptAsync(Ver, new FactuurInvoer(null, relatie, new DateOnly(2026, 11, 1), 14,
            [new FactuurregelInvoerId("Zaalhuur", aantal, new Bedrag(prijs), _verhuur.Id)]));

    private Verkoopfactuur Factuur(Guid id) => _data.Verkoopfacturen.Single(f => f.Id == id);

    private Task<BankimportResultaat> BankAsync(params VbTransactie[] t) =>
        _bank.ImporteerAsync(Ver, BankimportPlanner.Plan(_data.Stand, [Camt053Lezer.Lees(CamtVoorbeeld.Stroom(
            CamtVoorbeeld.Xml(CamtVoorbeeld.Betaal, "08", new VbAfschrift(new DateOnly(2026, 11, 10), 0, t))))]), new Dictionary<string, NieuweBankrekening>());

    [Fact]
    public async Task Relatie_bewaren_en_wijzigen()
    {
        var id = await _dienst.BewaarRelatieAsync(new RelatieInvoer(null, Kerk(), true));
        await _dienst.BewaarRelatieAsync(new RelatieInvoer(id, Kerk() with { Plaats = "Stad" }, false));

        var relatie = _data.Relaties.Single();
        Assert.Equal("Stad", relatie.Plaats);
        Assert.False(relatie.IsActief);
    }

    [Fact]
    public async Task Definitief_maken_boekt_op_debiteuren_overig_en_nummert_per_jaar()
    {
        var relatie = await _dienst.BewaarRelatieAsync(new RelatieInvoer(null, Kerk(), true));
        var eerste = await ConceptAsync(relatie);
        var tweede = await ConceptAsync(relatie, 5000);

        Assert.Equal("V2026-001", await _dienst.MaakDefinitiefAsync(Ver, eerste));
        Assert.Equal("V2026-002", await _dienst.MaakDefinitiefAsync(Ver, tweede));

        var factuur = Factuur(eerste);
        var post = _data.Posten.Single(p => p.Id == factuur.PostId);
        Assert.True(post.IsGeboekt);
        Assert.Equal("VRK", post.Dagboek.Code);
        Assert.Equal(new DateOnly(2026, 11, 1), post.Datum);
        Assert.Equal(25000, post.Regels.Single(r => r.Rekening.Nummer == 1310).Saldo);
        Assert.Equal(-25000, post.Regels.Single(r => r.Rekening.Nummer == 8400).Saldo);
        var stand = Factuuroverzicht.Bereken(_data.Stand, new DateOnly(2026, 11, 20)).Single(f => f.Factuur.Id == eerste);
        Assert.Equal(25000, stand.Saldo);
        Assert.True(stand.IsVervallen); // vervaldatum 15-11, vandaag 20-11
    }

    [Fact]
    public async Task Ongeldig_concept_wordt_niet_definitief_en_concept_verwijderen()
    {
        var relatie = await _dienst.BewaarRelatieAsync(new RelatieInvoer(null, Kerk(), true));
        var leeg = await _dienst.BewaarConceptAsync(Ver, new FactuurInvoer(null, relatie, new DateOnly(2026, 11, 1), 14, []));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _dienst.MaakDefinitiefAsync(Ver, leeg));

        await _dienst.VerwijderConceptAsync(Ver, leeg);
        Assert.Empty(_data.Verkoopfacturen);
    }

    [Fact]
    public async Task Betaling_met_factuurnummer_of_iban_en_bedrag_wordt_herkend()
    {
        var relatie = await _dienst.BewaarRelatieAsync(new RelatieInvoer(null, Kerk(), true));
        var a = await ConceptAsync(relatie);
        await _dienst.MaakDefinitiefAsync(Ver, a);
        var b = await ConceptAsync(relatie, 5000);
        await _dienst.MaakDefinitiefAsync(Ver, b);

        var resultaat = await BankAsync(
            new VbTransactie(25000, Ref: "F1", TegenIban: "NL91ABNA0417164300", Naam: "Iemand anders", Omschrijving: "betaling v2026-001 zaalhuur"),
            new VbTransactie(5000, Ref: "F2", Naam: "De Kerk", Omschrijving: "dank", NtryRef: "2"));

        Assert.Equal(2, resultaat.Herkend);
        var overzicht = Factuuroverzicht.Bereken(_data.Stand, new DateOnly(2026, 11, 20));
        Assert.All(overzicht, f => Assert.Equal(0, f.Saldo));
        var concept = _data.Posten.Single(p => !p.IsGeboekt && p.Omschrijving.Contains("V2026-001", StringComparison.Ordinal));
        Assert.Equal(-25000, concept.Regels.Single(r => r.Rekening.Nummer == 1310).Saldo);
    }

    [Fact]
    public async Task Handmatig_afletteren_van_twee_facturen()
    {
        var relatie = await _dienst.BewaarRelatieAsync(new RelatieInvoer(null, Kerk(iban: null), true));
        var a = await ConceptAsync(relatie);
        await _dienst.MaakDefinitiefAsync(Ver, a);
        var b = await ConceptAsync(relatie, 5000);
        await _dienst.MaakDefinitiefAsync(Ver, b);
        await BankAsync(new VbTransactie(30000, Ref: "F3", Naam: "Penningmeester", Omschrijving: "twee facturen"));
        var mutatie = _data.Bankmutaties.Single();
        Assert.Null(mutatie.PostId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _dienst.LetterAfAsync(Ver, mutatie.Id, [a]));
        await _dienst.LetterAfAsync(Ver, mutatie.Id, [a, b]);

        Assert.NotNull(mutatie.PostId);
        Assert.All(Factuuroverzicht.Bereken(_data.Stand, new DateOnly(2026, 11, 20)), f => Assert.Equal(0, f.Saldo));
    }

    [Fact]
    public async Task Creditfactuur_heft_de_vordering_op()
    {
        var relatie = await _dienst.BewaarRelatieAsync(new RelatieInvoer(null, Kerk(), true));
        var a = await ConceptAsync(relatie);
        await _dienst.MaakDefinitiefAsync(Ver, a);

        var credit = await _dienst.MaakCreditfactuurAsync(Ver, a, new DateOnly(2026, 11, 18));
        Assert.Equal("V2026-002", await _dienst.MaakDefinitiefAsync(Ver, credit));

        var post = _data.Posten.Single(p => p.Id == Factuur(credit).PostId);
        Assert.Equal(-25000, post.Regels.Single(r => r.Rekening.Nummer == 1310).Saldo);
        Assert.Equal(25000, post.Regels.Single(r => r.Rekening.Nummer == 8400).Saldo);
        var overzicht = Factuuroverzicht.Bereken(_data.Stand, new DateOnly(2026, 11, 20));
        Assert.Equal(0, overzicht.Single(f => f.Factuur.Id == a).Saldo);
        Assert.Equal("Gecrediteerd", overzicht.Single(f => f.Factuur.Id == a).Statustekst);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _dienst.MaakCreditfactuurAsync(Ver, a, new DateOnly(2026, 11, 19)));
    }

    [Fact]
    public async Task Factuurinstellingen_bewaren_met_logo()
    {
        await _dienst.BewaarInstellingenAsync(Ver, new FactuurinstellingenInvoer(
            "Scouting Testgroep", "Clubweg 1", "1234 AB", "Dorp", "12345678", CamtVoorbeeld.Betaal, "info@example.org", null, 21, "Dank u wel!"),
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var instellingen = _data.Factuurinstellingen!;
        Assert.Equal(21, instellingen.Termijn);
        Assert.Equal("image/png", instellingen.LogoType);
        Assert.Equal(CamtVoorbeeld.Betaal, instellingen.Iban);
    }

    [Fact]
    public async Task Concept_van_een_herkende_betaling_verwijderen_heropent_de_factuur()
    {
        var relatie = await _dienst.BewaarRelatieAsync(new RelatieInvoer(null, Kerk(), true));
        var a = await ConceptAsync(relatie);
        await _dienst.MaakDefinitiefAsync(Ver, a);
        await BankAsync(new VbTransactie(25000, Ref: "F4", Omschrijving: "V2026-001"));
        var concept = _data.Posten.Single(p => !p.IsGeboekt);

        await _boekhouding.VerwijderConceptAsync(Ver, concept.Id);

        Assert.Equal(25000, Factuuroverzicht.Bereken(_data.Stand, new DateOnly(2026, 11, 20)).Single().Saldo);
    }
}
