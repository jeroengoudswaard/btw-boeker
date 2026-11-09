using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage.Streams;

namespace BtwBoeker.App.Diensten;

/// <summary>
/// Pagina's van een PDF of een afbeelding als WPF-afbeelding, met de PDF-functie van Windows zelf (besluit 135). Alles in
/// het geheugen: de bijlage komt niet onversleuteld op schijf.
/// </summary>
public static class PdfPaginas
{
    /// <summary>Bonnen en facturen zijn kort; een heel lang document wordt afgekapt.</summary>
    public const int MaximaalAantalPaginas = 50;

    /// <summary>Breedte in pixels bij 100 % (ongeveer A4 op 96 dpi, scherp genoeg om in te zoomen).</summary>
    private const uint Breedte = 1200;

    public static async Task<IReadOnlyList<ImageSource>> PdfAsync(byte[] pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        using var bron = new InMemoryRandomAccessStream();
        await bron.WriteAsync(pdf.AsBuffer());
        bron.Seek(0);
        var document = await PdfDocument.LoadFromStreamAsync(bron);
        var paginas = new List<ImageSource>();
        for (uint i = 0; i < Math.Min(document.PageCount, MaximaalAantalPaginas); i++)
        {
            using var pagina = document.GetPage(i);
            using var uit = new InMemoryRandomAccessStream();
            await pagina.RenderToStreamAsync(uit, new PdfPageRenderOptions { DestinationWidth = Breedte });
            var bytes = new byte[uit.Size];
            uit.Seek(0);
            await uit.ReadAsync(bytes.AsBuffer(), (uint)bytes.Length, InputStreamOptions.None);
            paginas.Add(Afbeelding(bytes));
        }

        return paginas;
    }

    public static ImageSource Afbeelding(byte[] inhoud)
    {
        ArgumentNullException.ThrowIfNull(inhoud);
        using var stroom = new MemoryStream(inhoud);
        var afbeelding = new BitmapImage();
        afbeelding.BeginInit();
        afbeelding.CacheOption = BitmapCacheOption.OnLoad;
        afbeelding.StreamSource = stroom;
        afbeelding.EndInit();
        afbeelding.Freeze();
        return afbeelding;
    }
}
