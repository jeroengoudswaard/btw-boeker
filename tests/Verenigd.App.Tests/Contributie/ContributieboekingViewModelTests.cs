using Verenigd.App.ViewModels.Boekhouding;
using Verenigd.App.ViewModels.Contributie;
using Verenigd.Application.Bank;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Debiteuren;
using Verenigd.Application.Incasso;
using Verenigd.Application.Tests.Bank;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.App.Tests.Contributie;

/// <summary>Stap 4c-1: opbrengstrekeningen, automatisch boeken, Openstaand en afletteren.</summary>
public sealed class ContributieboekingViewModelTests : IDisposable
{
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private readonly ContributieTestomgeving _omgeving = new();
    private readonly DirectoryInfo _map = Directory.CreateTempSubdirectory("verenigd-boeking-");
    private int _bankId;

    public void Dispose() => _map.Delete(recursive: true);

    private BoekhoudingDienst Boekhouding => new(_omgeving.Sessie, _omgeving.Tijd);

    /// <summary>Leden, tarieven (welpen € 12,50), een betaler voor Anna en een ingerichte boekhouding met 1100, 8020 en 4115.</summary>
    private async Task<ContributieViewModel> OpenAsync(bool metOpbrengstrekening = true)
    {
        await _omgeving.VulLedenAsync();
        await _omgeving.RichtIncassoInAsync();
        await Boekhouding.RichtInAsync(Ver);
        _bankId = await Boekhouding.BewaarRekeningAsync(Ver, new RekeningInvoer(null, 1100, "Bank", null, RekeningSoort.Balans, RekeningRol.Geen, true));
        await Boekhouding.BewaarRekeningAsync(Ver, new RekeningInvoer(null, 8020, "Contributie Welpen", null, RekeningSoort.Resultaat, RekeningRol.Geen, true));
        await Boekhouding.BewaarRekeningAsync(Ver, new RekeningInvoer(null, 4115, "Kosten", null, RekeningSoort.Resultaat, RekeningRol.Geen, true));
        var vm = await _omgeving.OpenAsync();
        vm.VoorstellenOvernemenCommand.Execute(null);
        vm.Tarieven.Single(t => t.Categorie == "Jeugdlid Welpen").Bedragtekst = "12,50";
        if (metOpbrengstrekening)
        {
            vm.Opbrengstrekeningen.Single(r => r.Categorie == "Jeugdlid Welpen").GekozenRekening = vm.Opbrengstkeuzes.Single(r => r.Nummer == 8020);
        }

        Assert.True(await vm.BewaarAsync());
        await _omgeving.Opslag.BewaarBetalerAsync(new BetalerInvoer(null, ContributieTestomgeving.IbanA, "P. Test", "VER-0001",
            new DateOnly(2019, 7, 1), null, [Domain.Gedeeld.Lidnummer.Parse("100000001")]));
        await vm.VernieuwCommand.ExecuteAsync(null);
        return vm;
    }

    private async Task<IncassoWizardViewModel> RunViaWizardAsync(ContributieViewModel vm)
    {
        IncassoWizardViewModel? gebruikt = null;
        _omgeving.Dialogen.XmlDoel = Path.Combine(_map.FullName, "incasso.xml");
        _omgeving.Dialogen.IncassoWizard = async wizard =>
        {
            gebruikt = wizard;
            await wizard.VolgendeCommand.ExecuteAsync(null);
            await wizard.VolgendeCommand.ExecuteAsync(null);
            wizard.IsGecontroleerd = true;
            await wizard.VolgendeCommand.ExecuteAsync(null);
            return wizard.IsKlaar;
        };
        await vm.NieuweRunCommand.ExecuteAsync(null);
        return gebruikt!;
    }

    private async Task StorneerAsync(Incassorun run)
    {
        var post = run.Posten.Single();
        var bestand = Camt053Lezer.Lees(CamtVoorbeeld.Stroom(CamtVoorbeeld.Xml(CamtVoorbeeld.Betaal, "08",
            new VbAfschrift(new DateOnly(2027, 3, 2), 0,
                new VbTransactie(run.Totaal.Centen, "IDDT", "ESDD", "680", Ref: "B1", Batch: 1, BatchId: run.Kenmerk + "-1", TegenIban: null, Naam: null, Omschrijving: null),
                new VbTransactie(-post.Bedrag.Centen, "IDDT", "UPDD", "631", Ref: "S1", E2e: post.EndToEndId, TegenIban: post.Iban.Waarde, Reden: "AM04", NtryRef: "2"),
                new VbTransactie(post.Bedrag.Centen, Ref: "H1", TegenIban: CamtVoorbeeld.Tegen, Naam: "Oma", Omschrijving: "voor Anna", NtryRef: "3")))));
        var stand = await _omgeving.Sessie.Boekhouding.LaadAsync(Ver);
        await new BankDienst(_omgeving.Sessie, _omgeving.Tijd).ImporteerAsync(Ver, BankimportPlanner.Plan(stand, [bestand]),
            new Dictionary<string, NieuweBankrekening> { [CamtVoorbeeld.Betaal] = new(_bankId, "RABO", "Rabobank") });
    }

    [Fact]
    public async Task Opbrengstrekening_per_categorie_wordt_bewaard()
    {
        await OpenAsync();

        var welpen = _omgeving.Opslag.Categorieen.Single(c => c.Naam == "Jeugdlid Welpen");
        Assert.NotNull(welpen.OpbrengstrekeningId);
        Assert.Null(_omgeving.Opslag.Categorieen.Single(c => c.Naam == "Leiding").OpbrengstrekeningId);
    }

    [Fact]
    public async Task Definitief_maken_boekt_de_run_automatisch()
    {
        var vm = await OpenAsync();

        var wizard = await RunViaWizardAsync(vm);

        Assert.Contains("geboekt als MEM-2027-0001", wizard.Samenvatting, StringComparison.Ordinal);
        var run = Assert.Single(vm.Runs);
        Assert.Equal("MEM-2027-0001", run.Geboekt);
        Assert.False(vm.BoekRunCommand.CanExecute(run));
    }

    [Fact]
    public async Task Zonder_opbrengstrekening_volgt_een_melding_en_later_boeken()
    {
        var vm = await OpenAsync(metOpbrengstrekening: false);

        var wizard = await RunViaWizardAsync(vm);

        Assert.Contains("nog niet geboekt", wizard.Samenvatting, StringComparison.Ordinal);
        Assert.Contains("Jeugdlid Welpen", wizard.Samenvatting, StringComparison.Ordinal);
        var run = Assert.Single(vm.Runs);
        Assert.Equal("—", run.Geboekt);
        Assert.True(vm.BoekRunCommand.CanExecute(run));

        vm.Opbrengstrekeningen.Single(r => r.Categorie == "Jeugdlid Welpen").GekozenRekening = vm.Opbrengstkeuzes.Single(r => r.Nummer == 8020);
        Assert.True(await vm.BewaarAsync());
        await vm.BoekRunCommand.ExecuteAsync(vm.Runs.Single());

        Assert.Equal("MEM-2027-0001", vm.Runs.Single().Geboekt);
    }

    [Fact]
    public async Task Openstaand_toont_stornering_met_reden_en_herincasso()
    {
        var vm = await OpenAsync();
        await RunViaWizardAsync(vm);
        await StorneerAsync(_omgeving.Opslag.Runs.Single());

        await vm.VernieuwCommand.ExecuteAsync(null);

        var open = Assert.Single(vm.Vorderingen);
        Assert.Equal("Gestorneerd", open.Status);
        Assert.Equal("Onvoldoende saldo (AM04)", open.Reden);
        Assert.Equal("€ 37,50", vm.TotaalOpenstaand);
        vm.GeselecteerdeVordering = open;
        Assert.True(vm.HerincassoCommand.CanExecute(null));

        await vm.HerincassoCommand.ExecuteAsync(null);

        Assert.True(Assert.Single(vm.Vorderingen).HerincassoGepland);
        Assert.Single(_omgeving.Opslag.Herincassos);
        Assert.False(vm.HerincassoCommand.CanExecute(null));
    }

    [Fact]
    public async Task Na_definitief_maken_wordt_de_vooraankondiging_per_betaler_aangeboden()
    {
        var vm = await OpenAsync();
        var voorstellen = new List<ViewModels.Email.EmailVoorstel>();
        vm.EmailAanbieden = v =>
        {
            voorstellen.Add(v);
            return Task.CompletedTask;
        };

        await RunViaWizardAsync(vm);

        Assert.Contains("vooraankondiging", _omgeving.Dialogen.Bevestigingsvragen[^1], StringComparison.Ordinal);
        var voorstel = Assert.Single(voorstellen);
        Assert.Equal(Domain.Communicatie.Emailsoort.Vooraankondiging, voorstel.Soort);
        var ontvanger = Assert.Single(voorstel.Ontvangers);
        Assert.Equal("voogd@example.org", ontvanger.Adres); // Anna is jeugdlid: de voogd
        Assert.Equal("VER-0001", ontvanger.Velden["mandaatkenmerk"]);
        Assert.Equal("€ 37,50", ontvanger.Velden["bedrag"]);
    }

    [Fact]
    public async Task Bij_een_stornering_kan_een_bericht_worden_opgesteld()
    {
        var vm = await OpenAsync();
        await RunViaWizardAsync(vm);
        await StorneerAsync(_omgeving.Opslag.Runs.Single());
        await vm.VernieuwCommand.ExecuteAsync(null);
        var voorstellen = new List<ViewModels.Email.EmailVoorstel>();
        vm.EmailAanbieden = v =>
        {
            voorstellen.Add(v);
            return Task.CompletedTask;
        };
        Assert.False(vm.MailStorneringCommand.CanExecute(null));

        vm.GeselecteerdeVordering = vm.Vorderingen.Single();
        await vm.MailStorneringCommand.ExecuteAsync(null);

        var voorstel = Assert.Single(voorstellen);
        Assert.Equal(Domain.Communicatie.Emailsoort.Stornering, voorstel.Soort);
        Assert.Equal("Onvoldoende saldo (AM04)", Assert.Single(voorstel.Ontvangers).Velden["reden"]);
    }

    [Fact]
    public async Task Oninbaar_afboeken_na_bevestiging()
    {
        var vm = await OpenAsync();
        await RunViaWizardAsync(vm);
        await StorneerAsync(_omgeving.Opslag.Runs.Single());
        await vm.VernieuwCommand.ExecuteAsync(null);
        vm.GeselecteerdeVordering = vm.Vorderingen.Single();
        Assert.True(vm.KanOninbaarNu); // besluit 270: de acties verschijnen onder de gekozen post
        Assert.True(vm.KanHerincassoNu);
        Assert.False(vm.IsOninbaarKeuzeOpen);
        vm.OninbaarKiezenCommand.Execute(null); // de kostenrekening kiest u pas na Afboeken als oninbaar…
        Assert.True(vm.IsOninbaarKeuzeOpen);
        vm.Oninbaarrekening = vm.Kostenrekeningen.Single(r => r.Nummer == 4115);
        _omgeving.Dialogen.BevestigAntwoord = true;

        await vm.OninbaarCommand.ExecuteAsync(null);

        Assert.False(vm.IsOninbaarKeuzeOpen);
        Assert.Empty(vm.Vorderingen); // alleen openstaand
        vm.AlleenOpenstaand = false;
        Assert.Equal("Afgeboekt", Assert.Single(vm.Vorderingen).Status);
    }

    [Fact]
    public async Task Bank_ontvangst_afletteren_met_een_open_post()
    {
        var contributie = await OpenAsync();
        await RunViaWizardAsync(contributie);
        await StorneerAsync(_omgeving.Opslag.Runs.Single());
        var boekhouding = new BoekhoudingViewModel(_omgeving.Sessie, _omgeving.Dialogen, _omgeving.Tijd, ContributieTestomgeving.Vereniging,
            FakeBoekhouding.Hulpmiddelen(), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await boekhouding.VernieuwCommand.ExecuteAsync(null);

        boekhouding.GeselecteerdeMutatie = boekhouding.Mutaties.Single(m => m.Tegenpartij == "Oma");
        var keuze = Assert.Single(boekhouding.Afletterkeuzes);
        Assert.False(boekhouding.AfletterenCommand.CanExecute(null));
        keuze.IsGekozen = true;
        Assert.Equal("€ 37,50", boekhouding.Afletterentotaal);
        Assert.True(boekhouding.AfletterenCommand.CanExecute(null));

        await boekhouding.AfletterenCommand.ExecuteAsync(null);

        Assert.Equal(0, boekhouding.AantalTeVerwerken);
        var stand = await _omgeving.Sessie.Boekhouding.LaadAsync(Ver);
        Assert.Equal(Vorderingstatus.Betaald, Assert.Single(Contributievorderingen.Bereken(stand)).Status);
    }
}
