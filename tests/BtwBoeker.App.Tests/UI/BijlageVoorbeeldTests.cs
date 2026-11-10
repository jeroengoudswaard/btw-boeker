using BtwBoeker.App.Diensten;
using BtwBoeker.Tests.Gedeeld;

namespace BtwBoeker.App.Tests.UI;

/// <summary>Het voorbeeld van een PDF via Windows zelf.</summary>
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

    /// <summary>
    /// Zonder <c>BijlageVenster</c>: dat heeft de app-resources nodig (bijv. <c>Pictogrammen</c>) en wordt daarom end-to-end
    /// getest in <see cref="AppUiTests"/>.
    /// </summary>
    [Fact]
    public void Een_afbeelding_wordt_als_een_pagina_geladen()
    {
        // 1x1 PNG
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        var afbeelding = PdfPaginas.Afbeelding(png);

        Assert.True(afbeelding.IsFrozen);
        Assert.Equal(1, afbeelding.Width, 1);
    }
}
