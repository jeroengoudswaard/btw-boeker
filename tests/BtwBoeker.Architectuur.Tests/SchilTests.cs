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

        Assert.True(knoppen.Count >= 1, "Verwacht de knop Opslaan op Boekhouding (Instellingen bewaart direct, besluit 42)");
        var fouten = knoppen.Where(k => !k.Knop.Contains("Visibility=\"{Binding HeeftOnopgeslagenWijzigingen", StringComparison.Ordinal)).Select(k => k.Bestand);
        Assert.True(!fouten.Any(), "Opslaan zonder zichtbaarheid bij wijzigingen: " + string.Join(", ", fouten));
        Assert.DoesNotContain("Niet-opgeslagen wijzigingen", string.Concat(Directory.EnumerateFiles(Views, "*.xaml", SearchOption.AllDirectories).Select(File.ReadAllText)), StringComparison.Ordinal);
    }

    /// <summary>
    /// UX-review stap 3 (besluit 38): elk onderdeel met een lijst heeft één detailpaneel dat sluit (✕, Esc), en nergens staat
    /// nog een leeg vlak met "Selecteer een …" of "Kies een … in de lijst".
    /// </summary>
    [Fact]
    public void Lijst_en_detail_zonder_leeg_vlak()
    {
        var xaml = Lees("Boekhouding", "BoekhoudingView.xaml");

        Assert.DoesNotContain("Selecteer een", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("in de lijst.", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("T.Tekst.SelecteerPost", xaml, StringComparison.Ordinal);
        var panelen = Detailpaneel().Matches(xaml).Select(m => m.Value).ToList();
        Assert.Equal(7, panelen.Count); // Journaal, Bank, Bonnen, Boekingsregels, Relaties, Rekeningschema, Boekjaren (besluit 40)
        Assert.All(panelen, p => Assert.Contains("SluitOpdracht=\"{Binding", p, StringComparison.Ordinal));
        Assert.Contains("Key=\"Escape\"", xaml, StringComparison.Ordinal);
    }

    /// <summary>Boekingsregels en het rekeningschema worden in het detail bewerkt, niet meer in de cellen van de lijst.</summary>
    [Theory]
    [InlineData("Boekingsregels")]
    [InlineData("Rekeningen")]
    public void Inrichtingslijsten_zijn_alleen_lezen(string lijst)
    {
        var grid = Regex.Match(Lees("Boekhouding", "BoekhoudingView.xaml"), $"<DataGrid ItemsSource=\"{{Binding {lijst}[^>]*>");

        Assert.True(grid.Success, lijst);
        Assert.Contains("IsReadOnly=\"True\"", grid.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// UX-review stap 4 (besluit 40): acties staan in het detail, volgens de status, niet als knoppen in elke rij van een lijst;
    /// en de startwizard heeft geen <i>Later verder</i> meer (sluiten doet hetzelfde).
    /// </summary>
    [Fact]
    public void Acties_staan_in_het_detail_en_niet_in_de_rijen()
    {
        var boekjaren = Regex.Match(Lees("Boekhouding", "BoekhoudingView.xaml"), "<DataGrid ItemsSource=\"\\{Binding Jaren\\}\".*?</DataGrid>", RegexOptions.Singleline);

        Assert.True(boekjaren.Success, "Geen lijst Jaren");
        Assert.DoesNotContain("<Button", boekjaren.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("StartwizardLaterVerder", Lees("Inrichting", "StartwizardVenster.xaml"), StringComparison.Ordinal);
    }

    /// <summary>
    /// UX-review stap 6 (besluit 42): Instellingen bewaart direct, zonder knop Opslaan, met thema en weergave (ook in het menu
    /// Beeld); de drempel <i>zonder bon</i> staat bij het filter in het Journaal.
    /// </summary>
    [Fact]
    public void Instellingen_bewaart_direct_en_de_drempel_staat_bij_het_filter()
    {
        var instellingen = Lees("InstellingenView.xaml");

        Assert.DoesNotContain("OpslaanCommand", instellingen, StringComparison.Ordinal);
        Assert.DoesNotContain("Drempel", instellingen, StringComparison.Ordinal);
        Assert.Contains("GekozenThema", instellingen, StringComparison.Ordinal);
        Assert.Contains("GekozenWeergave", instellingen, StringComparison.Ordinal);
        Assert.Contains("KiesThemaCommand", Lees("ShellWindow.xaml"), StringComparison.Ordinal);
        Assert.Contains("Drempeltekst", Lees("Boekhouding", "BoekhoudingView.xaml"), StringComparison.Ordinal);
    }

    [GeneratedRegex("<g:Detailpaneel\\s[^>]*>")]
    private static partial Regex Detailpaneel();

    /// <summary>De navigatiebalk kiest het onderdeel: geen tabkoppen en ook geen lege lijn waar die stonden.</summary>
    [Fact]
    public void Onderdelen_zonder_tabkoppen_gebruiken_het_onderdeelpaneel()
    {
        var tabbladen = Tabblad().Matches(Lees("Boekhouding", "BoekhoudingView.xaml")).ToList();

        Assert.NotEmpty(tabbladen);
        Assert.All(tabbladen, t => Assert.Contains("Style=\"{StaticResource Onderdeelpaneel}\"", t.Value, StringComparison.Ordinal));
    }
}
