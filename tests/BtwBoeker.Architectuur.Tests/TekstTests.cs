using System.Text.RegularExpressions;
using BtwBoeker.Application.Weergaven;

namespace BtwBoeker.Architectuur.Tests;

/// <summary>
/// UX-review stap 5 (besluit 41): gewone taal in de interface. Overal "…"; sneltoetsen, bestandsformaten en extensies alleen in
/// tooltips en menu's; het bestand heet <i>administratie</i>, niet <i>database</i>; alleen <i>bonnen</i>, geen <i>facturen</i>; geen
/// woorden uit het oude model.
/// </summary>
public partial class TekstTests
{
    /// <summary>Overal verboden, ook in tooltips.</summary>
    private static readonly string[] NergensWoorden = ["...", "administraties", "groepen", "facturen"];

    /// <summary>Alleen in een tooltip, een <c>InputGestureText</c> of een bestandsfilter.</summary>
    private static readonly string[] AlleenInTooltip = ["Ctrl+", ".btwdb", ".btwbackup", ".btwsjabloon", "CAMT", "XAF", "Kruisposten"];

    [GeneratedRegex(@"\b(?<attr>Text|Content|Header|Title|Titel|Tekst|Knoptekst|ToolTip|AutomationProperties\.Name)=""(?<waarde>[^""{][^""]*)""")]
    private static partial Regex XamlTekst();

    [GeneratedRegex(@"\bdatabases?\b", RegexOptions.IgnoreCase)]
    private static partial Regex Database();

    [GeneratedRegex(@"""(?:[^""\\\r\n]|\\.)*""")]
    private static partial Regex Tekenreeks();

    private static string Repositorymap()
    {
        var map = new DirectoryInfo(AppContext.BaseDirectory);
        while (map is not null && !File.Exists(Path.Combine(map.FullName, "BtwBoeker.slnx")))
        {
            map = map.Parent;
        }

        return map?.FullName ?? throw new InvalidOperationException("De hoofdmap van de repository is niet gevonden.");
    }

    private static IEnumerable<string> Fouten(string bron, string tekst, bool isTooltip)
    {
        foreach (var woord in NergensWoorden.Where(w => tekst.Contains(w, StringComparison.OrdinalIgnoreCase)))
        {
            yield return $"{bron}: \"{woord}\" in \"{tekst}\"";
        }

        if (Database().IsMatch(tekst))
        {
            yield return $"{bron}: \"database\" in \"{tekst}\"";
        }

        if (!isTooltip)
        {
            foreach (var woord in AlleenInTooltip.Where(w => tekst.Contains(w, StringComparison.Ordinal)))
            {
                yield return $"{bron}: \"{woord}\" hoort in een tooltip: \"{tekst}\"";
            }
        }
    }

    [Fact]
    public void Zichtbare_teksten_in_de_xaml_zijn_gewone_taal()
    {
        var views = Path.Combine(Repositorymap(), "src", "BtwBoeker.App", "Views");
        var fouten = Directory.EnumerateFiles(views, "*.xaml", SearchOption.AllDirectories)
            .SelectMany(b => XamlTekst().Matches(File.ReadAllText(b))
                .SelectMany(m => Fouten(Path.GetFileName(b), m.Groups["waarde"].Value, m.Groups["attr"].Value == "ToolTip")))
            .ToList();

        Assert.True(fouten.Count == 0, string.Join(Environment.NewLine, fouten));
    }

    /// <summary>De kolomkoppen van de posteditor komen uit de woordenlijst, zodat Eenvoudig zijn eigen woorden toont.</summary>
    [Fact]
    public void Debet_en_credit_staan_niet_vast_in_de_xaml()
    {
        var xaml = File.ReadAllText(Path.Combine(Repositorymap(), "src", "BtwBoeker.App", "Views", "Boekhouding", "BoekhoudingView.xaml"));

        Assert.DoesNotContain("Header=\"Debet\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Credit\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void De_woordenlijst_is_gewone_taal()
    {
        var fouten = Woordenlijst.Termen
            .SelectMany(t => new[] { t.Expert, t.Eenvoudig, t.Uitleg }.OfType<string>()
                .SelectMany(w => Fouten(t.Sleutel, w, isTooltip: t.Sleutel.StartsWith("Tip.", StringComparison.Ordinal) || w == t.Uitleg)))
            .ToList();

        Assert.True(fouten.Count == 0, string.Join(Environment.NewLine, fouten));
    }

    /// <summary>
    /// Teksten in C# die de gebruiker ziet: meldingen, dialoogtitels, kaarten en foutmeldingen. Logregels, bestandsnamen en
    /// bestandsfilters tellen niet; bestandsformaten in foutmeldingen ook niet (die helpen bij een verkeerd bestand).
    /// </summary>
    [Fact]
    public void Zichtbare_teksten_in_csharp_zijn_gewone_taal()
    {
        var src = Path.Combine(Repositorymap(), "src");
        string[] mappen = [Path.Combine(src, "BtwBoeker.App"), Path.Combine(src, "BtwBoeker.Application"), Path.Combine(src, "BtwBoeker.Infrastructure")];
        string[] overslaan = ["LoggerMessage", "Meetpunt", "ShellLog", "Filter =", "FileName =", "nameof(", ".Entity(", "ToTable(", "GeneratedRegex"];
        var fouten = new List<string>();
        foreach (var bestand in mappen.SelectMany(m => Directory.EnumerateFiles(m, "*.cs", SearchOption.AllDirectories))
                     .Where(b => !b.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                 && !b.Contains($"{Path.DirectorySeparatorChar}Migraties{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            foreach (var (regel, nummer) in File.ReadLines(bestand).Select((r, i) => (r, i + 1)))
            {
                var code = regel.TrimStart();
                if (code.StartsWith("//", StringComparison.Ordinal) || overslaan.Any(o => regel.Contains(o, StringComparison.Ordinal)))
                {
                    continue;
                }

                foreach (Match m in Tekenreeks().Matches(regel))
                {
                    var tekst = m.Value;
                    var bron = $"{Path.GetFileName(bestand)}:{nummer}";
                    if (tekst.Contains("...", StringComparison.Ordinal) || tekst.Contains("Ctrl+", StringComparison.Ordinal)
                        || tekst.Contains("facturen", StringComparison.OrdinalIgnoreCase) || tekst.Contains("administraties", StringComparison.OrdinalIgnoreCase)
                        || Database().IsMatch(tekst))
                    {
                        fouten.Add($"{bron}: {tekst}");
                    }
                }
            }
        }

        Assert.True(fouten.Count == 0, string.Join(Environment.NewLine, fouten));
    }
}
