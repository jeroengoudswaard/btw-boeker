using ClosedXML.Excel;
using BtwBoeker.Application.Boekhouding;

namespace BtwBoeker.Infrastructure.Excel;

/// <summary>Schrijft een rapport als .xlsx met euro-opmaak, vetgedrukte koppen en totalen.</summary>
public sealed class ClosedXmlSpreadsheetSchrijver : ISpreadsheetSchrijver
{
    public void Schrijf(RapportTabel tabel, string pad)
    {
        ArgumentNullException.ThrowIfNull(tabel);
        using var boek = new XLWorkbook();
        var blad = boek.Worksheets.Add(Bladnaam(tabel.Titel));
        blad.Cell(1, 1).Value = tabel.Titel;
        blad.Cell(1, 1).Style.Font.Bold = true;
        blad.Cell(1, 1).Style.Font.FontSize = 14;
        if (tabel.Subtitel is { } subtitel)
        {
            blad.Cell(2, 1).Value = subtitel;
        }

        const int kopRij = 4;
        for (var k = 0; k < tabel.Kolommen.Count; k++)
        {
            var cel = blad.Cell(kopRij, k + 1);
            cel.Value = tabel.Kolommen[k].Naam;
            cel.Style.Font.Bold = true;
            cel.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        for (var r = 0; r < tabel.Rijen.Count; r++)
        {
            var rij = tabel.Rijen[r];
            for (var k = 0; k < rij.Cellen.Count; k++)
            {
                var cel = blad.Cell(kopRij + 1 + r, k + 1);
                switch (rij.Cellen[k])
                {
                    case Domain.Financien.Bedrag bedrag:
                        cel.Value = bedrag.Centen / 100m;
                        cel.Style.NumberFormat.Format = "#,##0.00";
                        break;
                    case { } waarde:
                        cel.Value = Convert.ToString(waarde, System.Globalization.CultureInfo.InvariantCulture);
                        break;
                }

                cel.Style.Font.Bold = rij.IsKop || rij.IsTotaal;
                if (rij.IsTotaal)
                {
                    cel.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                }
            }
        }

        blad.Columns().AdjustToContents();
        boek.SaveAs(pad);
    }

    private static string Bladnaam(string titel)
    {
        var schoon = new string(titel.Where(t => t is not ('\\' or '/' or '?' or '*' or '[' or ']' or ':')).ToArray());
        return schoon.Length > 31 ? schoon[..31] : schoon;
    }
}
