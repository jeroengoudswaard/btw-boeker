using System.Globalization;
using System.Text;

namespace Verenigd.Tests.Gedeeld;

/// <summary>Maakt kleine, verzonnen PDF-bestanden voor tests (gedeeld met de Windows-tests van de app).</summary>
public static class Testpdf
{
    /// <summary>Een kleine, geldige PDF met tekst in Helvetica (zonder ingesloten lettertype).</summary>
    public static byte[] Maak(params string[] regels)
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
}
