using Verenigd.Application.Boekhouding;

namespace Verenigd.App.Diensten;

/// <summary>Afdrukken via het standaard Windows-afdrukvenster, dus ook naar PDF ("Microsoft Print to PDF", besluit 67).</summary>
public interface IAfdrukDienst
{
    /// <returns><c>false</c> als de gebruiker het afdrukken annuleert.</returns>
    bool Druk(RapportTabel tabel);
}

/// <summary>Wat het tabblad Boekhouding nodig heeft van buiten de ViewModels.</summary>
public sealed record BoekhoudingHulpmiddelen(IWerkbladLezer Lezer, ISpreadsheetSchrijver Schrijver, IAfdrukDienst Afdruk);
