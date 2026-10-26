namespace Verenigd.App.Diensten;

/// <summary>
/// Locaties van instellingen en logbestanden. Met de omgevingsvariabele <c>VERENIGD_DATAMAP</c>
/// kan alles naar één map worden omgeleid (gebruikt door de UI-tests).
/// </summary>
internal static class Paden
{
    private static readonly string? Omleiding = Environment.GetEnvironmentVariable("VERENIGD_DATAMAP");

    public static string Instellingenbestand => Path.Combine(
        Omleiding ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Verenigd"),
        "instellingen.json");

    public static string Logmap => Omleiding is not null
        ? Path.Combine(Omleiding, "logs")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Verenigd", "logs");
}
