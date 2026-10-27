using Microsoft.EntityFrameworkCore;
using Verenigd.Application.Bank;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Application.Debiteuren;
using Verenigd.Application.Tests.Bank;
using Verenigd.Application.Import;
using Verenigd.Application.Incasso;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;
using Verenigd.Infrastructure.Database;

namespace Verenigd.Infrastructure.Tests.Incasso;

/// <summary>Contributie en incasso op een echte, versleutelde database. Alle IBAN's zijn openbare voorbeeldnummers.</summary>
public sealed class ContributieOpslagTests : IAsyncLifetime, IDisposable
{
    private const string Wachtwoord = "scouting is fantastisch";
    private const string IbanA = "NL91ABNA0417164300";
    private const string IbanB = "BE68539007547034";
    private static readonly Maand Maart = new(2027, 3);
    private readonly TijdelijkeMap _map = new();
    private readonly DatabaseBeheer _beheer = new();
    private readonly VasteTijd _tijd = new(new DateTimeOffset(2027, 2, 20, 10, 0, 0, TimeSpan.FromHours(1)));
    private IDatabaseSessie _sessie = null!;

    private IContributieRepository Repo => _sessie.Contributie;

    public async Task InitializeAsync()
    {
        var pad = _map.Bestand("test.vdb");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        _sessie = await _beheer.OpenAsync(pad, Wachtwoord);
    }

    public async Task DisposeAsync() => await _sessie.DisposeAsync();

    public void Dispose() => _map.Dispose();

    private async Task ImporteerLedenAsync(params (string Lidnummer, string Voornaam)[] leden)
    {
        var import = new Sol3Import(
            [.. leden.Select(l => new ImportLid(
                Lidnummer.Parse(l.Lidnummer),
                new LidGegevens(l.Voornaam, null, "Test", new DateOnly(2015, 1, 1), null, null, null, null, "Actief", null, null, null, null, null, null),
                [],
                [new FunctieGegevens("Welpen", "Lid", Lidtype.Jeugdlid, new DateOnly(2026, 12, 15), null, "Actief")],
                null,
                null))],
            []);
        var datum = new DateOnly(2027, 2, 1);
        var stand = await _sessie.LedenImport.LaadAsync();
        var keuzes = ImportKeuzes.Standaard(ImportPlanner.Plan(stand, import, datum));
        await _sessie.LedenImport.PasToeAsync(s => ImportToepasser.PasToe(s, import, keuzes, datum, DateTime.UtcNow));
    }

    private async Task<Contributiecategorie> RichtInAsync()
    {
        await Repo.BewaarInstellingenAsync(new IncassoinstellingenInvoer(
            "DE98ZZZ09999999999", "Scouting Testgroep", IbanA, 1, new Maand(2027, 1), Incassoinstellingen.StandaardSjabloon));
        await new IncassoDienst(_sessie, _tijd).ZorgVoorStandaardcategorieenAsync(["Welpen"]);
        var welpen = (await Repo.LaadAsync()).Categorieen.Single(c => c.Naam == "Jeugdlid Welpen");
        await Repo.BewaarTarievenAsync(2027, new Dictionary<int, Bedrag> { [welpen.Id] = new Bedrag(1250) });
        return welpen;
    }

    private async Task<IncassoDienst> VolledigIngerichtAsync()
    {
        await ImporteerLedenAsync(("100000001", "Anna"), ("100000002", "Bo"));
        var welpen = await RichtInAsync();
        await Repo.BewaarLidCategorieenAsync(
        [
            new(Lidnummer.Parse("100000001"), welpen.Id, new DateOnly(2027, 1, 1)),
            new(Lidnummer.Parse("100000002"), welpen.Id, new DateOnly(2027, 1, 1)),
        ]);
        await Repo.BewaarBetalerAsync(new BetalerInvoer(
            null, IbanA, "P. Test", "VER-0001", new DateOnly(2019, 7, 1), null, [Lidnummer.Parse("100000001"), Lidnummer.Parse("100000002")]));
        return new IncassoDienst(_sessie, _tijd);
    }

    [Fact]
    public async Task Run_boeken_ontvangst_stornering_en_herincasso_worden_bewaard()
    {
        var incasso = await VolledigIngerichtAsync();
        var boekhouding = new BoekhoudingDienst(_sessie, _tijd);
        await boekhouding.RichtInAsync(Domain.Administraties.AdministratieSoort.Vereniging);
        const Domain.Administraties.AdministratieSoort ver = Domain.Administraties.AdministratieSoort.Vereniging;
        var bankId = await boekhouding.BewaarRekeningAsync(ver, new RekeningInvoer(null, 1100, "Bank", null, Domain.Financien.Boekhouding.RekeningSoort.Balans, Domain.Financien.Boekhouding.RekeningRol.Geen, true));
        await boekhouding.BewaarRekeningAsync(ver, new RekeningInvoer(null, 8020, "Contributie Welpen", null, Domain.Financien.Boekhouding.RekeningSoort.Resultaat, Domain.Financien.Boekhouding.RekeningRol.Geen, true));
        var stand = await boekhouding.LaadAsync(ver);
        var welpen = (await Repo.LaadAsync()).Categorieen.Single(c => c.Naam == "Jeugdlid Welpen");
        await Repo.BewaarOpbrengstrekeningenAsync(new Dictionary<int, int?> { [welpen.Id] = stand.Rekeningen.Single(r => r.Nummer == 8020).Id });
        Assert.NotNull((await Repo.LaadAsync()).Categorieen.Single(c => c.Id == welpen.Id).OpbrengstrekeningId);

        var run = await incasso.MaakDefinitiefAsync(await incasso.BerekenAsync(Maart));
        var dienst = new ContributieboekingDienst(_sessie, _tijd);
        await dienst.BoekRunAsync(run.Id);
        var post = run.Posten.Single();

        var bank = new BankDienst(_sessie, _tijd);
        var bestand = Camt053Lezer.Lees(CamtVoorbeeld.Stroom(CamtVoorbeeld.Xml(CamtVoorbeeld.Betaal, "08",
            new VbAfschrift(new DateOnly(2027, 3, 2), 0,
                new VbTransactie(run.Totaal.Centen, "IDDT", "ESDD", "680", Ref: "B1", Batch: 1, BatchId: run.Kenmerk + "-1", TegenIban: null, Naam: null, Omschrijving: null),
                new VbTransactie(-post.Bedrag.Centen, "IDDT", "UPDD", "631", Ref: "S1", E2e: post.EndToEndId, TegenIban: IbanA, Reden: "AM04", NtryRef: "2")))));
        var resultaat = await bank.ImporteerAsync(ver, BankimportPlanner.Plan(await boekhouding.LaadAsync(ver), [bestand]),
            new Dictionary<string, NieuweBankrekening> { [CamtVoorbeeld.Betaal] = new(bankId, "RABO", "Rabobank") });
        Assert.Equal(2, resultaat.Herkend);

        stand = await boekhouding.LaadAsync(ver);
        var vordering = Assert.Single(Contributievorderingen.Bereken(stand));
        Assert.Equal(Vorderingstatus.Gestorneerd, vordering.Status);
        Assert.Equal("AM04", vordering.Reden);
        Assert.Equal("AM04", stand.Bankmutaties.Single(m => !m.IsBij).Retourreden);

        await dienst.VraagHerincassoAanAsync(vordering.Vordering);
        var april = await incasso.MaakDefinitiefAsync(await incasso.BerekenAsync(new Maand(2027, 4)));
        Assert.Contains(april.Posten, p => p.HerincassoVan == post.EndToEndId);
        var herincasso = (await Repo.LaadAsync()).Herincassos.Single();
        Assert.Equal(april.Id, herincasso.RunId);
        Assert.Contains(april.Posten.SelectMany(p => p.Regels), r => r.IsHerincasso && r.Maand == Maart);

        await dienst.AnnuleerRunAsync(april.Id);
        Assert.True((await Repo.LaadAsync()).Herincassos.Single().IsOpen);
    }

    [Fact]
    public async Task Instellingen_worden_bewaard_en_ongeldig_incassant_id_geweigerd()
    {
        await RichtInAsync();

        var instellingen = (await Repo.LaadAsync()).Instellingen!;
        Assert.Equal("DE98ZZZ09999999999", instellingen.IncassantId.Waarde);
        Assert.Equal(new Maand(2027, 1), instellingen.EersteIncassomaand);

        await Assert.ThrowsAsync<ArgumentException>(() => Repo.BewaarInstellingenAsync(new IncassoinstellingenInvoer(
            "DE97ZZZ09999999999", "x", IbanA, 1, Maart, Incassoinstellingen.StandaardSjabloon)));
    }

    [Fact]
    public async Task Tarieven_en_categoriehistorie()
    {
        var welpen = await RichtInAsync();
        await Repo.BewaarTarievenAsync(2027, new Dictionary<int, Bedrag> { [welpen.Id] = new Bedrag(1300) });
        var lid = Lidnummer.Parse("100000001");
        var leiding = (await Repo.LaadAsync()).Categorieen.Single(c => c.Soort == CategorieSoort.Leiding);

        await Repo.BewaarLidCategorieenAsync([new(lid, welpen.Id, new DateOnly(2027, 1, 1))]);
        await Repo.BewaarLidCategorieenAsync([new(lid, leiding.Id, new DateOnly(2027, 1, 1)), new(lid, welpen.Id, new DateOnly(2027, 6, 1))]);

        var stand = await Repo.LaadAsync();
        Assert.Equal(new Bedrag(1300), Assert.Single(stand.Tarieven, t => t.Boekjaar == 2027).Maandbedrag);
        Assert.Equal(2, stand.LidCategorieen.Count);
        Assert.Equal("Leiding", LidCategorie.Op(stand.LidCategorieen, lid, new DateOnly(2027, 3, 1))!.Categorie.Naam);
    }

    [Fact]
    public async Task Betaler_met_machtiging_en_leden_verplaatsen()
    {
        var a = Lidnummer.Parse("100000001");
        var b = Lidnummer.Parse("100000002");
        var idA = await Repo.BewaarBetalerAsync(new BetalerInvoer(null, IbanA, "P. Test", "VER-0001", new DateOnly(2019, 7, 1), null, [a, b]));
        var idB = await Repo.BewaarBetalerAsync(new BetalerInvoer(null, IbanB, "Q. Test", null, null, null, [b]));

        var stand = await Repo.LaadAsync();
        Assert.Equal(idA, stand.Koppelingen.Single(k => k.Lidnummer == a).BetalerId);
        Assert.Equal(idB, stand.Koppelingen.Single(k => k.Lidnummer == b).BetalerId);
        Assert.True(stand.Betalers.Single(x => x.Id == idA).HeeftActieveMachtiging(new DateOnly(2027, 3, 1)));

        await Repo.BewaarBetalerAsync(new BetalerInvoer(idA, IbanA, "P. Test", "VER-0001", new DateOnly(2019, 7, 1), new DateOnly(2027, 2, 1), []));
        stand = await Repo.LaadAsync();
        Assert.DoesNotContain(stand.Koppelingen, k => k.Lidnummer == a);
        Assert.False(stand.Betalers.Single(x => x.Id == idA).HeeftActieveMachtiging(new DateOnly(2027, 3, 1)));

        var fout = await Assert.ThrowsAsync<ArgumentException>(() =>
            Repo.BewaarBetalerAsync(new BetalerInvoer(null, "nl91 abna 0417 1643 00", null, null, null, null, [])));
        Assert.Contains("al een betaler", fout.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Volledige_run_met_momentopname_bestand_en_geen_dubbele_incasso()
    {
        var dienst = await VolledigIngerichtAsync();

        var voorstel = await dienst.BerekenAsync(Maart);
        Assert.Equal(new Bedrag(7500), voorstel.Totaal); // 2 leden × jan–mrt
        var run = await dienst.MaakDefinitiefAsync(voorstel);
        var bestand = await dienst.MaakBestandAsync(run);

        Assert.Empty(Pain008Schrijver.Valideer(bestand.Inhoud));
        Assert.True(File.Exists(Assert.Single(Directory.GetFiles(_map.Pad, "*-voor-incasso-*.vbak"))));

        // IBAN van de betaler wijzigt later: de run blijft wat er naar de bank ging.
        var stand = await Repo.LaadAsync();
        var betaler = stand.Betalers.Single();
        await Repo.BewaarBetalerAsync(new BetalerInvoer(betaler.Id, IbanB, "P. Test", "VER-0001", new DateOnly(2019, 7, 1), null,
            [Lidnummer.Parse("100000001"), Lidnummer.Parse("100000002")]));

        stand = await Repo.LaadAsync();
        var opgeslagen = Assert.Single(stand.Runs);
        Assert.Equal(IbanA, Assert.Single(opgeslagen.Posten).Iban.Waarde);
        Assert.Equal(6, opgeslagen.Posten[0].Regels.Count);
        Assert.Equal(Pain008Schrijver.Hash(bestand.Inhoud), opgeslagen.BestandHash);
        Assert.True((await dienst.BerekenAsync(Maart)).IsLeeg);
    }

    [Fact]
    public async Task Database_weigert_dezelfde_maand_twee_keer_tot_annuleren()
    {
        var dienst = await VolledigIngerichtAsync();
        var voorstel = await dienst.BerekenAsync(Maart);
        var run = await dienst.MaakDefinitiefAsync(voorstel);

        // Omzeil de rekenregel: probeer dezelfde regels nogmaals rechtstreeks op te slaan.
        await Assert.ThrowsAsync<DbUpdateException>(() => Repo.BewaarRunAsync(_ => Kopie(run)));

        await dienst.AnnuleerAsync(run);
        var stand = await Repo.LaadAsync();
        Assert.Equal(IncassorunStatus.Geannuleerd, stand.Runs.Single().Status);
        Assert.All(stand.Runs.Single().Posten.SelectMany(p => p.Regels), r => Assert.True(r.IsGeannuleerd));

        var opnieuw = await dienst.MaakDefinitiefAsync(await dienst.BerekenAsync(Maart));
        Assert.Equal(new Bedrag(7500), opnieuw.Totaal);
        Assert.Equal(2, (await Repo.LaadAsync()).Runs.Count);
    }

    private static Incassorun Kopie(Incassorun run)
    {
        var kopie = new Incassorun(run.Maand, run.Uitvoerdatum, DateTime.UtcNow);
        foreach (var post in run.Posten)
        {
            var nieuw = kopie.VoegPostToe(post.BetalerId, post.Iban, post.Rekeninghouder, post.Mandaatkenmerk, post.OndertekendOp, post.Omschrijving);
            foreach (var regel in post.Regels)
            {
                nieuw.VoegRegelToe(regel.Lidnummer, regel.Maand, regel.Categorie, regel.Bedrag);
            }
        }

        return kopie;
    }

    private sealed class VasteTijd(DateTimeOffset nu) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nu.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone("test", nu.Offset, "test", "test");
    }
}
