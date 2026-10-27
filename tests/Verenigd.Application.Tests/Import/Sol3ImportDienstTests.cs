using Verenigd.Application.Administraties;
using Verenigd.Application.Database;
using Verenigd.Application.Import;
using Verenigd.Application.Leden;
using static Verenigd.Application.Tests.Import.Sol3Voorbeeld;

namespace Verenigd.Application.Tests.Import;

public sealed class Sol3ImportDienstTests : IDisposable
{
    private readonly DirectoryInfo _map = Directory.CreateTempSubdirectory("verenigd-import-");
    private readonly FakeSessie _sessie;
    private readonly Sol3ImportDienst _dienst;

    public Sol3ImportDienstTests()
    {
        _sessie = new FakeSessie(Path.Combine(_map.FullName, "test.vdb"));
        _dienst = new Sol3ImportDienst(_sessie, new VasteTijd(new DateTimeOffset(2026, 10, 27, 10, 15, 0, TimeSpan.FromHours(1))));
    }

    public void Dispose() => _map.Delete(recursive: true);

    private string Schrijf(string naam, byte[] inhoud)
    {
        var pad = Path.Combine(_map.FullName, naam);
        File.WriteAllBytes(pad, inhoud);
        return pad;
    }

    private Dictionary<Sol3Export, string> VierExports() => new()
    {
        [Sol3Export.LedenBasis] = Schrijf("1.csv", Csv(KopLeden, Lid("100000001"), Lid("100000002", voornaam: "Bo"))),
        [Sol3Export.Huishouden] = Schrijf("2.csv", Csv(KopHuishouden, Huishouden("100000001"), Huishouden("100000002"))),
        [Sol3Export.Voogden] = Schrijf("3.csv", Csv(KopVoogden, Voogden("100000001"))),
        [Sol3Export.Functies] = Schrijf("4.csv", Csv(KopFuncties, Functie("100000001"), Functie("100000002"))),
    };

    [Fact]
    public void Bestand_wordt_herkend_aan_kopregel_met_aantal_regels()
    {
        var herkenning = Sol3ImportDienst.HerkenBestand(Schrijf("x.csv", Csv(KopFuncties, Functie("1"), Functie("2"))));

        Assert.Equal(Sol3Export.Functies, herkenning.Export);
        Assert.Equal(2, herkenning.AantalRegels);
        Assert.Null(herkenning.Fout);
    }

    [Fact]
    public void Onbekend_bestand_geeft_fout()
    {
        var herkenning = Sol3ImportDienst.HerkenBestand(Schrijf("x.csv", Csv("Naam;Adres", ["a", "b"])));

        Assert.Null(herkenning.Export);
        Assert.NotNull(herkenning.Fout);
    }

    [Fact]
    public void Ontbrekende_kolom_geeft_fout_met_kolomnaam()
    {
        var kop = string.Join(';', KopLeden.Split(';').Where(k => k != "Geboortedatum"));
        var herkenning = Sol3ImportDienst.HerkenBestand(Schrijf("x.csv", Csv(kop)));

        Assert.Equal(Sol3Export.LedenBasis, herkenning.Export);
        Assert.Contains("Geboortedatum", herkenning.Fout, StringComparison.Ordinal);
    }

    [Fact]
    public void Niet_bestaand_bestand_geeft_fout()
    {
        Assert.NotNull(Sol3ImportDienst.HerkenBestand(Path.Combine(_map.FullName, "bestaat-niet.csv")).Fout);
    }

    [Fact]
    public async Task Alle_vier_exports_zijn_verplicht()
    {
        var paden = VierExports();
        paden.Remove(Sol3Export.Functies);

        var fout = await Assert.ThrowsAsync<Sol3ImportException>(() => _dienst.AnalyseerAsync(paden));
        Assert.Contains("Functies", fout.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyse_maakt_voorstel_zonder_iets_te_wijzigen()
    {
        var analyse = await _dienst.AnalyseerAsync(VierExports());

        Assert.Equal(2, analyse.Voorstel.Nieuw.Count);
        Assert.Equal(new DateOnly(2026, 10, 27), analyse.Importdatum);
        Assert.Empty(_sessie.Opslag.Stand.Personen);
        Assert.Empty(_sessie.Backups);
    }

    [Fact]
    public async Task Uitvoeren_maakt_eerst_een_backup_en_slaat_dan_op()
    {
        var analyse = await _dienst.AnalyseerAsync(VierExports());

        var resultaat = await _dienst.VoerUitAsync(analyse, ImportKeuzes.Standaard(analyse.Voorstel));

        var backup = Assert.Single(_sessie.Backups);
        Assert.Equal(Path.Combine(_map.FullName, "test-voor-import-20261027-101500.vbak"), backup);
        Assert.Equal(backup, resultaat.Backup);
        Assert.Equal(0, _sessie.Opslag.PersonenBijBackup);
        Assert.Equal(2, _sessie.Opslag.Stand.Personen.Count);
        Assert.Equal(2, resultaat.Nieuw);
    }

    [Fact]
    public async Task Mislukte_backup_voorkomt_import()
    {
        _sessie.BackupMislukt = true;
        var analyse = await _dienst.AnalyseerAsync(VierExports());

        await Assert.ThrowsAsync<IOException>(() => _dienst.VoerUitAsync(analyse, ImportKeuzes.Standaard(analyse.Voorstel)));
        Assert.Empty(_sessie.Opslag.Stand.Personen);
    }

    [Fact]
    public async Task Tweede_keer_dezelfde_import_is_leeg()
    {
        var analyse = await _dienst.AnalyseerAsync(VierExports());
        await _dienst.VoerUitAsync(analyse, ImportKeuzes.Standaard(analyse.Voorstel));

        Assert.True((await _dienst.AnalyseerAsync(VierExports())).Voorstel.IsLeeg);
    }

    private sealed class VasteTijd(DateTimeOffset nu) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nu.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.CreateCustomTimeZone("test", nu.Offset, "test", "test");
    }

    private sealed class FakeOpslag : ILedenImportOpslag
    {
        public ImportStand Stand { get; private set; } = ImportStand.Leeg;
        public int? PersonenBijBackup { get; set; }

        public Task<ImportStand> LaadAsync(CancellationToken annulering = default) => Task.FromResult(Stand);

        public Task PasToeAsync(Func<ImportStand, ImportWijzigingen> wijziging, CancellationToken annulering = default)
        {
            var w = wijziging(Stand);
            Stand = new ImportStand(
                [.. Stand.Personen, .. w.NieuwePersonen], [.. Stand.Groepen, .. w.NieuweGroepen],
                [.. Stand.Betalers, .. w.NieuweBetalers], [.. Stand.Koppelingen, .. w.NieuweKoppelingen]);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSessie(string pad) : IDatabaseSessie
    {
        public string Pad { get; } = pad;
        public FakeOpslag Opslag { get; } = new();
        public List<string> Backups { get; } = [];
        public bool BackupMislukt { get; set; }

        public IAdministratieRepository Administraties => throw new NotSupportedException();
        public ILedenRepository Leden => throw new NotSupportedException();
        public ILedenImportOpslag LedenImport => Opslag;

        public Task MaakBackupAsync(string doelPad, CancellationToken annulering = default)
        {
            if (BackupMislukt)
            {
                throw new IOException("schijf vol");
            }

            Opslag.PersonenBijBackup = Opslag.Stand.Personen.Count;
            Backups.Add(doelPad);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
