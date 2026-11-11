using ClosedXML.Excel;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Database;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;
using BtwBoeker.Infrastructure.Database;
using BtwBoeker.Infrastructure.Excel;
using BtwBoeker.Application.Tests.Boekhouding;

namespace BtwBoeker.Infrastructure.Tests.Boekhouding;

/// <summary>Grootboek op een echte, versleutelde database, ingericht met een verzonnen voorstel.</summary>
public sealed class BoekhoudingOpslagTests : IAsyncLifetime, IDisposable
{
    private const string Wachtwoord = "zonnig vakantiehuis 2026";
    private const AdministratieSoort Adm = AdministratieSoort.Standaard;
    private readonly TijdelijkeMap _map = new();
    private readonly DatabaseBeheer _beheer = new();
    private IDatabaseSessie _sessie = null!;
    private BoekhoudingDienst _dienst = null!;

    public async Task InitializeAsync()
    {
        var pad = _map.Bestand("test.btwdb");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        _sessie = await _beheer.OpenAsync(pad, Wachtwoord);
        _dienst = new BoekhoudingDienst(_sessie, TimeProvider.System);
    }

    public async Task DisposeAsync() => await _sessie.DisposeAsync();

    public void Dispose() => _map.Dispose();

    private Task ImporteerAsync(decimal bank = 799.5m, VerschilKeuze? verschil = null) =>
        _dienst.ImporteerAsync(Adm, Testvoorstel.Standaard(bank), verschil);

    private static PostInvoer Invoer(BoekhoudingStand stand, long bedrag, Guid? id = null) => new(
        id, stand.Dagboeken.Single(d => d.Code == "MEM").Id, new DateOnly(2026, 5, 1), "Energie mei",
        [
            new RegelInvoerId(stand.Rekeningen.Single(r => r.Nummer == 4115).Id, null, new Bedrag(bedrag), Bedrag.Nul),
            new RegelInvoerId(stand.Rekeningen.Single(r => r.Nummer == 1100).Id, null, Bedrag.Nul, new Bedrag(bedrag)),
        ]);

    [Fact]
    public async Task Inrichten_maakt_schema_dagboeken_en_openingsbalans()
    {
        await ImporteerAsync();

        var stand = await _dienst.LaadAsync(Adm);
        Assert.Equal([600, 1000, 1100, 4115, 8020], stand.Rekeningen.Select(r => r.Nummer));
        Assert.Equal("Parkkosten", stand.Rekeningen.Single(r => r.Nummer == 4115).Rubriek?.Naam);
        Assert.Equal(["MEM", "KAS"], stand.Dagboeken.Select(d => d.Code));
        Assert.Equal(2026, Assert.Single(stand.Boekjaren).Jaar);
        var opening = Assert.Single(stand.Posten);
        Assert.Equal("MEM-2026-0001", opening.Nummer);
        Assert.Equal(20050, Grootboek.Saldo(stand, stand.Rekeningen.Single(r => r.Nummer == 1000), 2026));
        Assert.Single(Directory.GetFiles(_map.Pad, "*-voor-boekhouding-import-*.btwbackup"));
    }

    [Fact]
    public async Task Import_met_verschil_alleen_na_bewuste_keuze()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => ImporteerAsync(bank: 806.74m));
        Assert.False((await _dienst.LaadAsync(Adm)).IsIngericht);

        await ImporteerAsync(bank: 806.74m, verschil: VerschilKeuze.Standaard);

        var stand = await _dienst.LaadAsync(Adm);
        var verschil = stand.Rekeningen.Single(r => r.Nummer == 1299);
        Assert.Equal(-724, Grootboek.Saldo(stand, verschil, 2026));
        Assert.True(Assert.Single(stand.Posten).IsInEvenwicht);
    }

    [Fact]
    public async Task Concept_wijzigen_boeken_en_tegenboeken_worden_opgeslagen()
    {
        await ImporteerAsync();
        var stand = await _dienst.LaadAsync(Adm);

        var id = await _dienst.BewaarConceptAsync(Adm, Invoer(stand, 10000));
        await _dienst.BewaarConceptAsync(Adm, Invoer(stand, 12000, id));
        var nummer = await _dienst.BoekAsync(Adm, Invoer(stand, 12500, id));
        var tegen = await _dienst.TegenboekAsync(Adm, id, new DateOnly(2026, 5, 2));

        stand = await _dienst.LaadAsync(Adm);
        Assert.Equal("MEM-2026-0002", nummer);
        Assert.Equal("MEM-2026-0003", tegen);
        Assert.Equal(2, stand.Posten.Single(p => p.Id == id).Regels.Count); // oude conceptregels zijn weg
        Assert.Equal(0, Grootboek.Saldo(stand, stand.Rekeningen.Single(r => r.Nummer == 4115), 2026));
    }

    [Fact]
    public async Task Jaarafsluiting_en_heropenen_worden_opgeslagen()
    {
        await ImporteerAsync();
        var stand = await _dienst.LaadAsync(Adm);
        await _dienst.BoekAsync(Adm, Invoer(stand, 30000));

        var resultaat = await _dienst.SluitBoekjaarAsync(Adm, 2026);

        stand = await _dienst.LaadAsync(Adm);
        Assert.Equal(new Bedrag(30000), new Bedrag(-resultaat.Centen));
        Assert.False(stand.Boekjaar(2026)!.IsOpen);
        Assert.True(stand.Boekjaar(2027)!.IsOpen);
        Assert.Equal(-70000 - 0, Grootboek.Saldo(stand, stand.Rekeningen.Single(r => r.Rol == RekeningRol.Reserve), 2027));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _dienst.BoekAsync(Adm, Invoer(stand, 100)));

        await _dienst.HeropenBoekjaarAsync(Adm, 2026);
        stand = await _dienst.LaadAsync(Adm);
        Assert.True(stand.Boekjaar(2026)!.IsOpen);
        Assert.DoesNotContain(stand.Posten, p => p.Soort == PostSoort.Afsluiting);
    }

    [Fact]
    public async Task Rapport_naar_xlsx_bevat_bedragen_als_getal()
    {
        await ImporteerAsync();
        var stand = await _dienst.LaadAsync(Adm);
        var pad = _map.Bestand("balans.xlsx");

        new ClosedXmlSpreadsheetSchrijver().Schrijf(Rapporten.Balans(Grootboek.Balans(stand, 2026), "Mijn administratie"), pad);

        using var boek = new XLWorkbook(pad);
        var blad = boek.Worksheet(1);
        Assert.Equal("Balans per 31-12-2026", blad.Cell(1, 1).GetText());
        Assert.Contains(blad.CellsUsed(), c => c.DataType == XLDataType.Number && c.GetDouble() == 1000.0);
    }


}
