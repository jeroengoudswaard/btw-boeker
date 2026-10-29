using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Verenigd.Application.Bijlagen;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Infrastructure.Database;
using Verenigd.Infrastructure.Excel;
using Verenigd.Infrastructure.Pdf;

namespace Verenigd.Infrastructure.Tests.Bijlagen;

/// <summary>Bijlagen in de echte, versleutelde database en het lezen van PDF-tekst (besluiten 84, 96 en 135). Gegevens zijn verzonnen.</summary>
public sealed class BijlageOpslagTests : IAsyncLifetime, IDisposable
{
    private const string Wachtwoord = "scouting is fantastisch";
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private readonly TijdelijkeMap _map = new();
    private readonly DatabaseBeheer _beheer = new();
    private IDatabaseSessie _sessie = null!;
    private BoekhoudingDienst _boekhouding = null!;
    private BijlageDienst _bijlagen = null!;

    public async Task InitializeAsync()
    {
        var pad = _map.Bestand("test.vdb");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        _sessie = await _beheer.OpenAsync(pad, Wachtwoord);
        _boekhouding = new BoekhoudingDienst(_sessie, TimeProvider.System);
        _bijlagen = new BijlageDienst(_sessie, new PdfPigTekstLezer(), TimeProvider.System);
    }

    public async Task DisposeAsync() => await _sessie.DisposeAsync();

    public void Dispose() => _map.Dispose();

    /// <summary>Een kleine, geldige PDF met tekst in Helvetica (zonder ingesloten lettertype).</summary>
    internal static byte[] TestPdf(params string[] regels)
    {
        var inhoud = new StringBuilder("BT /F1 12 Tf 50 750 Td 14 TL\n");
        foreach (var regel in regels)
        {
            inhoud.Append('(').Append(regel.Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal)).Append(") Tj T*\n");
        }

        inhoud.Append("ET");
        string[] objecten =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {inhoud.Length} >>\nstream\n{inhoud}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        ];
        var pdf = new StringBuilder("%PDF-1.4\n");
        var posities = new List<int>();
        for (var i = 0; i < objecten.Length; i++)
        {
            posities.Add(pdf.Length);
            pdf.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objecten[i]}\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objecten.Length + 1}\n0000000000 65535 f \n");
        foreach (var positie in posities)
        {
            pdf.Append(CultureInfo.InvariantCulture, $"{positie:D10} 00000 n \n");
        }

        pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objecten.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(pdf.ToString());
    }

    private async Task<Guid> ConceptAsync(bool boeken = false)
    {
        var pad = _map.Bestand("boekhouding.xlsx");
        if (!File.Exists(pad))
        {
            using var boek = new XLWorkbook();
            var blad = boek.Worksheets.Add("Voorblad");
            blad.Cell(1, 1).Value = 2025;
            blad.Cell(3, 2).Value = "Vermogen";
            blad.Cell(4, 1).Value = 600; blad.Cell(4, 2).Value = "Algemene Reserve"; blad.Cell(4, 10).Value = 100;
            blad.Cell(5, 2).Value = "Kas Bank";
            blad.Cell(6, 1).Value = 1100; blad.Cell(6, 2).Value = "Bank"; blad.Cell(6, 9).Value = 100;
            blad.Cell(7, 2).Value = "Kosten";
            blad.Cell(8, 1).Value = 4115; blad.Cell(8, 2).Value = "Energie";
            boek.SaveAs(pad);
            await _boekhouding.ImporteerAsync(Ver, VoorbladInlezer.Lees(new ClosedXmlWerkbladLezer().Lees(pad, VoorbladInlezer.Werkbladnaam)));
        }

        var stand = await _boekhouding.LaadAsync(Ver);
        var invoer = new PostInvoer(null, stand.Dagboeken.Single(d => d.Code == "MEM").Id, new DateOnly(2026, 10, 12), "Gamma",
        [
            new RegelInvoerId(stand.Rekeningen.Single(r => r.Nummer == 4115).Id, null, new Bedrag(2345), Bedrag.Nul),
            new RegelInvoerId(stand.Rekeningen.Single(r => r.Nummer == 1100).Id, null, Bedrag.Nul, new Bedrag(2345)),
        ]);
        var id = await _boekhouding.BewaarConceptAsync(Ver, invoer);
        if (boeken)
        {
            await _boekhouding.BoekAsync(Ver, invoer with { Id = id });
        }

        return id;
    }

    [Fact]
    public void PdfPig_leest_de_tekst_en_een_ongeldige_pdf_geeft_null()
    {
        var lezer = new PdfPigTekstLezer();
        var tekst = lezer.LeesTekst(TestPdf("Bouwmarkt Voorbeeld", "Datum 12-10-2026", "Te betalen 23,45"));

        Assert.NotNull(tekst);
        Assert.Contains("Datum 12-10-2026", tekst, StringComparison.Ordinal);
        Assert.Equal(new BonGegevens(new DateOnly(2026, 10, 12), new Bedrag(2345)), BonHerkenning.Herken(tekst));
        Assert.Null(lezer.LeesTekst(Encoding.ASCII.GetBytes("%PDF-1.4 kapot")));
    }

    [Fact]
    public async Task Bijlage_wordt_opgeslagen_zonder_inhoud_in_de_stand_en_de_inhoud_apart_geladen()
    {
        var pdf = TestPdf("Datum 12-10-2026", "Totaal 23,45");

        var resultaat = await _bijlagen.VoegToeAsync(Ver, [new BijlageBestand("bon.pdf", pdf)], postId: null);

        var stand = await _boekhouding.LaadAsync(Ver);
        var bijlage = Assert.Single(stand.Bijlagen);
        Assert.Equal(Assert.Single(resultaat.Toegevoegd).Id, bijlage.Id);
        Assert.True(bijlage.IsInPostvak);
        Assert.Equal(new DateOnly(2026, 10, 12), bijlage.Datum);
        Assert.Equal(new Bedrag(2345), bijlage.Bedrag);
        Assert.Equal(pdf, await _bijlagen.LaadInhoudAsync(Ver, bijlage.Id));
        Assert.Empty((await _boekhouding.LaadAsync(AdministratieSoort.Stichting)).Bijlagen);
    }

    [Fact]
    public async Task Koppelen_aan_een_geboekte_post_en_verwijderen_ruimt_de_inhoud_op()
    {
        var post = await ConceptAsync(boeken: true);
        var bijlage = Assert.Single((await _bijlagen.VoegToeAsync(Ver, [new BijlageBestand("bon.pdf", TestPdf("bon"))], post)).Toegevoegd);
        Assert.Equal(bijlage.Id, Assert.Single((await _boekhouding.LaadAsync(Ver)).BijlagenVan(post)).Id);

        await _bijlagen.VerwijderAsync(Ver, bijlage.Id);

        Assert.Empty((await _boekhouding.LaadAsync(Ver)).Bijlagen);
        Assert.Null(await _sessie.Boekhouding.LaadBijlageInhoudAsync(bijlage.Id));
    }

    [Fact]
    public async Task Een_verwijderd_concept_zet_zijn_bijlage_terug_in_het_postvak()
    {
        var post = await ConceptAsync();
        await _bijlagen.VoegToeAsync(Ver, [new BijlageBestand("bon.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 7])], post);

        await _boekhouding.VerwijderConceptAsync(Ver, post);

        Assert.True(Assert.Single((await _boekhouding.LaadAsync(Ver)).Bijlagen).IsInPostvak);
    }

    [Fact]
    public async Task Bijlagen_gaan_mee_in_de_backup()
    {
        var pdf = TestPdf("bewaren");
        var id = Assert.Single((await _bijlagen.VoegToeAsync(Ver, [new BijlageBestand("bon.pdf", pdf)], null)).Toegevoegd).Id;
        var backup = _map.Bestand("backup.vbak");
        await _sessie.MaakBackupAsync(backup);
        var hersteld = _map.Bestand("hersteld.vdb");

        await _beheer.HerstelBackupAsync(backup, hersteld);

        await using var sessie = await _beheer.OpenAsync(hersteld, Wachtwoord);
        Assert.Equal(pdf, await sessie.Boekhouding.LaadBijlageInhoudAsync(id));
    }
}
