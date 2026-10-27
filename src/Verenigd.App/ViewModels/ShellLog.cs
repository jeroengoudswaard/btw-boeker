using Microsoft.Extensions.Logging;

namespace Verenigd.App.ViewModels;

/// <summary>
/// Gegenereerde, gestructureerde logberichten. Alleen bestandsnamen, nooit volledige paden of persoonsgegevens.
/// </summary>
internal static partial class ShellLog
{
    [LoggerMessage(1, LogLevel.Information, "Nieuwe database aangemaakt: {Bestand}")]
    public static partial void DatabaseAangemaakt(ILogger logger, string bestand);

    [LoggerMessage(2, LogLevel.Information, "Database geopend: {Bestand}")]
    public static partial void DatabaseGeopend(ILogger logger, string bestand);

    [LoggerMessage(3, LogLevel.Information, "Database gesloten")]
    public static partial void DatabaseGesloten(ILogger logger);

    [LoggerMessage(4, LogLevel.Information, "Openen geweigerd: onjuiste sleutel")]
    public static partial void OnjuisteSleutel(ILogger logger);

    [LoggerMessage(5, LogLevel.Information, "Database geopend met herstelsleutel; wachtwoord vervangen")]
    public static partial void HerstelsleutelGebruikt(ILogger logger);

    [LoggerMessage(6, LogLevel.Information, "Back-up gemaakt")]
    public static partial void BackupGemaakt(ILogger logger);

    [LoggerMessage(7, LogLevel.Information, "Back-up teruggezet naar {Bestand}")]
    public static partial void BackupTeruggezet(ILogger logger, string bestand);

    [LoggerMessage(9, LogLevel.Information, "SOL3-import uitgevoerd: {Nieuw} nieuw, {Gewijzigd} gewijzigd, {Uitgeschreven} uitgeschreven")]
    public static partial void Sol3Geimporteerd(ILogger logger, int nieuw, int gewijzigd, int uitgeschreven);

    [LoggerMessage(10, LogLevel.Information, "Incassorun {Maand} definitief: {Aantal} posten, totaal {Totaal}")]
    public static partial void IncassorunDefinitief(ILogger logger, string maand, int aantal, string totaal);

    [LoggerMessage(11, LogLevel.Information, "Incassorun {Maand} geannuleerd")]
    public static partial void IncassorunGeannuleerd(ILogger logger, string maand);

    [LoggerMessage(12, LogLevel.Information, "Boekhouding {Administratie} overgenomen uit Excel: {Aantal} rekeningen")]
    public static partial void BoekhoudingOvergenomen(ILogger logger, string administratie, int aantal);

    [LoggerMessage(13, LogLevel.Warning, "Boekhouding {Administratie} overgenomen met een bewust geboekt verschil van {Verschil} op rekening {Rekening}")]
    public static partial void BoekhoudingVerschilGeboekt(ILogger logger, string administratie, string verschil, string rekening);

    [LoggerMessage(14, LogLevel.Information, "Boekjaar {Jaar} van {Administratie} {Handeling}")]
    public static partial void BoekjaarGewijzigd(ILogger logger, int jaar, string administratie, string handeling);

    [LoggerMessage(15, LogLevel.Information, "Bankafschriften {Administratie} ingelezen: {Nieuw} nieuw, {Herkend} herkend, {Werkvoorraad} in de werkvoorraad")]
    public static partial void BankafschriftIngelezen(ILogger logger, string administratie, int nieuw, int herkend, int werkvoorraad);

    [LoggerMessage(8, LogLevel.Warning, "{Handeling} mislukt")]
    public static partial void Mislukt(ILogger logger, Exception fout, string handeling);
}
