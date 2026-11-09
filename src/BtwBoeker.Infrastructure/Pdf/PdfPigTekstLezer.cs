using System.Text;
using UglyToad.PdfPig;
using BtwBoeker.Application.Bijlagen;

namespace BtwBoeker.Infrastructure.Pdf;

/// <summary>Leest de tekst van een PDF met PdfPig (besluit 135), regel voor regel en volledig in het geheugen.</summary>
public sealed class PdfPigTekstLezer : IPdfTekstLezer
{
    /// <summary>Bonnen en facturen zijn kort; meer pagina's lezen is niet nodig.</summary>
    private const int MaximaalAantalPaginas = 5;

    public string? LeesTekst(byte[] pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        try
        {
            using var document = PdfDocument.Open(pdf);
            var tekst = new StringBuilder();
            foreach (var pagina in document.GetPages().Take(MaximaalAantalPaginas))
            {
                // Woorden per regel (zelfde hoogte), van boven naar beneden, zodat "Totaal" en het bedrag op één regel staan.
                foreach (var regel in pagina.GetWords().GroupBy(w => Math.Round(w.BoundingBox.Bottom)).OrderByDescending(g => g.Key))
                {
                    tekst.AppendLine(string.Join(' ', regel.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));
                }
            }

            return tekst.ToString();
        }
#pragma warning disable CA1031 // Een beschadigde of versleutelde PDF is geen fout: de gebruiker vult datum en bedrag zelf in.
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    }
}
