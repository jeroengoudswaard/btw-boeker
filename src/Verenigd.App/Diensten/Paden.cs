namespace Verenigd.App.Diensten;

/// <summary>
/// Locaties van instellingen en logbestanden. Met de omgevingsvariabele <c>BTW_BOEKER_DATAMAP</c>
/// kan alles naar één map worden omgeleid (gebruikt door de UI-tests).
/// </summary>
internal static class Paden
{
    private const string Applicatiemap = "BTW-Boeker";

    private static string? Omleiding => Environment.GetEnvironmentVariable("BTW_BOEKER_DATAMAP");

    public static string Instellingenbestand => Path.Combine(
        Omleiding ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Applicatiemap),
        "instellingen.json");

    public static string Logmap => Omleiding is not null
        ? Path.Combine(Omleiding, "logs")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Applicatiemap, "logs");
}
