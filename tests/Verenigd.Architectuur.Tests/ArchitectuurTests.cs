using System.Reflection;
using Verenigd.Application.Import;
using Verenigd.Domain.Leden;
using Verenigd.Infrastructure.Database;

namespace Verenigd.Architectuur.Tests;

/// <summary>Bewaakt de lagen (docs/ARCHITECTUUR.md) en de scheiding leden–financiën (besluiten 36 en 39).</summary>
public class ArchitectuurTests
{
    private static readonly Assembly Domain = typeof(Persoon).Assembly;
    private static readonly Assembly Application = typeof(Sol3Inlezer).Assembly;
    private static readonly Assembly Infrastructure = typeof(DatabaseBeheer).Assembly;

    private static IEnumerable<string> Verwijzingen(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

    [Fact]
    public void Domain_verwijst_naar_geen_enkele_andere_laag_of_framework()
    {
        Assert.DoesNotContain(Verwijzingen(Domain), n =>
            n.StartsWith("Verenigd", StringComparison.Ordinal)
            || n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || n.StartsWith("Presentation", StringComparison.Ordinal)
            || n.StartsWith("System.Windows", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_verwijst_alleen_naar_domain()
    {
        var verwijzingen = Verwijzingen(Application).ToList();

        Assert.Contains("Verenigd.Domain", verwijzingen);
        Assert.DoesNotContain(verwijzingen, n =>
            n is "Verenigd.Infrastructure" or "Verenigd"
            || n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || n.StartsWith("Microsoft.Data.Sqlite", StringComparison.Ordinal)
            || n.StartsWith("Presentation", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_verwijst_niet_naar_de_app()
    {
        Assert.DoesNotContain(Verwijzingen(Infrastructure), n => n is "Verenigd" || n.StartsWith("Presentation", StringComparison.Ordinal));
    }

    [Fact]
    public void App_icoon_bevat_alle_windows_formaten_en_is_ingesteld()
    {
        var map = Path.Combine(Repositorymap(), "src", "Verenigd.App");
        var icoon = File.ReadAllBytes(Path.Combine(map, "Assets", "Verenigd.ico"));
        Assert.Equal(1, BitConverter.ToUInt16(icoon, 2)); // type: icoon
        var aantal = BitConverter.ToUInt16(icoon, 4);
        var maten = Enumerable.Range(0, aantal).Select(i => icoon[6 + (16 * i)] == 0 ? 256 : icoon[6 + (16 * i)]).ToHashSet();
        Assert.Superset(new HashSet<int> { 16, 24, 32, 48, 256 }, maten);
        Assert.Contains("<ApplicationIcon>Assets\\Verenigd.ico</ApplicationIcon>", File.ReadAllText(Path.Combine(map, "Verenigd.App.csproj")), StringComparison.Ordinal);
    }

    [Fact]
    public void PdfPig_wordt_alleen_in_infrastructure_gebruikt()
    {
        Assert.DoesNotContain(Verwijzingen(Domain).Concat(Verwijzingen(Application)), n => n.StartsWith("UglyToad", StringComparison.Ordinal));
        Assert.Contains(Verwijzingen(Infrastructure), n => n.StartsWith("UglyToad.PdfPig", StringComparison.Ordinal));
        var buiten = Directory
            .EnumerateFiles(Path.Combine(Repositorymap(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Verenigd.Infrastructure{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("UglyToad", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();
        Assert.Empty(buiten);
    }

    [Theory]
    [InlineData("Leden", "Financien")]
    [InlineData("Financien", "Leden")]
    public void Leden_en_financien_kennen_elkaar_niet(string kant, string andereKant)
    {
        var overtredingen = new List<string>();
        foreach (var assembly in new[] { Domain, Application, Infrastructure })
        {
            foreach (var type in assembly.GetTypes().Where(t => InNamespace(t, kant)))
            {
                overtredingen.AddRange(GebruikteTypen(type)
                    .Where(t => InNamespace(t, andereKant))
                    .Select(t => $"{type.FullName} -> {t.FullName}"));
            }
        }

        Assert.Empty(overtredingen.Distinct());
    }

    [Theory]
    [InlineData("Leden", "Financien")]
    [InlineData("Financien", "Leden")]
    public void Broncode_van_leden_en_financien_kent_elkaar_niet(string kant, string andereKant)
    {
        var overtredingen = Directory
            .EnumerateFiles(Path.Combine(Repositorymap(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => f.Split(Path.DirectorySeparatorChar).Contains(kant))
            .Where(f => File.ReadAllText(f).Contains("." + andereKant, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(overtredingen);
    }

    private static bool InNamespace(Type type, string deel) =>
        type.Namespace?.Split('.').Contains(deel, StringComparer.Ordinal) == true;

    /// <summary>Typen in de signatuur: velden, eigenschappen, methoden, constructors en basistypen.</summary>
    private static IEnumerable<Type> GebruikteTypen(Type type)
    {
        const BindingFlags alles = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var typen = new List<Type>();
        if (type.BaseType is { } basis)
        {
            typen.Add(basis);
        }

        typen.AddRange(type.GetInterfaces());
        typen.AddRange(type.GetFields(alles).Select(f => f.FieldType));
        typen.AddRange(type.GetProperties(alles).Select(p => p.PropertyType));
        foreach (var methode in type.GetMethods(alles).Cast<MethodBase>().Concat(type.GetConstructors(alles)))
        {
            typen.AddRange(methode.GetParameters().Select(p => p.ParameterType));
            if (methode is MethodInfo info)
            {
                typen.Add(info.ReturnType);
            }

            if (methode.GetMethodBody() is { } body)
            {
                typen.AddRange(body.LocalVariables.Select(v => v.LocalType));
            }
        }

        return typen.SelectMany(Uitgepakt);
    }

    private static IEnumerable<Type> Uitgepakt(Type type)
    {
        if (type.HasElementType && type.GetElementType() is { } element)
        {
            return Uitgepakt(element);
        }

        return type.IsGenericType ? [type, .. type.GetGenericArguments().SelectMany(Uitgepakt)] : [type];
    }

    private static string Repositorymap()
    {
        var map = new DirectoryInfo(AppContext.BaseDirectory);
        while (map is not null && !File.Exists(Path.Combine(map.FullName, "Verenigd.slnx")))
        {
            map = map.Parent;
        }

        return map?.FullName ?? throw new InvalidOperationException("Repositorymap niet gevonden.");
    }

    /// <summary>
    /// Een AutomationId op een element zonder automation peer (Border, panels) is onzichtbaar voor UI Automation en voor
    /// schermlezers (huisregel 2). Gebruik Groepskader, Groepspaneel of Groepsraster (Verenigd.App.Views.Toegankelijkheid).
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

    private static IEnumerable<string> XamlBestanden()
    {
        var scheiding = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(Repositorymap(), "src", "Verenigd.App"), "*.xaml", SearchOption.AllDirectories)
            .Where(b => !b.Contains($"{scheiding}obj{scheiding}", StringComparison.Ordinal) && !b.Contains($"{scheiding}bin{scheiding}", StringComparison.Ordinal));
    }

    /// <summary>
    /// Het historische boekjaar (besluiten 122 en 125) telt nooit mee in het grootboek: in de applicatielaag gebruiken
    /// alleen het model, de grootboekberekeningen (voor het historische jaar zelf) en de historie-import de historische
    /// regels en totalen. Bank, contributie, facturen en de jaarafsluiting blijven er dus buiten.
    /// </summary>
    [Fact]
    public void Historische_regels_worden_alleen_gebruikt_door_model_grootboek_en_historie()
    {
        string[] toegestaan = ["BoekhoudingModel.cs", "Grootboek.cs", "HistorieDienst.cs", "HistorieInlezer.cs"];
        var overtredingen = Directory
            .EnumerateFiles(Path.Combine(Repositorymap(), "src", "Verenigd.Application"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !toegestaan.Contains(Path.GetFileName(f)))
            .Where(f => File.ReadAllText(f) is var tekst
                && (tekst.Contains("HistorischeRegel", StringComparison.Ordinal) || tekst.Contains("HistorischTotaal", StringComparison.Ordinal)
                    || tekst.Contains("HistorischeTotalen", StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(overtredingen);
    }

    /// <summary>
    /// De begroting telt nooit mee in het grootboek (fase 5a): in de applicatielaag gebruiken alleen het model, de
    /// begrotingsonderdelen en de rapporten haar; grootboek, jaarafsluiting, bank en boekhoudingdienst niet.
    /// </summary>
    [Fact]
    public void Begroting_en_meerjarenplan_worden_niet_gebruikt_door_grootboek_jaarafsluiting_of_bank()
    {
        string[] toegestaan = ["BoekhoudingModel.cs", "Rapporten.cs"];
        var overtredingen = Directory
            .EnumerateFiles(Path.Combine(Repositorymap(), "src", "Verenigd.Application"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Begrotingen{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !toegestaan.Contains(Path.GetFileName(f)))
            .Where(f => File.ReadAllText(f) is var tekst
                && (tekst.Contains("Financien.Begrotingen", StringComparison.Ordinal) || tekst.Contains("Application.Begrotingen", StringComparison.Ordinal)
                    || tekst.Contains("Begrotingsrekenaar", StringComparison.Ordinal) || tekst.Contains("stand.Begrotingen", StringComparison.Ordinal)
                    || tekst.Contains("Meerjarenrekenaar", StringComparison.Ordinal) || tekst.Contains("stand.Meerjarenplannen", StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(overtredingen);
    }

    /// <summary>Elke tekst van de weergaven (besluit 127) die de XAML gebruikt, staat in de woordenlijst.</summary>
    [Fact]
    public void Xaml_gebruikt_alleen_termen_uit_de_woordenlijst()
    {
        var bekend = Verenigd.Application.Weergaven.Woordenlijst.Termen.Select(t => t.Sleutel).ToHashSet(StringComparer.Ordinal);
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
