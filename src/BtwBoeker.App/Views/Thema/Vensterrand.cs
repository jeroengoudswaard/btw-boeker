namespace BtwBoeker.App.Views.Thema;

/// <summary>
/// UX-review stap 1 (besluit 32, uit het moederproduct): de keuzes voor de vensterrand, zonder Windows-aanroepen
/// (testbaar). Windows 11 tekent afgeronde hoeken; Windows 10 alleen de schaduw. De randkleur volgt Windows (besluit 26).
/// </summary>
public static class Vensterrand
{
    /// <summary>Windows 11 begint bij build 22000.</summary>
    public static bool IsWindows11(Version versie)
    {
        ArgumentNullException.ThrowIfNull(versie);
        return versie.Major >= 10 && versie.Build >= 22000;
    }
}
