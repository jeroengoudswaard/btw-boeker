namespace Verenigd.Architectuur.Tests;

/// <summary>
/// Bewaakt de productnaam: BTW-Boeker is een eigen product en draagt geen resten van de oude naam,
/// en de bestandsextensies zijn die van BTW-Boeker.
/// </summary>
public class ProductnaamTests
{
    // Bewust samengesteld, zodat dit bestand de oude naam zelf niet bevat.
    private static readonly string OudeNaam = "Veren" + "igd";

    private static readonly string[] OudeExtensies = [".v" + "db", ".v" + "bak", ".vs" + "jabloon"];

    // Bewust samengesteld, zodat dit bestand het woord zelf niet bevat.
    private static readonly string OudDomein = "scout" + "ing";

    // CLAUDE.md beschrijft de herkomst van het product.
    private static readonly string[] Uitzonderingen =
    [
        "CLAUDE.md",
        "ProductnaamTests.cs",
    ];

    // prive/ en voorbeelden/ bevatten echte gegevens en worden nooit gelezen.
    private static readonly string[] Genegeerdemappen = [".git", "bin", "obj", "node_modules", "artifacts", "TestResults", ".vs", "prive", "privé", "voorbeelden"];

    private static readonly string[] Tekstextensies =
        [".cs", ".csproj", ".slnx", ".xaml", ".xml", ".json", ".md", ".yml", ".yaml", ".sh", ".wxs", ".wixproj", ".props", ".targets", ".config", ".txt", ".ts", ".svelte", ".html"];

    private static string Opslagplaats()
    {
        var map = new DirectoryInfo(AppContext.BaseDirectory);
        while (map is not null)
        {
            if (Directory.Exists(Path.Combine(map.FullName, ".git")) || map.GetFiles("*.slnx").Length > 0)
            {
                return map.FullName;
            }

            map = map.Parent;
        }

        throw new InvalidOperationException("De hoofdmap van de opslagplaats is niet gevonden.");
    }

    private static IEnumerable<string> Bestanden(string map)
    {
        foreach (var bestand in Directory.EnumerateFiles(map))
        {
            yield return bestand;
        }

        foreach (var submap in Directory.EnumerateDirectories(map))
        {
            if (Genegeerdemappen.Contains(Path.GetFileName(submap), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var bestand in Bestanden(submap))
            {
                yield return bestand;
            }
        }
    }

    private static IEnumerable<string> Gecontroleerd()
    {
        var wortel = Opslagplaats();
        return Bestanden(wortel)
            .Where(b => !Uitzonderingen.Contains(Path.GetFileName(b), StringComparer.OrdinalIgnoreCase))
            .Select(b => Path.GetRelativePath(wortel, b));
    }

    [Fact]
    public void Geen_pad_of_bestandsnaam_draagt_de_oude_naam()
    {
        var wortel = Opslagplaats();
        var gevonden = Gecontroleerd()
            .Where(p => p.Contains(OudeNaam, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var mappen = Directory.EnumerateDirectories(wortel, "*", SearchOption.AllDirectories)
            .Select(d => Path.GetRelativePath(wortel, d))
            .Where(p => !p.Split(Path.DirectorySeparatorChar).Any(d => Genegeerdemappen.Contains(d, StringComparer.OrdinalIgnoreCase)))
            .Where(p => p.Contains(OudeNaam, StringComparison.OrdinalIgnoreCase));

        Assert.Empty(gevonden.Concat(mappen));
    }

    [Fact]
    public void Geen_tekstbestand_noemt_de_oude_naam()
    {
        var wortel = Opslagplaats();
        var gevonden = Gecontroleerd()
            .Where(p => Tekstextensies.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
            .Where(p => File.ReadAllText(Path.Combine(wortel, p)).Contains(OudeNaam, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(gevonden);
    }

    /// <summary>BTW-Boeker is geen ledenadministratie meer: teksten, tests en documentatie noemen het oude domein niet.</summary>
    [Fact]
    public void Geen_tekstbestand_noemt_het_oude_domein()
    {
        var wortel = Opslagplaats();
        var gevonden = Gecontroleerd()
            .Where(p => Tekstextensies.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
            .Where(p => File.ReadAllText(Path.Combine(wortel, p)).Contains(OudDomein, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(gevonden);
    }

    [Fact]
    public void Namespaces_en_methodenamen_bevatten_geen_koppelteken_uit_de_productnaam()
    {
        var wortel = Opslagplaats();
        var gevonden = Gecontroleerd()
            .Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .SelectMany(p => File.ReadAllLines(Path.Combine(wortel, p))
                .Select((regel, i) => (Pad: p, Nummer: i + 1, Regel: regel.Trim())))
            .Where(r => (r.Regel.StartsWith("namespace ", StringComparison.Ordinal)
                    || r.Regel.StartsWith("using ", StringComparison.Ordinal)
                    || r.Regel.StartsWith("public void ", StringComparison.Ordinal)
                    || r.Regel.StartsWith("public async Task ", StringComparison.Ordinal))
                && r.Regel.Contains("BTW-Boeker", StringComparison.Ordinal))
            .Select(r => $"{r.Pad}:{r.Nummer}")
            .ToList();

        Assert.Empty(gevonden);
    }

    [Fact]
    public void Broncode_gebruikt_geen_extensies_van_het_oude_product()
    {
        var wortel = Opslagplaats();
        var gevonden = Gecontroleerd()
            .Where(p => p.StartsWith("src", StringComparison.OrdinalIgnoreCase) || p.StartsWith("tests", StringComparison.OrdinalIgnoreCase))
            .Where(p => Tekstextensies.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
            .Where(p =>
            {
                var tekst = File.ReadAllText(Path.Combine(wortel, p));
                return OudeExtensies.Any(e => tekst.Contains(e + "\"", StringComparison.Ordinal)
                    || tekst.Contains(e + ")", StringComparison.Ordinal)
                    || tekst.Contains(e + "-", StringComparison.Ordinal)
                    || tekst.Contains(e + "|", StringComparison.Ordinal)
                    || tekst.Contains(e + "<", StringComparison.Ordinal)
                    || tekst.Contains(e + " ", StringComparison.Ordinal)
                    || tekst.Contains(e + ".", StringComparison.Ordinal));
            })
            .ToList();

        Assert.Empty(gevonden);
    }
}
