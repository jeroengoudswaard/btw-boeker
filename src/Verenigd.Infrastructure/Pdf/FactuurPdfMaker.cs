using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using QRCoder;
using Verenigd.Application.Facturen;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Facturen;

namespace Verenigd.Infrastructure.Pdf;

/// <summary>
/// De PDF van een verkoopfactuur met PDFsharp/MigraDoc (besluit 95): logo, afzender, ontvanger, regels, "geen btw"
/// (besluit 94), betaalinstructie en een EPC-QR-code (besluit 110). Een creditfactuur of een factuur zonder IBAN krijgt
/// geen QR-code.
/// </summary>
public sealed class FactuurPdfMaker : IFactuurPdfMaker
{
    private const string Lettertype = "Segoe UI";
    private static readonly CultureInfo Nederlands = CultureInfo.GetCultureInfo("nl-NL");

    static FactuurPdfMaker()
    {
        GlobalFontSettings.FontResolver ??= new Lettertypen();
    }

    public byte[] Maak(Verkoopfactuur factuur, Factuurinstellingen instellingen)
    {
        ArgumentNullException.ThrowIfNull(factuur);
        ArgumentNullException.ThrowIfNull(instellingen);
        var document = new Document();
        document.Info.Title = $"{Soort(factuur)} {factuur.Nummer ?? "(concept)"}";
        document.Info.Author = instellingen.Naam ?? string.Empty;
        var stijl = document.Styles[StyleNames.Normal]!;
        stijl.Font.Name = Lettertype;
        stijl.Font.Size = 10;

        var sectie = document.AddSection();
        sectie.PageSetup = document.DefaultPageSetup.Clone();
        sectie.PageSetup.PageFormat = PageFormat.A4;
        sectie.PageSetup.TopMargin = Unit.FromCentimeter(2);
        sectie.PageSetup.LeftMargin = sectie.PageSetup.RightMargin = Unit.FromCentimeter(2.2);

        Kop(sectie, factuur, instellingen);
        Regels(sectie, factuur);
        Betaling(sectie, factuur, instellingen);
        if (instellingen.Voettekst is { } voet)
        {
            var alinea = sectie.Footers.Primary.AddParagraph(voet);
            alinea.Format.Font.Size = 8;
            alinea.Format.Alignment = ParagraphAlignment.Center;
        }

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stroom = new MemoryStream();
        renderer.PdfDocument.Save(stroom, false);
        return stroom.ToArray();
    }

    private static string Soort(Verkoopfactuur factuur) => factuur.IsCreditfactuur ? "Creditfactuur" : "Factuur";

    private static void Kop(Section sectie, Verkoopfactuur factuur, Factuurinstellingen instellingen)
    {
        var kop = sectie.AddTable();
        kop.AddColumn(Unit.FromCentimeter(9.6));
        kop.AddColumn(Unit.FromCentimeter(7));
        var rij = kop.AddRow();
        if (instellingen.Logo is { } logo)
        {
            var afbeelding = rij.Cells[0].AddImage("base64:" + Convert.ToBase64String(logo));
            afbeelding.LockAspectRatio = true;
            afbeelding.Height = Unit.FromCentimeter(2.5);
        }

        var afzender = rij.Cells[1].AddParagraph();
        afzender.Format.Alignment = ParagraphAlignment.Right;
        afzender.AddFormattedText(instellingen.Naam ?? string.Empty, TextFormat.Bold);
        foreach (var regel in new[]
                 {
                     instellingen.Straat, Samen(instellingen.Postcode, instellingen.Plaats), instellingen.Email, instellingen.Telefoon,
                     instellingen.Kvk is { } kvk ? "KvK " + kvk : null, instellingen.Iban is { } iban ? "IBAN " + Iban.Maak(iban).ToString() : null,
                 }.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            afzender.AddLineBreak();
            afzender.AddText(regel!);
        }

        var ontvanger = sectie.AddParagraph();
        ontvanger.Format.SpaceBefore = Unit.FromCentimeter(1.5);
        ontvanger.Format.SpaceAfter = Unit.FromCentimeter(1);
        var regels = (factuur.Ontvanger ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < regels.Length; i++)
        {
            if (i > 0)
            {
                ontvanger.AddLineBreak();
            }

            ontvanger.AddText(regels[i]);
        }

        var titel = sectie.AddParagraph(Soort(factuur));
        titel.Format.Font.Size = 18;
        titel.Format.Font.Bold = true;
        titel.Format.SpaceAfter = Unit.FromPoint(6);

        var gegevens = sectie.AddTable();
        gegevens.AddColumn(Unit.FromCentimeter(4));
        gegevens.AddColumn(Unit.FromCentimeter(6));
        Gegeven(gegevens, "Factuurnummer", factuur.Nummer ?? "Concept");
        Gegeven(gegevens, "Factuurdatum", Datum(factuur.Factuurdatum));
        if (!factuur.IsCreditfactuur)
        {
            Gegeven(gegevens, "Vervaldatum", Datum(factuur.Vervaldatum));
        }
    }

    private static void Gegeven(Table tabel, string label, string waarde)
    {
        var rij = tabel.AddRow();
        rij.Cells[0].AddParagraph(label);
        rij.Cells[1].AddParagraph(waarde);
    }

    private static void Regels(Section sectie, Verkoopfactuur factuur)
    {
        sectie.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(0.6);
        var tabel = sectie.AddTable();
        tabel.Borders.Bottom.Width = 0.5;
        tabel.AddColumn(Unit.FromCentimeter(9));
        tabel.AddColumn(Unit.FromCentimeter(2)).Format.Alignment = ParagraphAlignment.Right;
        tabel.AddColumn(Unit.FromCentimeter(2.8)).Format.Alignment = ParagraphAlignment.Right;
        tabel.AddColumn(Unit.FromCentimeter(2.8)).Format.Alignment = ParagraphAlignment.Right;
        var kop = tabel.AddRow();
        kop.HeadingFormat = true;
        kop.Format.Font.Bold = true;
        kop.Borders.Bottom.Width = 0.75;
        kop.Cells[0].AddParagraph("Omschrijving");
        kop.Cells[1].AddParagraph("Aantal");
        kop.Cells[2].AddParagraph("Prijs");
        kop.Cells[3].AddParagraph("Bedrag");
        foreach (var regel in factuur.Regels)
        {
            var rij = tabel.AddRow();
            rij.TopPadding = rij.BottomPadding = Unit.FromPoint(2);
            rij.Cells[0].AddParagraph(regel.Omschrijving);
            rij.Cells[1].AddParagraph(regel.Aantal.ToString("0.##", Nederlands));
            rij.Cells[2].AddParagraph(regel.Prijs.ToString());
            rij.Cells[3].AddParagraph(regel.Bedrag.ToString());
        }

        var totaal = tabel.AddRow();
        totaal.Format.Font.Bold = true;
        totaal.TopPadding = Unit.FromPoint(4);
        totaal.Cells[0].AddParagraph("Totaal");
        totaal.Cells[3].AddParagraph(factuur.Totaal.ToString());

        var btw = sectie.AddParagraph("Geen btw: de vereniging is niet btw-plichtig.");
        btw.Format.SpaceBefore = Unit.FromPoint(6);
        btw.Format.Font.Size = 8;
    }

    private static void Betaling(Section sectie, Verkoopfactuur factuur, Factuurinstellingen instellingen)
    {
        var alinea = sectie.AddParagraph();
        alinea.Format.SpaceBefore = Unit.FromCentimeter(1);
        if (factuur.IsCreditfactuur)
        {
            alinea.AddText("Deze creditfactuur vervalt de oorspronkelijke factuur voor het genoemde bedrag.");
            return;
        }

        if (instellingen.Iban is not { } tekst || !Iban.TryMaak(tekst, out var iban) || factuur.Totaal.Centen <= 0)
        {
            alinea.AddText($"Graag betalen vóór {Datum(factuur.Vervaldatum)} onder vermelding van {factuur.Nummer ?? "het factuurnummer"}.");
            return;
        }

        var kenmerk = factuur.Nummer ?? "Concept";
        alinea.AddText($"Graag {factuur.Totaal} overmaken vóór {Datum(factuur.Vervaldatum)} op {iban} " +
            $"t.n.v. {instellingen.Naam}, onder vermelding van {kenmerk}.");
        var inhoud = EpcQr.Inhoud(instellingen.Naam ?? string.Empty, iban, factuur.Totaal, kenmerk);
        using var data = new QRCodeGenerator().CreateQrCode(inhoud, QRCodeGenerator.ECCLevel.M);
        var png = QrPng(data);
        var qr = sectie.AddParagraph();
        qr.Format.SpaceBefore = Unit.FromCentimeter(0.5);
        var afbeelding = qr.AddImage("base64:" + Convert.ToBase64String(png));
        afbeelding.Width = afbeelding.Height = Unit.FromCentimeter(3.5);
        var uitleg = sectie.AddParagraph("Scan de code met uw bankapp om te betalen.");
        uitleg.Format.Font.Size = 8;
    }

    /// <summary>
    /// De QR-code als 8-bits grijswaarden-PNG met een stille zone van vier modules; de 1-bits PNG van QRCoder kan PDFsharp
    /// niet lezen.
    /// </summary>
    internal static byte[] QrPng(QRCodeData data, int schaal = 8)
    {
        var matrix = data.ModuleMatrix;
        var zijde = matrix.Count * schaal;
        using var pixels = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(pixels, System.IO.Compression.CompressionLevel.Optimal, true))
        {
            var rij = new byte[zijde + 1];
            for (var y = 0; y < zijde; y++)
            {
                var modules = matrix[y / schaal];
                for (var x = 0; x < zijde; x++)
                {
                    rij[x + 1] = modules[x / schaal] ? (byte)0 : (byte)255;
                }

                zlib.Write(rij);
            }
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var kop = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(kop, zijde);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(kop.AsSpan(4), zijde);
        kop[8] = 8; // bitdiepte; kleurtype 0 = grijswaarden
        Blok(png, "IHDR", kop);
        Blok(png, "IDAT", pixels.ToArray());
        Blok(png, "IEND", []);
        return png.ToArray();
    }

    private static void Blok(Stream doel, string soort, byte[] inhoud)
    {
        Span<byte> getal = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(getal, inhoud.Length);
        doel.Write(getal);
        var soortBytes = System.Text.Encoding.ASCII.GetBytes(soort);
        doel.Write(soortBytes);
        doel.Write(inhoud);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(getal, ~Crc(Crc(uint.MaxValue, soortBytes), inhoud));
        doel.Write(getal);
    }

    /// <summary>CRC-32 zoals PNG die vraagt (polynoom 0xEDB88320).</summary>
    private static uint Crc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc;
    }

    private static string Datum(DateOnly datum) => datum.ToString("d-M-yyyy", Nederlands);

    private static string? Samen(string? postcode, string? plaats) =>
        string.Join("  ", new[] { postcode, plaats }.Where(t => !string.IsNullOrWhiteSpace(t))) is { Length: > 0 } t ? t : null;

    /// <summary>
    /// Zoekt Segoe UI (Windows) of een vrij alternatief (Linux); PDFsharp heeft buiten Windows geen eigen lettertypen.
    /// </summary>
    private sealed class Lettertypen : IFontResolver
    {
        private static readonly string[][] Kandidaten =
        [
            [@"C:\Windows\Fonts\segoeui.ttf", @"C:\Windows\Fonts\arial.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"],
            [@"C:\Windows\Fonts\segoeuib.ttf", @"C:\Windows\Fonts\arialbd.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"],
            [@"C:\Windows\Fonts\segoeuii.ttf", @"C:\Windows\Fonts\ariali.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Italic.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Oblique.ttf"],
            [@"C:\Windows\Fonts\segoeuiz.ttf", @"C:\Windows\Fonts\arialbi.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-BoldItalic.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-BoldOblique.ttf"],
        ];

        public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) =>
            new($"verenigd-{(bold ? 1 : 0) + (italic ? 2 : 0)}");

        public byte[] GetFont(string faceName)
        {
            var index = int.Parse(faceName[(faceName.LastIndexOf('-') + 1)..], CultureInfo.InvariantCulture);
            var pad = Kandidaten[index].FirstOrDefault(File.Exists) ?? Kandidaten[0].FirstOrDefault(File.Exists)
                ?? throw new InvalidOperationException("Geen bruikbaar lettertype gevonden voor de PDF.");
            return File.ReadAllBytes(pad);
        }
    }
}
