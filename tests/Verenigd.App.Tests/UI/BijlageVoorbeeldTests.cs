using System.Windows;
using Verenigd.App.Diensten;
using Verenigd.App.ViewModels.Boekhouding;
using Verenigd.App.Views.Boekhouding;
using Verenigd.Domain.Financien.Bijlagen;
using Verenigd.Tests.Gedeeld;

namespace Verenigd.App.Tests.UI;

/// <summary>Het voorbeeld van een PDF via Windows zelf (besluit 135; alleen op Windows).</summary>
public sealed class BijlageVoorbeeldTests
{
    [Fact]
    public void Een_pdf_wordt_als_pagina_getoond()
    {
        Exception? fout = null;
        var draad = new Thread(() =>
        {
            try
            {
                var pdf = Testpdf.Maak("Bouwmarkt Voorbeeld", "Datum 12-10-2026", "Te betalen 23,45");
                var paginas = PdfPaginas.PdfAsync(pdf).GetAwaiter().GetResult();

                var pagina = Assert.Single(paginas);
                Assert.True(pagina.Width > 100 && pagina.Height > pagina.Width, $"Pagina is {pagina.Width:0} x {pagina.Height:0}.");
            }
            catch (Exception ex)
            {
                fout = ex;
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

    [Fact]
    public void Het_voorbeeldvenster_laadt_een_afbeelding_zonder_fout()
    {
        Exception? fout = null;
        var draad = new Thread(() =>
        {
            try
            {
                // 1x1 PNG
                byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
                var vm = new BijlageVoorbeeldViewModel("bon.png", BijlageSoort.Png, png, new FakeDialoogService());
                var venster = new BijlageVenster(vm) { WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000 };
                venster.LaadPaginasAsync().GetAwaiter().GetResult();

                Assert.Single(venster.Paginas);
                Assert.Null(vm.Foutmelding);
                venster.Close();
            }
            catch (Exception ex)
            {
                fout = ex;
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
