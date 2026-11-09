using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Financien;

namespace BtwBoeker.App.Diensten;

/// <summary>Drukt een rapport af als FlowDocument via het standaard Windows-afdrukvenster.</summary>
internal sealed class WpfAfdrukDienst : IAfdrukDienst
{
    public bool Druk(RapportTabel tabel)
    {
        ArgumentNullException.ThrowIfNull(tabel);
        var dialoog = new PrintDialog();
        if (tabel.IsLiggend)
        {
            // Besluit 165: brede tabellen (meerjarenplan) liggend; de gebruiker kan dat in het afdrukvenster nog wijzigen.
            dialoog.PrintTicket.PageOrientation = System.Printing.PageOrientation.Landscape;
        }

        if (dialoog.ShowDialog() != true)
        {
            return false;
        }

        var document = MaakDocument(tabel);
        document.PageHeight = dialoog.PrintableAreaHeight;
        document.PageWidth = dialoog.PrintableAreaWidth;
        document.ColumnWidth = dialoog.PrintableAreaWidth;
        dialoog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, tabel.Titel);
        return true;
    }

    internal static FlowDocument MaakDocument(RapportTabel tabel)
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = tabel.IsLiggend ? 8.5 : 11,
            PagePadding = new Thickness(tabel.IsLiggend ? 32 : 48),
        };
        document.Blocks.Add(new Paragraph(new Run(tabel.Titel)) { FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        if (tabel.Subtitel is { } subtitel)
        {
            document.Blocks.Add(new Paragraph(new Run(subtitel)) { Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 12) });
        }

        var tabelBlok = new Table { CellSpacing = 0 };
        foreach (var kolom in tabel.Kolommen)
        {
            // Alleen verhoudingen: de FlowDocumentScrollViewer leest een vaste breedte (110) als verhouding, waardoor de
            // tekstkolom één letter breed werd. Tekst 3 delen, elk bedrag 1 deel.
            tabelBlok.Columns.Add(new TableColumn { Width = new GridLength(kolom.IsBedrag ? 1 : 3, GridUnitType.Star) });
        }

        var groep = new TableRowGroup();
        groep.Rows.Add(Rij(tabel.Kolommen.Select(k => (object?)k.Naam).ToList(), tabel, vet: true, lijnOnder: true));
        foreach (var rij in tabel.Rijen)
        {
            groep.Rows.Add(Rij(rij.Cellen, tabel, vet: rij.IsKop || rij.IsTotaal, lijnBoven: rij.IsTotaal));
        }

        tabelBlok.RowGroups.Add(groep);
        document.Blocks.Add(tabelBlok);
        return document;
    }

    private static TableRow Rij(IReadOnlyList<object?> cellen, RapportTabel tabel, bool vet, bool lijnOnder = false, bool lijnBoven = false)
    {
        var rij = new TableRow();
        for (var k = 0; k < tabel.Kolommen.Count; k++)
        {
            var waarde = k < cellen.Count ? cellen[k] : null;
            var tekst = waarde is Bedrag bedrag ? bedrag.ToString() : Convert.ToString(waarde, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
            var cel = new TableCell(new Paragraph(new Run(tekst))
            {
                TextAlignment = tabel.Kolommen[k].IsBedrag ? TextAlignment.Right : TextAlignment.Left,
                FontWeight = vet ? FontWeights.SemiBold : FontWeights.Normal,
                Margin = new Thickness(0),
            })
            {
                Padding = new Thickness(4, 2, 4, 2),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0, lijnBoven ? 0.5 : 0, 0, lijnOnder ? 0.5 : 0),
            };
            rij.Cells.Add(cel);
        }

        return rij;
    }
}
