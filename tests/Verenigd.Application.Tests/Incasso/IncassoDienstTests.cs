using Verenigd.Application.Administraties;
using Verenigd.Application.Database;
using Verenigd.Application.Import;
using Verenigd.Application.Incasso;
using Verenigd.Application.Leden;
using Verenigd.Domain.Financien;

namespace Verenigd.Application.Tests.Incasso;

public sealed class IncassoDienstTests : IAsyncDisposable
{
    private static readonly Maand Maart = new(2027, 3);
    private readonly IncassoTestdata _data = new();
    private readonly FakeSessie _sessie;
    private readonly IncassoDienst _dienst;

    public IncassoDienstTests()
    {
        _sessie = new FakeSessie(_data);
        _dienst = new IncassoDienst(_sessie, new VasteTijd(new DateTimeOffset(2027, 2, 20, 10, 0, 0, TimeSpan.FromHours(1))));
        _data.Lid("1", "Anna", new DateOnly(2027, 1, 1), categorie: _data.Welpen);
        _data.Lid("2", "Bo", new DateOnly(2027, 1, 1), categorie: _data.Welpen, iban: null);
    }

    public ValueTask DisposeAsync() => _sessie.DisposeAsync();

    [Fact]
    public async Task Definitief_maken_maakt_eerst_backup_en_legt_run_vast()
    {
        var voorstel = await _dienst.BerekenAsync(Maart);

        var run = await _dienst.MaakDefinitiefAsync(voorstel);

        Assert.EndsWith("test-voor-incasso-2027-03-20270220-100000.vbak", Assert.Single(_sessie.Backups), StringComparison.Ordinal);
        Assert.Equal(0, _sessie.RunsBijBackup);
        Assert.Same(run, Assert.Single(_data.Runs));
        Assert.Equal(new Bedrag(3750), run.Totaal);
        Assert.True((await _dienst.BerekenAsync(Maart)).IsLeeg);
    }

    [Fact]
    public async Task Gewijzigde_gegevens_na_berekenen_worden_geweigerd()
    {
        var voorstel = await _dienst.BerekenAsync(Maart);
        _data.Tarieven.Single(t => t.Categorie == _data.Welpen && t.Boekjaar == 2027).WijzigBedrag(new Bedrag(9999));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _dienst.MaakDefinitiefAsync(voorstel));
        Assert.Empty(_data.Runs);
    }

    [Fact]
    public async Task Leeg_voorstel_kan_niet_definitief_worden()
    {
        _data.Personen.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _dienst.MaakDefinitiefAsync(await _dienst.BerekenAsync(Maart)));
    }

    [Fact]
    public async Task Bestand_wordt_gevalideerd_en_hash_vastgelegd()
    {
        var run = await _dienst.MaakDefinitiefAsync(await _dienst.BerekenAsync(Maart));

        var bestand = await _dienst.MaakBestandAsync(run);
        var opnieuw = await _dienst.MaakBestandAsync(run);

        Assert.Equal("incasso-2027-03.xml", bestand.Bestandsnaam);
        Assert.Equal(bestand.Inhoud, opnieuw.Inhoud);
        Assert.Equal(Pain008Schrijver.Hash(bestand.Inhoud), run.BestandHash);
    }

    [Fact]
    public async Task Annuleren_maakt_de_maanden_weer_vrij()
    {
        var run = await _dienst.MaakDefinitiefAsync(await _dienst.BerekenAsync(Maart));

        await _dienst.AnnuleerAsync(run);

        Assert.Equal(IncassorunStatus.Geannuleerd, run.Status);
        Assert.False((await _dienst.BerekenAsync(Maart)).IsLeeg);
    }

    [Fact]
    public async Task Zelf_te_innen_als_csv_zonder_iban()
    {
        var csv = IncassoDienst.ZelfTeInnenCsv(await _dienst.BerekenAsync(Maart));

        var regels = csv.TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("\"Lidnummer\";\"Naam\";\"Maanden\";\"Bedrag\";\"Reden\"", regels[0]);
        Assert.Contains("\"2\";\"Bo Test\";\"jan, feb en mrt 2027\";\"€ 37,50\"", regels[1], StringComparison.Ordinal);
        Assert.DoesNotContain("NL91", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Standaardcategorieen_per_speltak_plus_leiding_en_vrijgesteld()
    {
        _data.Categorieen.Clear();
        _data.Tarieven.Clear();

        await _dienst.ZorgVoorStandaardcategorieenAsync(["Welpen", "Junioren"]);
        await _dienst.ZorgVoorStandaardcategorieenAsync(["Welpen", "Loodsen"]);

        Assert.Equal(
            ["Jeugdlid Junioren", "Jeugdlid Loodsen", "Jeugdlid Welpen", "Leiding", "Vrijgesteld"],
            _data.Categorieen.Select(c => c.Naam).Order(StringComparer.Ordinal));
    }

    private sealed class VasteTijd(DateTimeOffset nu) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nu.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone("test", nu.Offset, "test", "test");
    }

    /// <summary>Werkt rechtstreeks op de testdata, alsof het de database is.</summary>
    internal sealed class FakeRepository(IncassoTestdata data) : IContributieRepository
    {
        public Task<IncassoStand> LaadAsync(CancellationToken annulering = default) => Task.FromResult(data.Stand);

        public Task BewaarRunAsync(Func<IncassoStand, Incassorun> maakRun, CancellationToken annulering = default)
        {
            data.Runs.Add(maakRun(data.Stand));
            return Task.CompletedTask;
        }

        public Task LegBestandVastAsync(Guid runId, string hash, CancellationToken annulering = default)
        {
            data.Runs.Single(r => r.Id == runId).LegBestandVast(hash);
            return Task.CompletedTask;
        }

        public Task AnnuleerRunAsync(Guid runId, DateTime nuUtc, CancellationToken annulering = default)
        {
            var run = data.Runs.Single(r => r.Id == runId);
            if (run.Status == IncassorunStatus.Definitief)
            {
                run.Annuleer(nuUtc);
            }

            foreach (var herincasso in data.Herincassos.Where(h => h.RunId == runId))
            {
                herincasso.GeefVrij();
            }

            return Task.CompletedTask;
        }

        public Task VraagHerincassoAanAsync(Domain.Financien.Debiteuren.Herincasso herincasso, CancellationToken annulering = default)
        {
            data.Herincassos.Add(herincasso);
            return Task.CompletedTask;
        }

        public Task BewaarOpbrengstrekeningenAsync(IReadOnlyDictionary<int, int?> perCategorie, CancellationToken annulering = default)
        {
            foreach (var (id, rekening) in perCategorie)
            {
                data.Categorieen.Single(c => c.Id == id).KoppelOpbrengstrekening(rekening);
            }

            return Task.CompletedTask;
        }

        public Task VoegCategorieenToeAsync(IReadOnlyList<Contributiecategorie> categorieen, CancellationToken annulering = default)
        {
            data.Categorieen.AddRange(categorieen);
            return Task.CompletedTask;
        }

        public Task BewaarTarievenAsync(int boekjaar, IReadOnlyDictionary<int, Bedrag> bedragen, CancellationToken annulering = default) =>
            throw new NotSupportedException();

        public Task BewaarLidCategorieenAsync(IReadOnlyList<LidCategorieInvoer> invoer, CancellationToken annulering = default) =>
            throw new NotSupportedException();

        public Task<Guid> BewaarBetalerAsync(BetalerInvoer invoer, CancellationToken annulering = default) =>
            throw new NotSupportedException();

        public Task BewaarInstellingenAsync(IncassoinstellingenInvoer invoer, CancellationToken annulering = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSessie(IncassoTestdata data) : IDatabaseSessie
    {
        public string Pad { get; } = Path.Combine(Path.DirectorySeparatorChar + "data", "test.vdb");
        public List<string> Backups { get; } = [];
        public int? RunsBijBackup { get; private set; }
        public IAdministratieRepository Administraties => throw new NotSupportedException();
        public ILedenRepository Leden => throw new NotSupportedException();
        public ILedenImportOpslag LedenImport => throw new NotSupportedException();
        public IContributieRepository Contributie { get; } = new FakeRepository(data);
        public Verenigd.Application.Boekhouding.IBoekhoudingRepository Boekhouding => throw new NotSupportedException();
        public Verenigd.Application.Inrichting.IStartwizardOpslag Startwizard => throw new NotSupportedException();

        public Task MaakBackupAsync(string doelPad, CancellationToken annulering = default)
        {
            RunsBijBackup = data.Runs.Count;
            Backups.Add(doelPad);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
