namespace BtwBoeker.Architectuur.Tests;

/// <summary>
/// Bewaakt de reikwijdte van BTW-Boeker (besluiten 6 en 28): onderdelen van het oude product die niet bij één administratie
/// voor de verhuur horen, komen niet terug in de broncode. Migraties tellen niet mee: die beschrijven de historie
/// van het databaseschema.
/// </summary>
public class ReikwijdteTests
{
    private static readonly string[] Genegeerdemappen = ["bin", "obj", "Migraties"];

    private static string Repositorymap()
    {
        var map = new DirectoryInfo(AppContext.BaseDirectory);
        while (map is not null && !File.Exists(Path.Combine(map.FullName, "BtwBoeker.slnx")))
        {
            map = map.Parent;
        }

        return map?.FullName ?? throw new InvalidOperationException("De hoofdmap van de repository is niet gevonden.");
    }

    private static List<string> BestandenMet(params string[] woorden)
    {
        var wortel = Path.Combine(Repositorymap(), "src");
        return Directory.EnumerateFiles(wortel, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Where(f => !Path.GetRelativePath(wortel, f).Split(Path.DirectorySeparatorChar)
                .Any(d => Genegeerdemappen.Contains(d, StringComparer.OrdinalIgnoreCase)))
            .Where(f => woorden.Any(w => Path.GetFileName(f).Contains(w, StringComparison.OrdinalIgnoreCase)
                || File.ReadAllText(f).Contains(w, StringComparison.OrdinalIgnoreCase)))
            .Select(f => Path.GetRelativePath(wortel, f))
            .ToList();
    }

    [Fact]
    public void Begroting_en_meerjarenplan_bestaan_niet()
    {
        Assert.Empty(BestandenMet("Begroting", "Begroot", "Meerjaren"));
    }

    [Fact]
    public void Excel_overname_en_historisch_boekjaar_bestaan_niet()
    {
        Assert.Empty(BestandenMet("Historie", "Historisch", "Voorblad", "BoekhoudingImport", "WerkbladLezer"));
    }

    [Fact]
    public void Ingebouwde_standaardschemas_bestaan_niet()
    {
        // De woorden van het oude domein zelf bewaakt ProductnaamTests in alle bestanden.
        Assert.Empty(BestandenMet("Standaardschema"));
    }

    [Fact]
    public void Er_is_geen_tweede_administratie_en_geen_wisselaar()
    {
        Assert.Empty(BestandenMet("Wisselaar"));
    }

    /// <summary>
    /// De app toont één administratie (besluit 26): geen keuze per module, geen accentkleur per administratie en geen
    /// administratie in het navigatieverzoek. Het datamodel per administratie blijft.
    /// </summary>
    [Fact]
    public void De_app_kent_geen_navigatie_per_administratie()
    {
        Assert.Empty(BestandenMet(
            "PerAdministratie", "AlleenVoor", "KiesAdministratie", "GekozenAdministratie", "ActieveAdministratie",
            "WijzigAccentkleur", ".Accentkleur", "Accentkleuren.", "ModuleVerzoek", "ModuleInOntwikkeling", "Tabblad" + "ViewModel", "Geselecteerd" + "Tabblad"));
    }
}
