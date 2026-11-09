using BtwBoeker.Application.Administraties;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Database;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Tests.Boekhouding;

public sealed class BoekhoudingDienstTests : IAsyncDisposable
{
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private readonly BoekhoudingTestdata _data = new();
    private readonly FakeSessie _sessie;
    private readonly BoekhoudingDienst _dienst;

    public BoekhoudingDienstTests()
    {
        _sessie = new FakeSessie(_data);
        _dienst = new BoekhoudingDienst(_sessie, new VasteTijd(new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero)));
    }

    public ValueTask DisposeAsync() => _sessie.DisposeAsync();

    private PostInvoer Invoer(DateOnly? datum = null, long bedrag = 25000, Guid? id = null) => new(
        id, _data.Memoriaal.Id, datum ?? new DateOnly(2026, 5, 1), "Energie mei",
        [new RegelInvoerId(_data.Energie.Id, null, new Bedrag(bedrag), Bedrag.Nul), new RegelInvoerId(_data.Bank.Id, null, Bedrag.Nul, new Bedrag(bedrag))]);

    [Fact]
    public async Task Concept_opslaan_wijzigen_en_verwijderen()
    {
        var id = await _dienst.BewaarConceptAsync(Ver, Invoer());
        await _dienst.BewaarConceptAsync(Ver, Invoer(bedrag: 30000, id: id));

        var post = Assert.Single(_data.Posten);
        Assert.Equal(PostStatus.Concept, post.Status);
        Assert.Equal(new Bedrag(30000), post.TotaalDebet);

        await _dienst.VerwijderConceptAsync(Ver, id);
        Assert.Empty(_data.Posten);
    }

    [Fact]
    public async Task Nieuwe_rekening_geeft_haar_id_terug()
    {
        var id = await _dienst.BewaarRekeningAsync(Ver, new RekeningInvoer(null, 4116, "Water", null, RekeningSoort.Resultaat, RekeningRol.Geen, true));

        Assert.NotEqual(0, id);
        Assert.Equal(4116, _data.Rekeningen.Single(r => r.Id == id).Nummer);
    }

    [Fact]
    public async Task Volgend_boekjaar_openen_om_alvast_te_boeken()
    {
        await _dienst.OpenBoekjaarAsync(Ver, 2027);

        Assert.Contains(_data.Boekjaren, b => b.Jaar == 2027 && b.IsOpen);
        Assert.Equal("MEM-2027-0001", await _dienst.BoekAsync(Ver, Invoer(datum: new DateOnly(2027, 1, 5))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _dienst.OpenBoekjaarAsync(Ver, 2027));
    }

    [Fact]
    public async Task Boeken_nummert_doorlopend_per_dagboek_en_jaar()
    {
        var eerste = await _dienst.BoekAsync(Ver, Invoer());
        var tweede = await _dienst.BoekAsync(Ver, Invoer());

        Assert.Equal("MEM-2026-0001", eerste);
        Assert.Equal("MEM-2026-0002", tweede);
        Assert.All(_data.Posten, p => Assert.True(p.IsGeboekt));
    }

    [Fact]
    public async Task Niet_in_evenwicht_wordt_niet_geboekt()
    {
        var invoer = Invoer() with
        {
            Regels = [new RegelInvoerId(_data.Energie.Id, null, new Bedrag(100), Bedrag.Nul), new RegelInvoerId(_data.Bank.Id, null, Bedrag.Nul, new Bedrag(90))],
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _dienst.BoekAsync(Ver, invoer));
        Assert.Empty(_data.Posten);
    }

    [Fact]
    public async Task Geboekte_post_kan_alleen_worden_tegengeboekt()
    {
        await _dienst.BoekAsync(Ver, Invoer());
        var post = Assert.Single(_data.Posten);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _dienst.BewaarConceptAsync(Ver, Invoer(id: post.Id)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _dienst.VerwijderConceptAsync(Ver, post.Id));

        var nummer = await _dienst.TegenboekAsync(Ver, post.Id, new DateOnly(2026, 5, 2));

        Assert.Equal("MEM-2026-0002", nummer);
        Assert.Equal(0, Grootboek.Saldo(_data.Stand, _data.Energie, 2026));
    }

    [Fact]
    public async Task Boeken_in_afgesloten_of_onbekend_boekjaar_wordt_geweigerd()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _dienst.BoekAsync(Ver, Invoer(new DateOnly(2027, 1, 5))));
        _data.Boekjaren[0].Sluit(DateTime.UtcNow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _dienst.BoekAsync(Ver, Invoer()));
    }

    [Fact]
    public async Task Jaarafsluiting_boekt_resultaat_naar_reserve_en_maakt_openingsbalans()
    {
        _data.Geboekt(new DateOnly(2026, 1, 1), "Openingsbalans", PostSoort.Openingsbalans, (_data.Bank, 100000, 0), (_data.Reserve, 0, 100000));
        _data.Geboekt(new DateOnly(2026, 3, 3), "Contributie", (_data.Bank, 50000, 0), (_data.Welpen, 0, 50000));
        _data.Geboekt(new DateOnly(2026, 4, 1), "Energie", (_data.Energie, 20000, 0), (_data.Bank, 0, 20000));

        var resultaat = await _dienst.SluitBoekjaarAsync(Ver, 2026);

        Assert.Equal(new Bedrag(30000), resultaat);
        Assert.Single(_sessie.Backups);
        Assert.False(_data.Boekjaren.Single(j => j.Jaar == 2026).IsOpen);
        Assert.Equal(0, Grootboek.Saldo(_data.Stand, _data.Welpen, 2026));
        Assert.Equal(-130000, Grootboek.Saldo(_data.Stand, _data.Reserve, 2026));
        Assert.True(_data.Boekjaren.Single(j => j.Jaar == 2027).IsOpen);
        var opening = _data.Posten.Single(p => p.Soort == PostSoort.Openingsbalans && p.Datum.Year == 2027);
        Assert.Equal(130000, Grootboek.Saldo(_data.Stand, _data.Bank, 2027));
        Assert.Equal(new DateOnly(2027, 1, 1), opening.Datum);
    }

    [Fact]
    public async Task Afsluiten_met_concepten_of_zonder_reserve_wordt_geweigerd()
    {
        await _dienst.BewaarConceptAsync(Ver, Invoer());
        var fout = await Assert.ThrowsAsync<InvalidOperationException>(() => _dienst.SluitBoekjaarAsync(Ver, 2026));
        Assert.Contains("concept", fout.Message, StringComparison.OrdinalIgnoreCase);

        _data.Posten.Clear();
        _data.Reserve.WijzigRol(RekeningRol.Geen);
        fout = await Assert.ThrowsAsync<InvalidOperationException>(() => _dienst.SluitBoekjaarAsync(Ver, 2026));
        Assert.Contains("reserve", fout.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Heropenen_verwijdert_afsluiting_en_openingsbalans_volgend_jaar()
    {
        _data.Geboekt(new DateOnly(2026, 3, 3), "Contributie", (_data.Bank, 50000, 0), (_data.Welpen, 0, 50000));
        await _dienst.SluitBoekjaarAsync(Ver, 2026);

        await _dienst.HeropenBoekjaarAsync(Ver, 2026);

        Assert.True(_data.Boekjaren.Single(j => j.Jaar == 2026).IsOpen);
        Assert.DoesNotContain(_data.Posten, p => p.Soort is PostSoort.Afsluiting or PostSoort.Openingsbalans);
        Assert.Equal(-50000, Grootboek.Saldo(_data.Stand, _data.Welpen, 2026));
    }

    [Fact]
    public async Task Rekeningnummer_is_uniek_en_rol_komt_maar_een_keer_voor()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _dienst.BewaarRekeningAsync(Ver, new RekeningInvoer(null, 4115, "Dubbel", null, RekeningSoort.Resultaat, RekeningRol.Geen, true)));

        await _dienst.BewaarRekeningAsync(Ver, new RekeningInvoer(null, 650, "Bestemmingsreserve", null, RekeningSoort.Balans, RekeningRol.Reserve, true));

        Assert.Equal(RekeningRol.Geen, _data.Reserve.Rol);
        Assert.Equal(RekeningRol.Reserve, _data.Rekeningen.Single(r => r.Nummer == 650).Rol);
    }

    [Fact]
    public async Task Soort_wijzigen_kan_niet_als_er_boekingen_zijn()
    {
        _data.Geboekt(new DateOnly(2026, 3, 3), "Contributie", (_data.Bank, 50000, 0), (_data.Welpen, 0, 50000));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _dienst.BewaarRekeningAsync(Ver,
            new RekeningInvoer(_data.Welpen.Id, 8020, "Contributie Welpen", null, RekeningSoort.Balans, RekeningRol.Geen, true)));
    }

    [Fact]
    public async Task Import_richt_lege_administratie_in_met_openingsbalans()
    {
        var leeg = new BoekhoudingTestdata(ingericht: false);
        await using var sessie = new FakeSessie(leeg);
        var dienst = new BoekhoudingDienst(sessie, new VasteTijd(DateTimeOffset.UtcNow));
        var voorstel = new VoorbladVoorstel(2025,
        [
            new VoorbladRekening(600, "Algemene reserve", "Vermogen", RekeningSoort.Balans, RekeningRol.Reserve, false, Bedrag.Nul, new Bedrag(140000)),
            new VoorbladRekening(1000, "Kas", "Kas Bank", RekeningSoort.Balans, RekeningRol.Kas, false, new Bedrag(40000), Bedrag.Nul),
            new VoorbladRekening(1100, "Bank", "Kas Bank", RekeningSoort.Balans, RekeningRol.Geen, false, new Bedrag(100000), Bedrag.Nul),
            new VoorbladRekening(4115, "Energie", "Terreinkosten", RekeningSoort.Resultaat, RekeningRol.Geen, false, Bedrag.Nul, Bedrag.Nul),
            new VoorbladRekening(9990, "Memoriaal", null, RekeningSoort.Balans, RekeningRol.Geen, true, Bedrag.Nul, Bedrag.Nul),
        ], []);

        await dienst.ImporteerAsync(Ver, voorstel);

        Assert.Equal(4, leeg.Rekeningen.Count);
        Assert.Equal(["Vermogen", "Kas Bank", "Terreinkosten"], leeg.Rubrieken.OrderBy(r => r.Volgorde).Select(r => r.Naam));
        Assert.Contains(leeg.Dagboeken, d => d.Code == "MEM");
        Assert.Contains(leeg.Dagboeken, d => d.Code == "KAS" && d.Rekening!.Nummer == 1000);
        Assert.True(Assert.Single(leeg.Boekjaren).IsOpen);
        var opening = Assert.Single(leeg.Posten);
        Assert.Equal(PostSoort.Openingsbalans, opening.Soort);
        Assert.Equal("MEM-2026-0001", opening.Nummer);
        Assert.Equal(new DateOnly(2026, 1, 1), opening.Datum);
        Assert.Single(sessie.Backups);

        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.ImporteerAsync(Ver, voorstel)); // eenmalig
    }

    [Fact]
    public async Task Import_niet_in_evenwicht_wordt_geweigerd()
    {
        var leeg = new BoekhoudingTestdata(ingericht: false);
        await using var sessie = new FakeSessie(leeg);
        var voorstel = new VoorbladVoorstel(2025,
            [new VoorbladRekening(1100, "Bank", null, RekeningSoort.Balans, RekeningRol.Geen, false, new Bedrag(100), Bedrag.Nul)], []);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new BoekhoudingDienst(sessie, TimeProvider.System).ImporteerAsync(Ver, voorstel));
        Assert.Empty(leeg.Rekeningen);
    }

    [Fact]
    public async Task Verschil_kan_bewust_op_nieuwe_tussenrekening_worden_geboekt()
    {
        var leeg = new BoekhoudingTestdata(ingericht: false);
        await using var sessie = new FakeSessie(leeg);
        var voorstel = new VoorbladVoorstel(2025,
        [
            new VoorbladRekening(600, "Algemene reserve", "Vermogen", RekeningSoort.Balans, RekeningRol.Reserve, false, Bedrag.Nul, new Bedrag(2355664)),
            new VoorbladRekening(1100, "Bank", "Kas Bank", RekeningSoort.Balans, RekeningRol.Geen, false, new Bedrag(2356388), Bedrag.Nul),
        ], []);

        await new BoekhoudingDienst(sessie, TimeProvider.System)
            .ImporteerAsync(Ver, voorstel, new VerschilKeuze(1299, "Te onderzoeken verschillen"));

        var verschil = leeg.Rekeningen.Single(r => r.Nummer == 1299);
        Assert.Equal(RekeningSoort.Balans, verschil.Soort);
        var opening = Assert.Single(leeg.Posten);
        Assert.True(opening.IsInEvenwicht);
        Assert.Equal(new Bedrag(724), opening.Regels.Single(r => r.Rekening == verschil).Credit);
        Assert.Contains("verschil", opening.Regels.Single(r => r.Rekening == verschil).Omschrijving, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Verschil_kan_ook_op_bestaande_rekening_uit_het_schema()
    {
        var leeg = new BoekhoudingTestdata(ingericht: false);
        await using var sessie = new FakeSessie(leeg);
        var voorstel = new VoorbladVoorstel(2025,
        [
            new VoorbladRekening(600, "Algemene reserve", null, RekeningSoort.Balans, RekeningRol.Reserve, false, Bedrag.Nul, new Bedrag(1000)),
            new VoorbladRekening(1100, "Bank", null, RekeningSoort.Balans, RekeningRol.Geen, false, new Bedrag(900), Bedrag.Nul),
            new VoorbladRekening(1298, "Kruisposten", null, RekeningSoort.Balans, RekeningRol.Kruisposten, false, Bedrag.Nul, Bedrag.Nul),
        ], []);

        await new BoekhoudingDienst(sessie, TimeProvider.System).ImporteerAsync(Ver, voorstel, new VerschilKeuze(1298, "Kruisposten"));

        Assert.Equal(3, leeg.Rekeningen.Count);
        Assert.Equal(new Bedrag(100), Assert.Single(leeg.Posten).Regels.Single(r => r.Rekening.Nummer == 1298).Debet);
    }

    [Fact]
    public async Task Verschil_op_resultaatrekening_wordt_geweigerd()
    {
        var leeg = new BoekhoudingTestdata(ingericht: false);
        await using var sessie = new FakeSessie(leeg);
        var voorstel = new VoorbladVoorstel(2025,
        [
            new VoorbladRekening(1100, "Bank", null, RekeningSoort.Balans, RekeningRol.Geen, false, new Bedrag(900), Bedrag.Nul),
            new VoorbladRekening(4115, "Energie", null, RekeningSoort.Resultaat, RekeningRol.Geen, false, Bedrag.Nul, Bedrag.Nul),
        ], []);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new BoekhoudingDienst(sessie, TimeProvider.System).ImporteerAsync(Ver, voorstel, new VerschilKeuze(4115, "Energie")));
        Assert.Empty(leeg.Rekeningen);
    }

    [Fact]
    public async Task Lege_administratie_inrichten_maakt_memoriaal_en_boekjaar()
    {
        var leeg = new BoekhoudingTestdata(ingericht: false);
        await using var sessie = new FakeSessie(leeg);

        await new BoekhoudingDienst(sessie, new VasteTijd(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero))).RichtInAsync(AdministratieSoort.Stichting);

        Assert.Contains(leeg.Dagboeken, d => d.Code == "MEM" && d.Administratie == AdministratieSoort.Stichting);
        Assert.Equal(2026, Assert.Single(leeg.Boekjaren).Jaar);
    }

    internal sealed class VasteTijd(DateTimeOffset nu) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nu.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone("test", nu.Offset, "test", "test");
    }

    internal sealed class FakeSessie(BoekhoudingTestdata data) : IDatabaseSessie
    {
        public string Pad { get; } = Path.Combine(Path.GetTempPath(), "test.btwdb");
        public List<string> Backups { get; } = [];
        public IAdministratieRepository Administraties => throw new NotSupportedException();
        public IBoekhoudingRepository Boekhouding { get; } = data;
        public BtwBoeker.Application.Inrichting.IStartwizardOpslag Startwizard => throw new NotSupportedException();

        public Task MaakBackupAsync(string doelPad, CancellationToken annulering = default)
        {
            Backups.Add(doelPad);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
