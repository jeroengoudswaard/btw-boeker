using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Verenigd.Application.Instellingen;
using Verenigd.Infrastructure.Database;
using Verenigd.Infrastructure.Instellingen;
using FlauiApplicatie = FlaUI.Core.Application;

namespace Verenigd.App.Tests.UI;

/// <summary>
/// End-to-end tests die de echte applicatie starten en via UI Automation bedienen.
/// Vereist een interactieve Windows-sessie. Filter: <c>dotnet test --filter Categorie=UI</c>.
/// Stel <c>VERENIGD_SCREENSHOTS</c> in op een map om schermafbeeldingen te bewaren.
/// </summary>
[Trait("Categorie", "UI")]
[Collection("UI")] // Nooit parallel: toetsenbord en muis zijn gedeeld.
public sealed class AppUiTests : IDisposable
{
    private const string Wachtwoord = "scouting kampvuur 2026";
    private static readonly TimeSpan Wachttijd = TimeSpan.FromSeconds(20);
    private readonly string _datamap = Path.Combine(Path.GetTempPath(), "verenigd-ui", Guid.NewGuid().ToString("N"));
    private readonly UIA3Automation _automation = new();
    private FlauiApplicatie? _app;

    public AppUiTests() => Directory.CreateDirectory(_datamap);

    public void Dispose()
    {
        if (_app is { HasExited: false })
        {
            _app.Kill();
        }

        _app?.Dispose();
        _automation.Dispose();
        try
        {
            Directory.Delete(_datamap, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Start_zonder_database_toont_startpagina_menubalk_en_uitgeschakelde_modules()
    {
        var venster = Start();

        Assert.Equal("Verenigd", venster.Title);
        foreach (var menu in new[] { "Bestand", "Bewerken", "Beeld", "Modules", "Extra", "Help" })
        {
            Zoek(venster, $"Menu_{menu}");
        }

        Zoek(venster, "StartNieuw");
        Zoek(venster, "StartOpenen");
        Zoek(venster, "StartBackup");
        Bewaar(venster, "01-startpagina");

        Zoek(venster, "Menu_Modules").AsMenuItem().Expand();
        var leden = Zoek(venster, "Menu_Leden");
        Assert.False(leden.IsEnabled);
        Keyboard.Press(VirtualKeyShort.ESCAPE); // submenu dicht
        Keyboard.Press(VirtualKeyShort.ESCAPE); // menumodus uit

        Sluit(venster);
    }

    [Fact]
    public async Task Laatste_database_openen_met_wachtwoord_en_modules_via_sneltoetsen()
    {
        var db = Path.Combine(_datamap, "verenigd.vdb");
        await new DatabaseBeheer().MaakAanAsync(db, Wachtwoord);
        new JsonInstellingenOpslag(Path.Combine(_datamap, "instellingen.json"))
            .Bewaar(new GebruikersInstellingen { LaatsteDatabase = db });

        var venster = Start();

        // Wachtwoordvenster verschijnt automatisch voor de laatst gebruikte database.
        var dialoog = Retry.WhileNull(() => venster.ModalWindows.FirstOrDefault(), Wachttijd).Result!;
        Assert.Equal("Database openen", dialoog.Title);
        dialoog.FindFirstDescendant(c => c.ByAutomationId("Wachtwoord"))!.Focus();
        Keyboard.Type(Wachtwoord);
        Keyboard.Press(VirtualKeyShort.ENTER);

        Assert.True(Retry.WhileFalse(() => venster.Title == "verenigd.vdb - Verenigd", Wachttijd).Result, venster.Title);
        Bewaar(venster, "02-overzicht");

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_4); // Boekhouding (Vereniging)
        Assert.True(Retry.WhileFalse(() => venster.Title == "Vereniging - verenigd.vdb - Verenigd", Wachttijd).Result, Diagnose(venster));
        Zoek(venster, "AdministratieBand");
        Bewaar(venster, "03-boekhouding-vereniging");

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.OEM_COMMA); // Instellingen
        Zoek(venster, "InstellingenOpslaan");
        Bewaar(venster, "04-instellingen");

        Sluit(venster);

        var bewaard = new JsonInstellingenOpslag(Path.Combine(_datamap, "instellingen.json")).Laad();
        Assert.Contains(bewaard.OpenTabbladen, t => t.Module == Application.Modules.ModuleSoort.Boekhouding);
    }

    [Fact]
    public void Donker_thema_wordt_toegepast_zonder_fouten()
    {
        new JsonInstellingenOpslag(Path.Combine(_datamap, "instellingen.json"))
            .Bewaar(new GebruikersInstellingen { Thema = Thema.Donker });

        var venster = Start();

        Zoek(venster, "StartNieuw");
        Bewaar(venster, "05-startpagina-donker");
        Sluit(venster);
    }

    private Window Start()
    {
        var info = new ProcessStartInfo(ExePad()) { UseShellExecute = false };
        info.Environment["VERENIGD_DATAMAP"] = _datamap;
        _app = FlauiApplicatie.Launch(info);
        var venster = _app.GetMainWindow(_automation, Wachttijd);
        Assert.NotNull(venster);
        return venster;
    }

    private static AutomationElement Zoek(AutomationElement bron, string automationId) =>
        Retry.WhileNull(() => bron.FindFirstDescendant(c => c.ByAutomationId(automationId)), Wachttijd).Result
        ?? throw new Xunit.Sdk.XunitException($"Element '{automationId}' niet gevonden.");

    private string Diagnose(Window venster)
    {
        var focus = _automation.FocusedElement();
        return $"Titel: '{venster.Title}'; focus: {focus?.ControlType} '{focus?.Name}' (proces {focus?.Properties.ProcessId.ValueOrDefault}, app {_app?.ProcessId})";
    }

    private void Sluit(Window venster)
    {
        venster.Close();
        Assert.True(Retry.WhileFalse(() => _app!.HasExited, Wachttijd).Result, "De applicatie sloot niet af.");
    }

    private static void Bewaar(AutomationElement element, string naam)
    {
        var map = Environment.GetEnvironmentVariable("VERENIGD_SCREENSHOTS");
        if (map is null)
        {
            return;
        }

        Directory.CreateDirectory(map);
        Thread.Sleep(300); // Animaties van het Fluent-thema laten uitlopen.
        Capture.Element(element).ToFile(Path.Combine(map, naam + ".png"));
    }

    private static string ExePad()
    {
        // tests/Verenigd.App.Tests/bin/<Configuratie>/net10.0-windows/ -> src/Verenigd.App/bin/<Configuratie>/net10.0-windows/
        var testmap = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuratie = testmap.Parent!.Name;
        var root = testmap.Parent.Parent!.Parent!.Parent!.Parent!.FullName;
        var pad = Path.Combine(root, "src", "Verenigd.App", "bin", configuratie, testmap.Name, "Verenigd.exe");
        Assert.True(File.Exists(pad), $"Verenigd.exe niet gevonden op {pad}. Bouw eerst de oplossing.");
        return pad;
    }
}
