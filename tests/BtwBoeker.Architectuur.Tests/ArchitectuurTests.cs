using System.Reflection;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Financien.Boekhouding;
using BtwBoeker.Infrastructure.Database;

namespace BtwBoeker.Architectuur.Tests;

/// <summary>Bewaakt de lagen (docs/ARCHITECTUUR.md).</summary>
public class ArchitectuurTests
{
    private static readonly Assembly Domain = typeof(Grootboekrekening).Assembly;
    private static readonly Assembly Application = typeof(BoekhoudingDienst).Assembly;
    private static readonly Assembly Infrastructure = typeof(DatabaseBeheer).Assembly;

    private static IEnumerable<string> Verwijzingen(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

    [Fact]
    public void Domain_verwijst_naar_geen_enkele_andere_laag_of_framework()
    {
        Assert.DoesNotContain(Verwijzingen(Domain), n =>
            n.StartsWith("BTW-Boeker", StringComparison.Ordinal)
            || n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || n.StartsWith("Presentation", StringComparison.Ordinal)
            || n.StartsWith("System.Windows", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_verwijst_alleen_naar_domain()
    {
        var verwijzingen = Verwijzingen(Application).ToList();

        Assert.Contains("BtwBoeker.Domain", verwijzingen);
        Assert.DoesNotContain(verwijzingen, n =>
            n is "BtwBoeker.Infrastructure" or "BTW-Boeker"
            || n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || n.StartsWith("Microsoft.Data.Sqlite", StringComparison.Ordinal)
            || n.StartsWith("Presentation", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_verwijst_niet_naar_de_app()
    {
        Assert.DoesNotContain(Verwijzingen(Infrastructure), n => n is "BTW-Boeker" || n.StartsWith("Presentation", StringComparison.Ordinal));
    }

    [Fact]
    public void App_gebruikt_een_eigen_productidentiteit()
    {
        var map = Path.Combine(Repositorymap(), "src", "BtwBoeker.App");
        var project = File.ReadAllText(Path.Combine(map, "BtwBoeker.App.csproj"));
        Assert.Contains("<AssemblyName>BTW-Boeker</AssemblyName>", project, StringComparison.Ordinal);
        Assert.Contains("<Product>BTW-Boeker</Product>", project, StringComparison.Ordinal);
        // Besluit 20: het eigen icoon (uit assets/bron/btw-boeker-icoon-1024.png) is het icoon van het programma, het venster
        // en de taakbalk; de MSI gebruikt hetzelfde bestand voor de snelkoppeling.
        Assert.Contains(@"<ApplicationIcon>Assets\BtwBoeker.ico</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(map, "Assets", "BtwBoeker.ico")));
    }

    /// <summary>Keuze A (2026-11-10): de eigen titelbalk toont linksboven het app-icoon op 16 px, zoals Windows 11-apps.</summary>
    [Fact]
    public void Titelbalk_toont_het_icoon_op_16_px()
    {
        var map = Path.Combine(Repositorymap(), "src", "BtwBoeker.App");
        var project = File.ReadAllText(Path.Combine(map, "BtwBoeker.App.csproj"));
        var venster = File.ReadAllText(Path.Combine(map, "Views", "ShellWindow.xaml"));

        Assert.Contains(@"<Resource Include=""Assets\BtwBoeker.ico"" />", project, StringComparison.Ordinal);
        Assert.Contains(@"Source=""/Assets/BtwBoeker.ico"" Width=""16"" Height=""16""", venster, StringComparison.Ordinal);
        Assert.Contains(@"AutomationProperties.AutomationId=""TitelbalkIcoon""", venster, StringComparison.Ordinal);
        Assert.DoesNotContain(@"<TextBlock Text=""BTW""", venster, StringComparison.Ordinal);
    }

    [Fact]
    public void PdfPig_wordt_alleen_in_infrastructure_gebruikt()
    {
        Assert.DoesNotContain(Verwijzingen(Domain).Concat(Verwijzingen(Application)), n => n.StartsWith("UglyToad", StringComparison.Ordinal));
        Assert.Contains(Verwijzingen(Infrastructure), n => n.StartsWith("UglyToad.PdfPig", StringComparison.Ordinal));
        var buiten = Directory
            .EnumerateFiles(Path.Combine(Repositorymap(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}BtwBoeker.Infrastructure{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("UglyToad", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Empty(buiten);
    }

    private static string Repositorymap()
    {
        var map = new DirectoryInfo(AppContext.BaseDirectory);
        while (map is not null && !File.Exists(Path.Combine(map.FullName, "BtwBoeker.slnx")))
        {
            map = map.Parent;
        }

        return map?.FullName ?? throw new InvalidOperationException("Repositorymap niet gevonden.");
    }

    /// <summary>
    /// Een AutomationId op een element zonder automation peer (Border, panels) is onzichtbaar voor UI Automation en voor
    /// schermlezers (huisregel 2). Gebruik Groepskader, Groepspaneel of Groepsraster (BtwBoeker.App.Views.Toegankelijkheid).
    /// </summary>
    [Fact]
    public void Xaml_zet_geen_AutomationId_op_een_element_zonder_automation_peer()
    {
        var zonderPeer = new[] { "Border", "StackPanel", "Grid", "DockPanel", "WrapPanel", "Canvas", "UniformGrid", "VirtualizingStackPanel" };
        var fouten = new List<string>();
        foreach (var bestand in XamlBestanden())
        {
            var tekst = File.ReadAllText(bestand);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                         tekst, "<([\\w:.]+)\\b[^<>]*?AutomationProperties\\.AutomationId=\"([^\"]+)\""))
            {
                if (zonderPeer.Contains(m.Groups[1].Value))
                {
                    fouten.Add($"{Path.GetFileName(bestand)}: {m.Groups[1].Value} {m.Groups[2].Value}");
                }
            }
        }

        Assert.True(fouten.Count == 0, "AutomationId op een element zonder peer: " + string.Join(", ", fouten));
    }

    /// <summary>
    /// De accentknop en het selectievakje van het Fluent-thema herkennen geen toegangstoets en tonen "_Boeken" letterlijk.
    /// Een accentknop of CheckBox met een underscore in de tekst gebruikt daarom ContentTemplate Toegangstekst (App.xaml).
    /// </summary>
    [Fact]
    public void Accentknop_met_toegangstoets_gebruikt_de_toegangstekst()
    {
        var fouten = new List<string>();
        foreach (var bestand in XamlBestanden())
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(bestand), "<(Button|CheckBox)\\b[^<>]*>"))
            {
                var tag = m.Value;
                if ((tag.StartsWith("<CheckBox", StringComparison.Ordinal) || tag.Contains("AccentButtonStyle", StringComparison.Ordinal))
                    && System.Text.RegularExpressions.Regex.Match(tag, "Content=\"([^\"]*)\"") is { Success: true } inhoud
                    && inhoud.Groups[1].Value.Contains('_', StringComparison.Ordinal)
                    && !tag.Contains("ContentTemplate=\"{StaticResource Toegangstekst}\"", StringComparison.Ordinal))
                {
                    fouten.Add($"{Path.GetFileName(bestand)}: {inhoud.Groups[1].Value}");
                }
            }
        }

        Assert.True(fouten.Count == 0, "Accentknop of selectievakje zonder Toegangstekst: " + string.Join(", ", fouten));
    }

    /// <summary>
    /// Besluit 168: één hoofdactie per pagina. Per subtabblad (TabItem) van de pagina's hooguit één accentknop, en in een
    /// pagina zonder subtabbladen hooguit één. Buiten de subtabbladen mag één accentknop staan (het startscherm van een lege
    /// boekhouding); <i>Opslaan</i> kiest zijn stijl met <c>HoofdactieStijl</c>.
    /// </summary>
    [Fact]
    public void Elke_pagina_heeft_hooguit_een_hoofdactie()
    {
        var fouten = new List<string>();
        foreach (var bestand in XamlBestanden().Where(b => b.EndsWith("View.xaml", StringComparison.Ordinal)))
        {
            var tekst = File.ReadAllText(bestand);
            var tabbladen = System.Text.RegularExpressions.Regex.Matches(tekst, "<TabItem\\b.*?</TabItem>", System.Text.RegularExpressions.RegexOptions.Singleline);
            foreach (System.Text.RegularExpressions.Match tab in tabbladen)
            {
                if (Accentknoppen(tab.Value) > 1)
                {
                    fouten.Add($"{Path.GetFileName(bestand)}: {System.Text.RegularExpressions.Regex.Match(tab.Value, "AutomationId=\"(\\w+)\"").Groups[1].Value}");
                }
            }

            var buiten = tabbladen.Aggregate(tekst, (rest, tab) => rest.Replace(tab.Value, string.Empty, StringComparison.Ordinal));
            if (Accentknoppen(buiten) > 1)
            {
                fouten.Add($"{Path.GetFileName(bestand)}: buiten de subtabbladen");
            }
        }

        Assert.True(fouten.Count == 0, "Meer dan één hoofdactie: " + string.Join(", ", fouten));

        static int Accentknoppen(string xaml) => System.Text.RegularExpressions.Regex.Count(xaml, "AccentButtonStyle");
    }

    /// <summary>
    /// <c>Keyboard.Press</c> van FlaUI drukt een toets alleen in. Zonder loslaten blijft bijvoorbeeld Alt ingedrukt en worden
    /// alle volgende toetsen (ook in andere tests en runs) sneltoetsen; UI-tests gebruiken daarom <c>Keyboard.Type</c>.
    /// </summary>
    [Fact]
    public void Ui_tests_drukken_geen_toets_in_zonder_hem_los_te_laten()
    {
        var fouten = Directory
            .EnumerateFiles(Path.Combine(Repositorymap(), "tests", "BtwBoeker.App.Tests", "UI"), "*.cs", SearchOption.AllDirectories)
            .Where(b => File.ReadAllText(b).Contains("Keyboard.Press(", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(fouten.Count == 0, "Keyboard.Press zonder Release in: " + string.Join(", ", fouten));
    }

    /// <summary>
    /// Besluit 236: verbindingen met de versleutelde database ontstaan alleen in DatabaseBeheer en DatabaseSessie (met pooling),
    /// en een sessie leegt bij sluiten zijn pool, zodat er geen bestandshandles open blijven.
    /// </summary>
    [Fact]
    public void Verbindingen_alleen_via_databasebeheer_en_de_pool_wordt_geleegd()
    {
        var map = Path.Combine(Repositorymap(), "src");
        var toegestaan = new[] { "DatabaseBeheer.cs", "DatabaseSessie.cs", "OntwerpDbContextFactory.cs" };
        var fouten = Directory.EnumerateFiles(map, "*.cs", SearchOption.AllDirectories)
            .Where(b => !b.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                     && !b.Contains(Path.DirectorySeparatorChar + "Migraties" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(b => !toegestaan.Contains(Path.GetFileName(b)))
            .Where(b => File.ReadAllText(b) is var t && (t.Contains("new SqliteConnection(", StringComparison.Ordinal) || t.Contains("UseSqlite(", StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToList();
        Assert.True(fouten.Count == 0, "Eigen databaseverbinding in: " + string.Join(", ", fouten));

        var sessie = File.ReadAllText(Path.Combine(map, "BtwBoeker.Infrastructure", "Database", "DatabaseSessie.cs"));
        var sluiten = sessie[sessie.IndexOf("public ValueTask DisposeAsync()", StringComparison.Ordinal)..];
        Assert.Contains("LeegPool(", sluiten[..sluiten.IndexOf('}')], StringComparison.Ordinal);
    }

    /// <summary>
    /// Besluit 234: de momentopname van de boekhouding is geldig zolang de wijzigingsteller van de sessie gelijk blijft.
    /// <c>SaveChanges</c> verhoogt de teller vanzelf; een bulkbewerking (<c>ExecuteDelete</c>, <c>ExecuteUpdate</c>,
    /// <c>ExecuteSql</c>) gaat daar buitenom en moet de teller daarom zelf verhogen.
    /// </summary>
    [Fact]
    public void Bulkbewerkingen_verhogen_de_wijzigingsteller()
    {
        var map = Path.Combine(Repositorymap(), "src", "BtwBoeker.Infrastructure");
        var fouten = Directory.EnumerateFiles(map, "*.cs", SearchOption.AllDirectories)
            .Where(b => !b.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                     && !b.Contains(Path.DirectorySeparatorChar + "Migraties" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(b => File.ReadAllText(b) is var t
                     && (t.Contains("ExecuteDelete", StringComparison.Ordinal) || t.Contains("ExecuteUpdate", StringComparison.Ordinal)
                         || t.Contains("ExecuteSql", StringComparison.Ordinal))
                     && !t.Contains("Wijzigingen?.Verhoog()", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();
        Assert.True(fouten.Count == 0, "Bulkbewerking zonder Wijzigingen?.Verhoog() in: " + string.Join(", ", fouten));
    }

    // ---- Ontwerpsysteem (besluit 240) ------------------------------------------------------------

    private static readonly double[] Lettergroottes = [12, 16, 24, 32];
    private static readonly double[] Pictogramgroottes = [10, 12, 16, 20, 24, 32, 48];
    private static readonly double[] Raster = [0, 4, 8, 12, 16, 24, 32, 48];
    private static readonly double[] Rondingen = [0, 4, 8, 10, 14]; // 10: pil (teller), 14: kleurrondje van 28 px
    private static readonly char[] Scheidingstekens = [',', ' '];
    private static readonly string[] Afstandseigenschappen = ["Margin", "Padding"];

    private static IEnumerable<(string Bestand, System.Xml.Linq.XElement Element)> XamlElementen() =>
        Directory.EnumerateFiles(Path.Combine(Repositorymap(), "src", "BtwBoeker.App"), "*.xaml", SearchOption.AllDirectories)
            .Where(b => !b.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                     && !b.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .SelectMany(b => System.Xml.Linq.XDocument.Load(b).Descendants().Select(e => (Path.GetFileName(b), e)));

    /// <summary>Waarde van een eigenschap: als attribuut, of als <c>Setter</c> (dan met de stijl als element).</summary>
    private static IEnumerable<(string Bestand, System.Xml.Linq.XElement Element, string Waarde)> Waarden(string eigenschap)
    {
        foreach (var (bestand, element) in XamlElementen())
        {
            if (element.Attribute(eigenschap) is { } attribuut)
            {
                yield return (bestand, element, attribuut.Value);
            }

            if (element.Name.LocalName == "Setter" && (string?)element.Attribute("Property") == eigenschap && element.Attribute("Value") is { } waarde)
            {
                yield return (bestand, element.Parent ?? element, waarde.Value);
            }
        }
    }

    private static bool IsPictogram(System.Xml.Linq.XElement element)
    {
        var lettertype = (string?)element.Attribute("FontFamily")
            ?? element.Elements().Where(e => e.Name.LocalName == "Setter" && (string?)e.Attribute("Property") == "FontFamily")
                .Select(e => (string?)e.Attribute("Value")).FirstOrDefault();
        return lettertype is not null && (lettertype.Contains("Pictogrammen", StringComparison.Ordinal) || lettertype.Contains("Icons", StringComparison.Ordinal));
    }

    private static IEnumerable<double> Getallen(string waarde) =>
        waarde.Split(Scheidingstekens).Where(d => d.Length > 0).Select(d => double.Parse(d, System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public void Lettergroottes_volgen_de_schaal()
    {
        var fouten = Waarden("FontSize")
            .Where(w => !w.Waarde.StartsWith('{'))
            .Where(w => !(IsPictogram(w.Element) ? Pictogramgroottes : Lettergroottes).Contains(Getallen(w.Waarde).Single()))
            .Select(w => $"{w.Bestand}: {w.Element.Name.LocalName} FontSize={w.Waarde}")
            .ToList();
        Assert.True(fouten.Count == 0, "Lettergrootte buiten 12/16/24/32 (14 is de standaard):\n" + string.Join("\n", fouten));
    }

    [Fact]
    public void Marges_en_opvulling_liggen_op_het_raster()
    {
        var fouten = Afstandseigenschappen
            .SelectMany(Waarden)
            .Where(w => !w.Waarde.StartsWith('{'))
            .Where(w => Getallen(w.Waarde).Any(g => !Raster.Contains(Math.Abs(g))))
            .Select(w => $"{w.Bestand}: {w.Element.Name.LocalName} {w.Waarde}")
            .Concat(XamlElementen()
                .Where(e => e.Element.Name.LocalName == "Thickness")
                .Where(e => Getallen(e.Element.Value).Any(g => !Raster.Contains(Math.Abs(g))))
                .Select(e => $"{e.Bestand}: Thickness {e.Element.Value}"))
            .ToList();
        Assert.True(fouten.Count == 0, "Afstand buiten 0/4/8/12/16/24/32/48:\n" + string.Join("\n", fouten));
    }

    [Fact]
    public void Rondingen_zijn_4_of_8_en_pillen_en_rondjes_half_zo_hoog()
    {
        var fouten = Waarden("CornerRadius")
            .Where(w => !w.Waarde.StartsWith('{'))
            .Where(w => Getallen(w.Waarde).Any(g => !Rondingen.Contains(g)))
            .Select(w => $"{w.Bestand}: {w.Element.Name.LocalName} CornerRadius={w.Waarde}")
            .ToList();
        Assert.True(fouten.Count == 0, "Ronding buiten 4/8 (pil 10, rondje 14):\n" + string.Join("\n", fouten));
    }

    /// <summary>
    /// xUnit voert een test met <c>[Fact(Timeout = …)]</c> alleen uit als hij asynchroon is
    /// (anders faalt hij direct met "only supported for async tests"); gevonden in de Windows-run van v0.11.0.
    /// </summary>
    [Fact]
    public void Tests_met_timeout_zijn_asynchroon()
    {
        var patroon = new System.Text.RegularExpressions.Regex(
            @"\[(?:Fact|Theory)\([^\]]*Timeout[^\]]*\)\]\s*(?:\[[^\]]*\]\s*)*public\s+void\s+(\w+)");
        var fouten = Directory
            .EnumerateFiles(Path.Combine(Repositorymap(), "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(b => !b.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                     && !b.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .SelectMany(b => patroon.Matches(File.ReadAllText(b)).Select(m => Path.GetFileName(b) + ": " + m.Groups[1].Value))
            .ToList();

        Assert.True(fouten.Count == 0, "Test met Timeout is niet asynchroon: " + string.Join(", ", fouten));
    }

    /// <summary>Besluit 168: elke hoofdlijst krijgt een leeg scherm met uitleg (en waar mogelijk één knop).</summary>
    [Theory]
    [InlineData("BoekhoudingView.xaml", "Posten")]
    [InlineData("BoekhoudingView.xaml", "Mutaties")]
    [InlineData("BoekhoudingView.xaml", "Postvak")]
    [InlineData("BoekhoudingView.xaml", "Boekingsregels")]
    [InlineData("BoekhoudingView.xaml", "Relaties")]
    public void Hoofdlijst_heeft_een_leeg_scherm(string view, string lijst)
    {
        var tekst = File.ReadAllText(XamlBestanden().Single(b => Path.GetFileName(b) == view));

        Assert.Contains($"<DataGrid ItemsSource=\"{{Binding {lijst}}}\"", tekst, StringComparison.Ordinal);
        Assert.Matches($"<g:LeegScherm[^>]*Lijst=\"{lijst}\"[^>]*{lijst}\\.Count", tekst);
    }

    private static IEnumerable<string> XamlBestanden()
    {
        var scheiding = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(Repositorymap(), "src", "BtwBoeker.App"), "*.xaml", SearchOption.AllDirectories)
            .Where(b => !b.Contains($"{scheiding}obj{scheiding}", StringComparison.Ordinal) && !b.Contains($"{scheiding}bin{scheiding}", StringComparison.Ordinal));
    }

    /// <summary>
    /// De jaarrekeningindeling is alleen weergave (besluiten 263–268): in de applicatielaag gebruiken alleen het model, de
    /// rekenaar, de dienst, de rapporten en het sjabloonbestand haar; grootboek, jaarafsluiting, bank en boekingen niet.
    /// </summary>
    [Fact]
    public void Jaarrekeningindeling_wordt_niet_gebruikt_door_grootboek_jaarafsluiting_of_bank()
    {
        string[] toegestaan = ["BoekhoudingModel.cs", "Jaarrekeningrekenaar.cs", "JaarrekeningDienst.cs", "Rapporten.cs", "Rekeningsjablonen.cs"];
        var overtredingen = Directory
            .EnumerateFiles(Path.Combine(Repositorymap(), "src", "BtwBoeker.Application"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !toegestaan.Contains(Path.GetFileName(f)))
            .Where(f => File.ReadAllText(f) is var tekst
                && (tekst.Contains("Jaarrekeningindeling", StringComparison.Ordinal) || tekst.Contains("Indelingsregel", StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(overtredingen);
        Assert.DoesNotContain("Jaarrekening", File.ReadAllText(Path.Combine(Repositorymap(), "src", "BtwBoeker.Application", "Boekhouding", "Grootboek.cs")), StringComparison.Ordinal);
    }

    /// <summary>Elke tekst van de weergaven (besluit 127) die de XAML gebruikt, staat in de woordenlijst.</summary>
    [Fact]
    public void Xaml_gebruikt_alleen_termen_uit_de_woordenlijst()
    {
        var bekend = BtwBoeker.Application.Weergaven.Woordenlijst.Termen.Select(t => t.Sleutel).ToHashSet(StringComparer.Ordinal);
        var onbekend = XamlBestanden()
            .SelectMany(b => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(b), "DynamicResource [TU]\\.([A-Za-z0-9.]+)\\}")
                .Select(m => (Bestand: Path.GetFileName(b), Sleutel: m.Groups[1].Value)))
            .Where(x => !bekend.Contains(x.Sleutel))
            .Select(x => $"{x.Bestand}: {x.Sleutel}")
            .Distinct()
            .ToList();

        Assert.Empty(onbekend);
        Assert.True(bekend.Count > 20);
    }
}
