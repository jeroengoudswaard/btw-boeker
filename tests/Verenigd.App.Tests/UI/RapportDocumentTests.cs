using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Verenigd.App.Diensten;
using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Financien;

namespace Verenigd.App.Tests.UI;

/// <summary>De layout van een rapport op het scherm (alleen op Windows, WPF). Een getoond venster is nodig voor de layout.</summary>
public sealed class RapportDocumentTests
{
    private static RapportTabel Saldibalans() => new("Saldibalans 2026", "Vereniging",
        [new("Rekening", false), new("Debet", true), new("Credit", true), new("Saldo debet", true), new("Saldo credit", true)],
        [new RapportRij(["600 Algemene reserve", Bedrag.Nul, new Bedrag(1582164), null, new Bedrag(1582164)], false, false)]);

    [Fact]
    public void Rekeningnaam_in_de_saldibalans_staat_op_een_regel_en_de_tekstkolom_is_het_breedst()
    {
        Exception? fout = null;
        var draad = new Thread(() =>
        {
            var venster = new Window { WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000, Width = 900, Height = 400 };
            try
            {
                var document = WpfAfdrukDienst.MaakDocument(Saldibalans());
                venster.Content = new FlowDocumentScrollViewer { Document = document, IsToolBarVisible = false };
                venster.Show();
                venster.UpdateLayout();

                var tabel = document.Blocks.OfType<Table>().Single();
                var rij = tabel.RowGroups[0].Rows[1];
                var naam = (Paragraph)rij.Cells[0].Blocks.FirstBlock;
                var begin = naam.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                var eind = naam.ContentEnd.GetCharacterRect(LogicalDirection.Backward);
                Assert.Equal(begin.Top, eind.Top, 1); // één regel, niet één letter per regel
                Assert.True(eind.Right - begin.Left > 100, $"Rekeningnaam is {eind.Right - begin.Left:0} breed.");

                double Links(int kolom) => ((Paragraph)rij.Cells[kolom].Blocks.FirstBlock).ContentStart.GetCharacterRect(LogicalDirection.Forward).Left;
                Assert.True(Links(1) > 300, $"De eerste bedragkolom begint al op {Links(1):0}.");
            }
            catch (Exception ex)
            {
                fout = ex;
            }
            finally
            {
                venster.Close();
                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        draad.SetApartmentState(ApartmentState.STA);
        draad.Start();
        draad.Join();
        if (fout is not null)
        {
            throw new InvalidOperationException("Fout op de STA-thread.", fout);
        }
    }
}
