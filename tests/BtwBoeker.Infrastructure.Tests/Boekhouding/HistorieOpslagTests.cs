using ClosedXML.Excel;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Database;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Infrastructure.Database;
using BtwBoeker.Infrastructure.Excel;

namespace BtwBoeker.Infrastructure.Tests.Boekhouding;

/// <summary>Het historische boekjaar uit een gegenereerd xlsx-bestand, op een echte versleutelde database. Gegevens zijn verzonnen.</summary>
public sealed class HistorieOpslagTests : IAsyncLifetime, IDisposable
{
    private const string Wachtwoord = "scouting is fantastisch";
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private readonly TijdelijkeMap _map = new();
    private readonly DatabaseBeheer _beheer = new();
    private IDatabaseSessie _sessie = null!;

    public async Task InitializeAsync()
    {
        var pad = _map.Bestand("test.btwdb");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        _sessie = await _beheer.OpenAsync(pad, Wachtwoord);
    }

    public async Task DisposeAsync() => await _sessie.DisposeAsync();

    public void Dispose() => _map.Dispose();

    private string MaakBoekhouding()
    {
        var pad = _map.Bestand("boekhouding.xlsx");
        using var boek = new XLWorkbook();
        var voorblad = boek.Worksheets.Add("Voorblad");
        voorblad.Cell(1, 1).Value = 2025;
        voorblad.Cell(3, 2).Value = "Vermogen";
        voorblad.Cell(4, 1).Value = 600; voorblad.Cell(4, 2).Value = "Algemene Reserve"; voorblad.Cell(4, 10).Value = 100;
        voorblad.Cell(5, 2).Value = "Kas Bank";
        voorblad.Cell(6, 1).Value = 1100; voorblad.Cell(6, 2).Value = "Bank"; voorblad.Cell(6, 9).Value = 100;
        voorblad.Cell(7, 2).Value = "Kosten";
        voorblad.Cell(8, 1).Value = 4115; voorblad.Cell(8, 2).Value = "Energie"; voorblad.Cell(8, 7).Value = 30;
        voorblad.Cell(10, 3).FormulaA1 = "SUM(C3:C9)";

        var blad = boek.Worksheets.Add("KasBank");
        blad.Cell(2, 1).Value = 1100; blad.Cell(2, 2).Value = "Bank"; blad.Cell(2, 3).Value = "Debet"; blad.Cell(2, 4).Value = "Credit";
        blad.Cell(3, 2).Value = "Beginsaldo"; blad.Cell(3, 3).Value = 130;
        blad.Cell(4, 1).Value = new DateTime(2025, 1, 31); blad.Cell(4, 2).Value = "Januari"; blad.Cell(4, 4).Value = 30;
        blad.Cell(5, 3).FormulaA1 = "SUM(C3:C4)";
        blad.Cell(2, 7).Value = 4115; blad.Cell(2, 8).Value = "Energie"; blad.Cell(2, 9).Value = "Debet"; blad.Cell(2, 10).Value = "Credit";
        blad.Cell(3, 7).Value = new DateTime(2025, 1, 31); blad.Cell(3, 8).Value = "Stroom"; blad.Cell(3, 9).Value = 30;
        boek.SaveAs(pad);
        return pad;
    }

    [Fact]
    public async Task Historie_wordt_gelezen_opgeslagen_en_teruggelezen()
    {
        var pad = MaakBoekhouding();
        var lezer = new ClosedXmlWerkbladLezer();
        var tijd = TimeProvider.System;
        await new BoekhoudingDienst(_sessie, tijd).ImporteerAsync(Ver, VoorbladInlezer.Lees(lezer.Lees(pad, VoorbladInlezer.Werkbladnaam)));

        var bladen = lezer.LeesAlle(pad);
        Assert.Equal(["Voorblad", "KasBank"], bladen.Select(b => b.Naam));
        var voorstel = HistorieInlezer.Lees(bladen);
        Assert.Equal(3, voorstel.AantalRegels); // de SUM-regel telt niet mee
        Assert.True(voorstel.Rekeningen.Single(r => r.Nummer == 1100).Klopt);
        Assert.Equal(3, await new HistorieDienst(_sessie, tijd).ImporteerAsync(Ver, voorstel, OnbekendeRekening.Overslaan));

        var stand = await _sessie.Boekhouding.LaadAsync(Ver);
        Assert.True(stand.Boekjaar(2025)!.IsHistorisch);
        Assert.Equal(3, stand.HistorischeRegels.Count);
        Assert.Equal(3, stand.HistorischeTotalen.Count);
        Assert.Equal(new Bedrag(3000), Grootboek.WinstEnVerlies(stand, 2025).TotaalLasten);
        var bank = stand.Rekeningen.Single(r => r.Nummer == 1100);
        var kaart = Grootboek.Kaart(stand, bank, 2025, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));
        Assert.Equal(["Beginsaldo", "Januari"], kaart.Mutaties.Select(m => m.Omschrijving));
        Assert.Equal(10000, kaart.TotaalVolgensVoorblad);
        Assert.Equal(10000, Grootboek.Saldo(stand, bank, 2026)); // openingsbalans 2026, los van de historie
    }
}
