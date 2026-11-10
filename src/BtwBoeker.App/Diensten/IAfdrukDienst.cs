using BtwBoeker.Application.Boekhouding;

namespace BtwBoeker.App.Diensten;

/// <summary>Afdrukken via het standaard Windows-afdrukvenster, dus ook naar PDF ("Microsoft Print to PDF").</summary>
public interface IAfdrukDienst
{
    /// <returns><c>false</c> als de gebruiker het afdrukken annuleert.</returns>
    bool Druk(RapportTabel tabel);
}

/// <summary>Wat de pagina's Boekhouding nodig hebben van buiten de ViewModels.</summary>
public sealed record BoekhoudingHulpmiddelen(
    ISpreadsheetSchrijver Schrijver, IAfdrukDienst Afdruk, ViewModels.Woordenboek? Woordenboek = null,
    Application.Bijlagen.IPdfTekstLezer? Tekstlezer = null)
{
    /// <summary>Leest datum en bedrag uit PDF-bonnen; zonder lezer vult de gebruiker ze zelf in.</summary>
    public Application.Bijlagen.IPdfTekstLezer PdfTekst { get; } = Tekstlezer ?? new GeenTekst();

    /// <summary>De woorden van de gekozen weergave, gedeeld door de hele app.</summary>
    public ViewModels.Woordenboek Woorden { get; } = Woordenboek ?? new ViewModels.Woordenboek();
}

internal sealed class GeenTekst : Application.Bijlagen.IPdfTekstLezer
{
    public string? LeesTekst(byte[] pdf) => null;
}
