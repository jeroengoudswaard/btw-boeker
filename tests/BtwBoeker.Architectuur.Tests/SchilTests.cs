using System.Globalization;
using System.Text.RegularExpressions;

namespace BtwBoeker.Architectuur.Tests;

/// <summary>
/// UX-review ronde 2, stappen 1 en 2 (besluiten 32 en 33): de rustiger schil, bewaakt in de XAML. Overgenomen uit het
/// moederproduct.
/// </summary>
public partial class SchilTests
{
    private static readonly char[] Scheidingstekens = [',', ' '];

    [GeneratedRegex("<WindowChrome [^>]*>")]
    private static partial Regex Vensterchrome();

    [GeneratedRegex("GlassFrameThickness=\"([^\"]*)\"")]
    private static partial Regex Glasrand();

    [GeneratedRegex("<Button\\b[^>]*Command=\"\\{Binding OpslaanCommand\\}\"[^>]*>")]
    private static partial Regex Opslaanknop();

    [GeneratedRegex("<TabControl\\b[^>]*>")]
    private static partial Regex Tabblad();

    private static string Repositorymap()
    {
        var map = new DirectoryInfo(AppContext.BaseDirectory);
        while (map is not null && !File.Exists(Path.Combine(map.FullName, "BtwBoeker.slnx")))
        {
            map = map.Parent;
        }

        return map?.FullName ?? throw new InvalidOperationException("De hoofdmap van de repository is niet gevonden.");
    }

    private static string Views => Path.Combine(Repositorymap(), "src", "BtwBoeker.App", "Views");

    private static string Lees(params string[] delen) => File.ReadAllText(Path.Combine([Views, .. delen]));

    /// <summary>
    /// Met <c>GlassFrameThickness="0"</c> tekent Windows geen schaduw en geen rand om het hoofdvenster, waardoor het boven
    /// een ander (wit) venster niet meer te onderscheiden is.
    /// </summary>
    [Fact]
    public void Hoofdvenster_houdt_de_schaduw_en_rand_van_windows()
    {
        var chrome = Vensterchrome().Match(Lees("ShellWindow.xaml"));
        Assert.True(chrome.Success, "Geen WindowChrome in ShellWindow.xaml");
        var glas = Glasrand().Match(chrome.Value);
        Assert.True(glas.Success, "WindowChrome zonder GlassFrameThickness");
        Assert.NotEqual(0, glas.Groups[1].Value.Split(Scheidingstekens, StringSplitOptions.RemoveEmptyEntries).Sum(d => double.Parse(d, CultureInfo.InvariantCulture)));

        Assert.Contains("VensterrandDwm.", Lees("ShellWindow.xaml.cs"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Geen statusbalk (meldingen in de meldingsbalk) en geen menu's die de navigatie herhalen (<i>Modules</i>) of maar één
    /// opdracht hebben (<i>Extra</i>).
    /// </summary>
    [Fact]
    public void Hoofdvenster_heeft_geen_statusbalk_en_geen_dubbele_menus()
    {
        var xaml = Lees("ShellWindow.xaml");

        Assert.DoesNotContain("<StatusBar", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Menu_Modules", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Menu_Extra", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"Meldingsbalk\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"Menu_Instellingen\"", xaml, StringComparison.Ordinal);
    }

    /// <summary>De knop <i>Opslaan</i> van een pagina staat er alleen als er iets op te slaan is; Ctrl+S werkt altijd.</summary>
    [Fact]
    public void Opslaan_op_een_pagina_staat_er_alleen_bij_wijzigingen()
    {
        var knoppen = Directory.EnumerateFiles(Views, "*View.xaml", SearchOption.AllDirectories)
            .SelectMany(b => Opslaanknop().Matches(File.ReadAllText(b)).Select(m => (Bestand: Path.GetFileName(b), Knop: m.Value)))
            .ToList();

        Assert.True(knoppen.Count >= 2, "Verwacht de knop Opslaan op Boekhouding en Instellingen");
        var fouten = knoppen.Where(k => !k.Knop.Contains("Visibility=\"{Binding HeeftOnopgeslagenWijzigingen", StringComparison.Ordinal)).Select(k => k.Bestand);
        Assert.True(!fouten.Any(), "Opslaan zonder zichtbaarheid bij wijzigingen: " + string.Join(", ", fouten));
        Assert.DoesNotContain("Niet-opgeslagen wijzigingen", string.Concat(Directory.EnumerateFiles(Views, "*.xaml", SearchOption.AllDirectories).Select(File.ReadAllText)), StringComparison.Ordinal);
    }

    /// <summary>De navigatiebalk kiest het onderdeel: geen tabkoppen en ook geen lege lijn waar die stonden.</summary>
    [Fact]
    public void Onderdelen_zonder_tabkoppen_gebruiken_het_onderdeelpaneel()
    {
        var tabbladen = Tabblad().Matches(Lees("Boekhouding", "BoekhoudingView.xaml")).ToList();

        Assert.NotEmpty(tabbladen);
        Assert.All(tabbladen, t => Assert.Contains("Style=\"{StaticResource Onderdeelpaneel}\"", t.Value, StringComparison.Ordinal));
    }
}
