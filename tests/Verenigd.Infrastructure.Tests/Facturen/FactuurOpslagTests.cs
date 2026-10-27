using ClosedXML.Excel;
using QRCoder;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Application.Facturen;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Debiteuren;
using Verenigd.Domain.Financien.Relaties;
using Verenigd.Infrastructure.Database;
using Verenigd.Infrastructure.Excel;
using Verenigd.Infrastructure.Pdf;

namespace Verenigd.Infrastructure.Tests.Facturen;

/// <summary>Relaties en verkoopfacturen op een echte, versleutelde database, plus de PDF. Het IBAN is een openbaar voorbeeldnummer.</summary>
public sealed class FactuurOpslagTests : IAsyncLifetime, IDisposable
{
    private const string Wachtwoord = "scouting is fantastisch";
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private const string Iban = "NL91ABNA0417164300";
    private readonly TijdelijkeMap _map = new();
    private readonly DatabaseBeheer _beheer = new();
    private IDatabaseSessie _sessie = null!;
    private FactuurDienst _dienst = null!;

    public async Task InitializeAsync()
    {
        var pad = _map.Bestand("test.vdb");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        _sessie = await _beheer.OpenAsync(pad, Wachtwoord);
        _dienst = new FactuurDienst(_sessie, TimeProvider.System);
        var excel = _map.Bestand("boekhouding.xlsx");
        using (var boek = new XLWorkbook())
        {
            var blad = boek.Worksheets.Add("Voorblad");
            blad.Cell(1, 1).Value = 2025;
            blad.Cell(1, 2).Value = "Omschrijving";
            blad.Cell(3, 2).Value = "Kas Bank";
            blad.Cell(4, 1).Value = 1100; blad.Cell(4, 2).Value = "Bank"; blad.Cell(4, 9).Value = 100;
            blad.Cell(5, 1).Value = 600; blad.Cell(5, 2).Value = "Algemene Reserve"; blad.Cell(5, 10).Value = 100;
            blad.Cell(7, 2).Value = "Opbrengsten";
            blad.Cell(8, 1).Value = 8400; blad.Cell(8, 2).Value = "Verhuur"; blad.Cell(8, 8).Value = 50;
            boek.SaveAs(excel);
        }

        var voorstel = VoorbladInlezer.Lees(new ClosedXmlWerkbladLezer().Lees(excel, VoorbladInlezer.Werkbladnaam));
        await new BoekhoudingDienst(_sessie, TimeProvider.System).ImporteerAsync(Ver, voorstel, null);
    }

    public async Task DisposeAsync() => await _sessie.DisposeAsync();

    public void Dispose() => _map.Dispose();

    private async Task<Guid> FactuurAsync(Guid relatie, long prijs = 12500)
    {
        var stand = await _dienst.LaadAsync(Ver);
        return await _dienst.BewaarConceptAsync(Ver, new FactuurInvoer(null, relatie, new DateOnly(2026, 11, 10), 14,
            [new FactuurregelInvoerId("Verhuur clubhuis", 2, new Bedrag(prijs), stand.Rekeningen.Single(r => r.Nummer == 8400).Id)]));
    }

    private Task<Guid> RelatieAsync() => _dienst.BewaarRelatieAsync(new RelatieInvoer(null,
        new RelatieGegevens("Stichting De Kerk", "J. Jansen", "Kerkstraat 1", "1234 AB", "Dorp", "info@example.org", null, Iban, true, false, null), true));

    [Fact]
    public async Task Definitieve_factuur_wordt_genummerd_geboekt_en_opgeslagen()
    {
        var relatie = await RelatieAsync();
        var eerste = await FactuurAsync(relatie);
        var tweede = await FactuurAsync(relatie, 5000);

        Assert.Equal("V2026-001", await _dienst.MaakDefinitiefAsync(Ver, eerste));
        Assert.Equal("V2026-002", await _dienst.MaakDefinitiefAsync(Ver, tweede));

        var stand = await _dienst.LaadAsync(Ver);
        var factuur = stand.Verkoopfacturen.Single(f => f.Id == eerste);
        Assert.Equal(25000, factuur.Totaal.Centen);
        Assert.Equal("Verhuur clubhuis", Assert.Single(factuur.Regels).Omschrijving);
        Assert.Equal(2m, factuur.Regels[0].Aantal);
        Assert.Contains("Stichting De Kerk", factuur.Ontvanger, StringComparison.Ordinal);
        var post = stand.Posten.Single(p => p.Id == factuur.PostId);
        Assert.Equal("VRK", post.Dagboek.Code);
        Assert.Contains(stand.Rekeningen, r => r.Nummer == 1310);
        Assert.Equal(DebiteurmutatieSoort.Vordering, stand.Debiteurmutaties.Single(m => m.Vordering == "V2026-001").Soort);
        Assert.Equal(Iban, Assert.Single(stand.Relaties).Iban);
    }

    [Fact]
    public async Task Creditfactuur_maakt_saldo_nul_en_concept_kan_worden_verwijderd()
    {
        var relatie = await RelatieAsync();
        var id = await FactuurAsync(relatie);
        await _dienst.MaakDefinitiefAsync(Ver, id);
        var credit = await _dienst.MaakCreditfactuurAsync(Ver, id, new DateOnly(2026, 11, 12));
        Assert.Equal("V2026-002", await _dienst.MaakDefinitiefAsync(Ver, credit));
        var concept = await FactuurAsync(relatie);
        await _dienst.VerwijderConceptAsync(Ver, concept);

        var stand = await _dienst.LaadAsync(Ver);
        var overzicht = Factuuroverzicht.Bereken(stand, new DateOnly(2026, 12, 31));
        Assert.Equal(2, overzicht.Count);
        Assert.Equal("Gecrediteerd", overzicht.Single(f => f.Factuur.Id == id).Statustekst);
        Assert.False(overzicht.Single(f => f.Factuur.Id == id).IsVervallen);
    }

    [Fact]
    public async Task Instellingen_met_logo_worden_opgeslagen_en_de_pdf_wordt_gemaakt()
    {
        using var qr = new QRCodeGenerator().CreateQrCode("Verenigd", QRCodeGenerator.ECCLevel.M);
        var logo = FactuurPdfMaker.QrPng(qr, 4);
        await _dienst.BewaarInstellingenAsync(Ver, new FactuurinstellingenInvoer(
            "Scouting Voorbeeld", "Clubweg 1", "1234 AB", "Dorp", "12345678", Iban, "penningmeester@example.org", null, 14, "Dank u wel."), logo);
        var relatie = await RelatieAsync();
        var id = await FactuurAsync(relatie);
        await _dienst.MaakDefinitiefAsync(Ver, id);
        var credit = await _dienst.MaakCreditfactuurAsync(Ver, id, new DateOnly(2026, 11, 12));
        await _dienst.MaakDefinitiefAsync(Ver, credit);

        var stand = await _dienst.LaadAsync(Ver);
        var instellingen = stand.Factuurinstellingen!;
        Assert.Equal("image/png", instellingen.LogoType);
        Assert.Equal(logo, instellingen.Logo);
        var maker = new FactuurPdfMaker();
        foreach (var factuur in stand.Verkoopfacturen)
        {
            var pdf = maker.Maak(factuur, instellingen);
            Assert.True(pdf.Length > 1000);
            Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);
        }
    }

    [Fact]
    public void Qr_code_is_een_8_bits_grijswaarden_png_die_pdfsharp_kan_lezen()
    {
        using var data = new QRCodeGenerator().CreateQrCode("BCD", QRCodeGenerator.ECCLevel.M);
        var png = FactuurPdfMaker.QrPng(data, 2);

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
        Assert.Equal(data.ModuleMatrix.Count * 2, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
        Assert.Equal(8, png[24]); // bitdiepte
        Assert.Equal(0, png[25]); // kleurtype grijswaarden
        Assert.Equal(0xAE426082u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(png.Length - 4))); // CRC van IEND
    }

    [Fact]
    public void Pdf_zonder_instellingen_of_logo_werkt_ook()
    {
        var relatie = new Relatie(new RelatieGegevens("Jan", null, null, null, null, null, null, null, true, false, null));
        var factuur = new Domain.Financien.Facturen.Verkoopfactuur(Ver, relatie, new DateOnly(2026, 11, 1), 14);
        var pdf = new FactuurPdfMaker().Maak(factuur, new Domain.Financien.Facturen.Factuurinstellingen(Ver));
        Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);
    }
}
