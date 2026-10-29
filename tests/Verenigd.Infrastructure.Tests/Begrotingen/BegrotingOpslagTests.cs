using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Verenigd.Application.Begrotingen;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Infrastructure.Database;
using Verenigd.Infrastructure.Excel;

namespace Verenigd.Infrastructure.Tests.Begrotingen;

/// <summary>Begroting en baten/lasten in de echte, versleutelde database (besluiten 149 en 152). Bedragen zijn verzonnen.</summary>
public sealed class BegrotingOpslagTests : IAsyncLifetime, IDisposable
{
    private const string Wachtwoord = "scouting is fantastisch";
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private readonly TijdelijkeMap _map = new();
    private readonly DatabaseBeheer _beheer = new();
    private IDatabaseSessie _sessie = null!;

    public async Task InitializeAsync()
    {
        var pad = _map.Bestand("test.vdb");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        _sessie = await _beheer.OpenAsync(pad, Wachtwoord);
        var excel = _map.Bestand("boekhouding.xlsx");
        using (var boek = new XLWorkbook())
        {
            var blad = boek.Worksheets.Add("Voorblad");
            blad.Cell(1, 1).Value = 2025;
            blad.Cell(3, 2).Value = "Vermogen";
            blad.Cell(4, 1).Value = 600; blad.Cell(4, 2).Value = "Algemene Reserve"; blad.Cell(4, 10).Value = 100;
            blad.Cell(5, 2).Value = "Kas Bank";
            blad.Cell(6, 1).Value = 1100; blad.Cell(6, 2).Value = "Bank"; blad.Cell(6, 9).Value = 100;
            blad.Cell(7, 2).Value = "Exploitatie";
            blad.Cell(8, 1).Value = 4115; blad.Cell(8, 2).Value = "Energie"; blad.Cell(8, 7).Value = 300;
            blad.Cell(9, 1).Value = 4900; blad.Cell(9, 2).Value = "Verhuur"; blad.Cell(9, 8).Value = 250; // credit: baten, ondanks nummer < 8000
            boek.SaveAs(excel);
        }

        await new BoekhoudingDienst(_sessie, TimeProvider.System)
            .ImporteerAsync(Ver, VoorbladInlezer.Lees(new ClosedXmlWerkbladLezer().Lees(excel, VoorbladInlezer.Werkbladnaam)));
    }

    public async Task DisposeAsync() => await _sessie.DisposeAsync();

    public void Dispose() => _map.Dispose();

    [Fact]
    public async Task Overname_uit_excel_bewaart_baten_of_lasten_uit_het_saldo_van_vorig_jaar()
    {
        var stand = await _sessie.Boekhouding.LaadAsync(Ver);

        Assert.Equal(ResultaatKant.Lasten, stand.Rekeningen.Single(r => r.Nummer == 4115).Kant);
        Assert.Equal(ResultaatKant.Baten, stand.Rekeningen.Single(r => r.Nummer == 4900).Kant);
        Assert.Equal(ResultaatKant.Geen, stand.Rekeningen.Single(r => r.Nummer == 1100).Kant);
    }

    [Fact]
    public async Task Begroting_met_versies_wordt_opgeslagen_en_teruggelezen()
    {
        var dienst = new BegrotingDienst(_sessie, TimeProvider.System);
        var stand = await _sessie.Boekhouding.LaadAsync(Ver);
        var energie = stand.Rekeningen.Single(r => r.Nummer == 4115);

        var id = await dienst.MaakAsync(Ver, 2027);
        await dienst.BewaarAsync(Ver, id, [new BegrotingInvoer(energie.Id, new Bedrag(130000), "Nieuw contract")]);
        await dienst.StelVastAsync(Ver, id, new DateOnly(2026, 12, 12), "ALV 12-12-2026");
        var herziening = await dienst.HerzieAsync(Ver, id);
        await dienst.BewaarAsync(Ver, herziening, [new BegrotingInvoer(energie.Id, new Bedrag(140000), null)]);

        stand = await _sessie.Boekhouding.LaadAsync(Ver);
        Assert.Equal(2, stand.Begrotingen.Count);
        var eerste = stand.Begrotingen.Single(b => b.Versie == 1);
        Assert.True(eerste.IsVastgesteld);
        Assert.Equal("ALV 12-12-2026", eerste.Omschrijving);
        Assert.Equal(new Bedrag(130000), eerste.Bedrag(energie));
        Assert.Equal("Nieuw contract", eerste.Regels.Single().Toelichting);
        Assert.Equal(new Bedrag(140000), stand.Begrotingen.Single(b => b.Versie == 2).Bedrag(energie));
        Assert.Same(eerste, Begrotingsrekenaar.Geldend(stand, 2027)); // de herziening is nog een concept
    }

    [Fact]
    public void Migratie_stelt_de_kant_voor_uit_het_saldo_zonder_afsluitpost_anders_op_nummer()
    {
        using var db = new SqliteConnection("Data Source=:memory:");
        db.Open();
        Voer(db, """
            CREATE TABLE "Grootboekrekeningen" ("Id" INTEGER PRIMARY KEY, "Nummer" INTEGER, "Soort" INTEGER, "Kant" INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE "Journaalposten" ("Id" TEXT PRIMARY KEY, "Soort" INTEGER);
            CREATE TABLE "Journaalregels" ("Id" INTEGER PRIMARY KEY, "PostId" TEXT, "RekeningId" INTEGER, "Debet" INTEGER, "Credit" INTEGER);
            CREATE TABLE "HistorischeTotalen" ("Id" INTEGER PRIMARY KEY, "RekeningId" INTEGER, "Debet" INTEGER, "Credit" INTEGER);
            INSERT INTO "Grootboekrekeningen" VALUES (1, 1100, 1, 0), (2, 4900, 2, 0), (3, 8020, 2, 0), (4, 4115, 2, 0), (5, 8500, 2, 0), (6, 4300, 2, 0);
            INSERT INTO "Journaalposten" VALUES ('gewoon', 1), ('afsluiting', 3);
            -- 4900: per saldo credit (baten); de afsluitpost draait dat om, maar telt niet mee
            INSERT INTO "Journaalregels" VALUES (1, 'gewoon', 2, 0, 25000), (2, 'afsluiting', 2, 25000, 0);
            -- 8020: per saldo debet in de historie (lasten), ondanks het nummer
            INSERT INTO "HistorischeTotalen" VALUES (1, 3, 1000, 0);
            """);

        Voer(db, Infrastructure.Database.Migraties.MigratieSql.KantVoorstellen);

        Assert.Equal(0L, Kant(db, 1)); // balans
        Assert.Equal(1L, Kant(db, 2)); // baten uit saldo
        Assert.Equal(2L, Kant(db, 3)); // lasten uit de historie
        Assert.Equal(2L, Kant(db, 4)); // geen saldo, < 8000: lasten
        Assert.Equal(1L, Kant(db, 5)); // geen saldo, >= 8000: baten
        Assert.Equal(2L, Kant(db, 6));
    }

    private static void Voer(SqliteConnection db, string sql)
    {
        using var opdracht = db.CreateCommand();
        opdracht.CommandText = sql;
        opdracht.ExecuteNonQuery();
    }

    private static long Kant(SqliteConnection db, int id)
    {
        using var opdracht = db.CreateCommand();
        opdracht.CommandText = $"SELECT \"Kant\" FROM \"Grootboekrekeningen\" WHERE \"Id\" = {id}";
        return (long)opdracht.ExecuteScalar()!;
    }
}
