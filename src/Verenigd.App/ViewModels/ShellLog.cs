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

    [LoggerMessage(8, LogLevel.Warning, "{Handeling} mislukt")]
    public static partial void Mislukt(ILogger logger, Exception fout, string handeling);
}
