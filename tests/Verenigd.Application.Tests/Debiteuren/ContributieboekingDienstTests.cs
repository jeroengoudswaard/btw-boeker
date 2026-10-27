using Verenigd.Application.Boekhouding;
using Verenigd.Application.Debiteuren;
using Verenigd.Application.Tests.Bank;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Tests.Debiteuren;

public sealed class ContributieboekingDienstTests
{
    private const Domain.Administraties.AdministratieSoort Ver = ContributieboekingTestomgeving.Ver;
    private readonly ContributieboekingTestomgeving _o = new();

    private Grootboekrekening Rekening(int nummer) => _o.Boekhouding.Rekeningen.Single(r => r.Nummer == nummer);

    private static VbTransactie Batch(Incassorun run, long centen) =>
        new(centen, "IDDT", "ESDD", "680", Ref: "B-" + run.Kenmerk, Batch: run.AantalPosten, BatchId: run.Kenmerk + "-1", Omschrijving: null, TegenIban: null, Naam: null);

    private static VbTransactie Storno(Incassopost post, string reden = "AM04", string referentie = "S1") =>
        new(-post.Bedrag.Centen, "IDDT", "UPDD", "631", Ref: referentie, E2e: post.EndToEndId, Mandaat: post.Mandaatkenmerk,
            TegenIban: post.Iban.Waarde, Naam: post.Rekeninghouder, Reden: reden, NtryRef: referentie);

    private static Incassopost PostVan(Incassorun run, Domain.Leden.Persoon lid) =>
        run.Posten.Single(p => p.Regels.Any(r => r.Lidnummer == lid.Lidnummer));

    [Fact]
    public async Task Run_boeken_maakt_vordering_en_opbrengst_per_speltak()
    {
        var run = await _o.RunAsync();

        var nummer = await _o.Dienst.BoekRunAsync(run.Id);

        var post = _o.Boekhouding.Posten.Single(p => p.Nummer == nummer);
        Assert.True(post.IsGeboekt);
        Assert.Equal(run.Uitvoerdatum, post.Datum);
        Assert.Contains("november 2026", post.Omschrijving, StringComparison.Ordinal);
        var debiteuren = Rekening(1300);
        Assert.Equal("Debiteuren contributie", debiteuren.Naam);
        Assert.Equal(RekeningSoort.Balans, debiteuren.Soort);
        Assert.Contains(_o.Boekhouding.Rekeningen, r => r.Nummer == 1310);
        Assert.Contains(_o.Boekhouding.Rekeningen, r => r.Nummer == 1600);
        Assert.Equal(2500, post.Regels.Where(r => r.Rekening.Nummer == 1300).Sum(r => r.Saldo));
        Assert.Equal(-1000, post.Regels.Single(r => r.Rekening.Nummer == 8020).Saldo);
        Assert.Equal(-1500, post.Regels.Single(r => r.Rekening.Nummer == 8030).Saldo);
        Assert.Equal(Vorderingstatus.Verwacht, _o.Vordering(_o.Anna).Status);
        Assert.True(Contributievorderingen.IsGeboekt(_o.Boekhouding.Stand, run.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _o.Dienst.BoekRunAsync(run.Id));
    }

    [Fact]
    public async Task Ontbrekende_opbrengstrekening_blokkeert_het_boeken_met_uitleg()
    {
        _o.Incasso.Junioren.KoppelOpbrengstrekening(null);
        var run = await _o.RunAsync();

        Assert.Equal(["Jeugdlid Junioren"], await _o.Dienst.OntbrekendeOpbrengstrekeningenAsync(run.Id));
        var fout = await Assert.ThrowsAsync<InvalidOperationException>(() => _o.Dienst.BoekRunAsync(run.Id));
        Assert.Contains("Jeugdlid Junioren", fout.Message, StringComparison.Ordinal);
        Assert.False(Contributievorderingen.IsGeboekt(_o.Boekhouding.Stand, run.Id));
    }

    [Fact]
    public async Task Ontvangen_batch_lettert_alle_posten_van_de_run_af()
    {
        var run = await _o.RunAsync();
        await _o.Dienst.BoekRunAsync(run.Id);

        var resultaat = await _o.BankAsync(Batch(run, 2500));

        Assert.Equal(1, resultaat.Herkend);
        var concept = _o.Boekhouding.Posten.Single(p => !p.IsGeboekt);
        Assert.Equal(2500, concept.Regels.Single(r => r.Rekening.Nummer == 1100).Saldo);
        Assert.Equal(-2500, concept.Regels.Single(r => r.Rekening.Nummer == 1300).Saldo);
        Assert.All(Contributievorderingen.Bereken(_o.Boekhouding.Stand), v => Assert.Equal(Vorderingstatus.Betaald, v.Status));
        Assert.True(Contributievorderingen.IsOntvangen(_o.Boekhouding.Stand, run.Id));
    }

    [Fact]
    public async Task Batch_met_afwijkend_bedrag_blijft_in_de_werkvoorraad()
    {
        var run = await _o.RunAsync();
        await _o.Dienst.BoekRunAsync(run.Id);

        var resultaat = await _o.BankAsync(Batch(run, 1500));

        Assert.Equal(1, resultaat.Werkvoorraad);
    }

    [Fact]
    public async Task Stornering_zet_de_vordering_weer_open_met_de_reden()
    {
        var run = await _o.RunAsync();
        await _o.Dienst.BoekRunAsync(run.Id);
        await _o.BankAsync(Batch(run, 2500));

        var resultaat = await _o.BankAsync(Storno(PostVan(run, _o.Anna)));

        Assert.Equal(1, resultaat.Herkend);
        var anna = _o.Vordering(_o.Anna);
        Assert.Equal(Vorderingstatus.Gestorneerd, anna.Status);
        Assert.Equal(1000, anna.Saldo);
        Assert.Equal("Onvoldoende saldo (AM04)", anna.Redentekst);
        Assert.Equal(Vorderingstatus.Betaald, _o.Vordering(_o.Bob).Status);
        var storno = _o.Boekhouding.Posten.Single(p => p.Omschrijving.StartsWith("Stornering", StringComparison.Ordinal));
        Assert.Equal(1000, storno.Regels.Single(r => r.Rekening.Nummer == 1300).Saldo);
    }

    [Fact]
    public async Task Handmatige_betaling_met_iban_en_bedrag_van_een_open_post_wordt_herkend()
    {
        var run = await _o.RunAsync();
        await _o.Dienst.BoekRunAsync(run.Id);
        await _o.BankAsync(Batch(run, 2500));
        var post = PostVan(run, _o.Anna);
        await _o.BankAsync(Storno(post));

        var resultaat = await _o.BankAsync(new VbTransactie(1000, Ref: "H1", TegenIban: post.Iban.Waarde, Naam: "Ouders", Omschrijving: "contributie"));

        Assert.Equal(1, resultaat.Herkend);
        Assert.Equal(Vorderingstatus.Betaald, _o.Vordering(_o.Anna).Status);
    }

    [Fact]
    public async Task Handmatig_afletteren_van_meerdere_open_posten()
    {
        var run = await _o.RunAsync();
        await _o.Dienst.BoekRunAsync(run.Id);
        await _o.BankAsync(Batch(run, 2500));
        await _o.BankAsync(Storno(PostVan(run, _o.Anna)), Storno(PostVan(run, _o.Bob), "MD06", "S2"));
        await _o.BankAsync(new VbTransactie(2500, Ref: "H2", TegenIban: CamtVoorbeeld.Tegen, Naam: "Oma", Omschrijving: "voor allebei"));
        var betaling = _o.Boekhouding.Bankmutaties.Single(m => m.Tegennaam == "Oma");
        Assert.Null(betaling.PostId); // geen automatische herkenning: ander IBAN

        var vorderingen = new[] { _o.Vordering(_o.Anna).Vordering, _o.Vordering(_o.Bob).Vordering };
        await Assert.ThrowsAsync<InvalidOperationException>(() => _o.Dienst.LetterAfAsync(betaling.Id, [vorderingen[0]]));

        await _o.Dienst.LetterAfAsync(betaling.Id, vorderingen);

        Assert.NotNull(betaling.PostId);
        Assert.Equal(Vorderingstatus.Betaald, _o.Vordering(_o.Anna).Status);
        Assert.Equal(Vorderingstatus.Betaald, _o.Vordering(_o.Bob).Status);
    }

    [Fact]
    public async Task Oninbare_post_afboeken_op_een_kostenrekening()
    {
        var run = await _o.RunAsync();
        await _o.Dienst.BoekRunAsync(run.Id);
        await _o.BankAsync(Batch(run, 2500));
        await _o.BankAsync(Storno(PostVan(run, _o.Anna)));

        var nummer = await _o.Dienst.BoekOninbaarAsync(_o.Vordering(_o.Anna).Vordering, _o.Boekhouding.Energie.Id);

        var post = _o.Boekhouding.Posten.Single(p => p.Nummer == nummer);
        Assert.True(post.IsGeboekt);
        Assert.Equal(1000, post.Regels.Single(r => r.Rekening.Nummer == 4115).Saldo);
        Assert.Equal(Vorderingstatus.Afgeboekt, _o.Vordering(_o.Anna).Status);
        Assert.Equal(0, _o.Vordering(_o.Anna).Saldo);
    }

    [Fact]
    public async Task Herincasso_gaat_mee_in_de_volgende_run_zonder_nieuwe_opbrengst()
    {
        var run = await _o.RunAsync();
        await _o.Dienst.BoekRunAsync(run.Id);
        await _o.BankAsync(Batch(run, 2500));
        var oorspronkelijk = PostVan(run, _o.Anna);
        await _o.BankAsync(Storno(oorspronkelijk));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _o.Dienst.VraagHerincassoAanAsync(_o.Vordering(_o.Bob).Vordering));

        await _o.Dienst.VraagHerincassoAanAsync(oorspronkelijk.EndToEndId);
        Assert.True(_o.Vordering(_o.Anna).HerincassoGepland);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _o.Dienst.VraagHerincassoAanAsync(oorspronkelijk.EndToEndId));

        var december = await _o.RunAsync(new Maand(2026, 12));

        var her = Assert.Single(december.Posten, p => p.IsHerincasso);
        Assert.Equal(oorspronkelijk.EndToEndId, her.HerincassoVan);
        Assert.Contains("(herincasso)", her.Omschrijving, StringComparison.Ordinal);
        Assert.Equal(oorspronkelijk.Bedrag, her.Bedrag);
        Assert.Equal(december.Id, _o.Incasso.Herincassos.Single().RunId);

        var nummer = await _o.Dienst.BoekRunAsync(december.Id);
        var boeking = _o.Boekhouding.Posten.Single(p => p.Nummer == nummer);
        Assert.Equal(2500, boeking.Regels.Where(r => r.Rekening.Nummer == 1300).Sum(r => r.Saldo)); // alleen december

        await _o.BankAsync(Batch(december, december.Totaal.Centen) with { Ref = "B-DEC" });
        var vorderingen = Contributievorderingen.Bereken(_o.Boekhouding.Stand);
        Assert.Equal(Vorderingstatus.Betaald, vorderingen.Single(v => v.Vordering == oorspronkelijk.EndToEndId).Status);
        Assert.All(vorderingen, v => Assert.Equal(Vorderingstatus.Betaald, v.Status));
    }

    [Fact]
    public async Task Geboekte_run_annuleren_boekt_tegen_en_kan_niet_meer_na_ontvangst()
    {
        var run = await _o.RunAsync();
        await _o.Dienst.BoekRunAsync(run.Id);

        await _o.Dienst.AnnuleerRunAsync(run.Id);

        Assert.Equal(IncassorunStatus.Geannuleerd, run.Status);
        Assert.Contains(_o.Boekhouding.Posten, p => p.Soort == PostSoort.Tegenboeking && p.IsGeboekt);
        Assert.Empty(Contributievorderingen.Bereken(_o.Boekhouding.Stand));
        Assert.Equal(0, Grootboek.Saldo(_o.Boekhouding.Stand, Rekening(1300), 2026));

        var tweede = await _o.RunAsync();
        await _o.Dienst.BoekRunAsync(tweede.Id);
        await _o.BankAsync(Batch(tweede, 2500));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _o.Dienst.AnnuleerRunAsync(tweede.Id));
    }

    [Fact]
    public async Task Concept_van_een_batch_verwijderen_maakt_de_aflettering_ongedaan()
    {
        var run = await _o.RunAsync();
        await _o.Dienst.BoekRunAsync(run.Id);
        await _o.BankAsync(Batch(run, 2500));
        var concept = _o.Boekhouding.Posten.Single(p => !p.IsGeboekt);

        await _o.Boekhoudingdienst.VerwijderConceptAsync(Ver, concept.Id);

        Assert.All(Contributievorderingen.Bereken(_o.Boekhouding.Stand), v => Assert.Equal(Vorderingstatus.Verwacht, v.Status));
        Assert.DoesNotContain(_o.Boekhouding.Debiteurmutaties, m => m.PostId == concept.Id);
    }
}
