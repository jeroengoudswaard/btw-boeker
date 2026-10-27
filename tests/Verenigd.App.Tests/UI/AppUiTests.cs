using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Verenigd.Application.Import;
using Verenigd.Application.Instellingen;
using Verenigd.Infrastructure.Database;
using Verenigd.Infrastructure.Instellingen;
using static Verenigd.Application.Tests.Import.Sol3Voorbeeld;
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
    public async Task Geimporteerde_leden_staan_in_tabblad_leden_en_wizard_opent_via_menu()
    {
        var db = Path.Combine(_datamap, "verenigd.vdb");
        var beheer = new DatabaseBeheer();
        await beheer.MaakAanAsync(db, Wachtwoord);
        await using (var sessie = await beheer.OpenAsync(db, Wachtwoord))
        {
            string Schrijf(string naam, byte[] inhoud)
            {
                var pad = Path.Combine(_datamap, naam);
                File.WriteAllBytes(pad, inhoud);
                return pad;
            }

            var dienst = new Sol3ImportDienst(sessie, TimeProvider.System);
            var analyse = await dienst.AnalyseerAsync(new Dictionary<Sol3Export, string>
            {
                [Sol3Export.LedenBasis] = Schrijf("1.csv", Csv(KopLeden, Lid("100000001"), Lid("100000002", voornaam: "Bo"))),
                [Sol3Export.Huishouden] = Schrijf("2.csv", Csv(KopHuishouden, Huishouden("100000001"), Huishouden("100000002"))),
                [Sol3Export.Voogden] = Schrijf("3.csv", Csv(KopVoogden, Voogden("100000001"))),
                [Sol3Export.Functies] = Schrijf("4.csv", Csv(KopFuncties, Functie("100000001"), Functie("100000002"))),
            });
            await dienst.VoerUitAsync(analyse, ImportKeuzes.Standaard(analyse.Voorstel));
        }

        new JsonInstellingenOpslag(Path.Combine(_datamap, "instellingen.json"))
            .Bewaar(new GebruikersInstellingen { LaatsteDatabase = db });

        var venster = Start();
        var dialoog = Retry.WhileNull(() => venster.ModalWindows.FirstOrDefault(), Wachttijd).Result!;
        dialoog.FindFirstDescendant(c => c.ByAutomationId("Wachtwoord"))!.Focus();
        Keyboard.Type(Wachtwoord);
        Keyboard.Press(VirtualKeyShort.ENTER);
        Assert.True(Retry.WhileFalse(() => venster.Title == "verenigd.vdb - Verenigd", Wachttijd).Result, venster.Title);

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_2); // Leden
        Zoek(venster, "Ledenlijst");
        Assert.True(
            Retry.WhileFalse(() => Zoek(venster, "LedenAantal").Name == "2 van 2 personen", Wachttijd).Result,
            Zoek(venster, "LedenAantal").Name);
        Bewaar(venster, "06-leden");

        Zoek(venster, "LedenImporteren").AsButton().Invoke();
        var wizard = Retry.WhileNull(() => venster.ModalWindows.FirstOrDefault(), Wachttijd).Result!;
        Assert.Equal("SOL3-import", wizard.Title);
        Assert.False(Zoek(wizard, "WizardVolgende").IsEnabled); // nog geen bestanden gekozen
        Bewaar(wizard, "07-importwizard");
        Zoek(wizard, "WizardAnnuleren").AsButton().Invoke();
        Assert.True(Retry.WhileFalse(() => venster.ModalWindows.Length == 0, Wachttijd).Result);

        Sluit(venster);
    }

    [Fact]
    public async Task Contributie_toont_subtabbladen_en_incassowizard()
    {
        var db = Path.Combine(_datamap, "verenigd.vdb");
        await new DatabaseBeheer().MaakAanAsync(db, Wachtwoord);
        new JsonInstellingenOpslag(Path.Combine(_datamap, "instellingen.json"))
            .Bewaar(new GebruikersInstellingen { LaatsteDatabase = db });

        var venster = Start();
        var dialoog = Retry.WhileNull(() => venster.ModalWindows.FirstOrDefault(), Wachttijd).Result!;
        dialoog.FindFirstDescendant(c => c.ByAutomationId("Wachtwoord"))!.Focus();
        Keyboard.Type(Wachtwoord);
        Keyboard.Press(VirtualKeyShort.ENTER);
        Assert.True(Retry.WhileFalse(() => venster.Title == "verenigd.vdb - Verenigd", Wachttijd).Result, venster.Title);

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_3); // Contributie (Vereniging)
        Zoek(venster, "ContributieTabblad");
        foreach (var subtab in new[] { "SubtabLeden", "SubtabBetalers", "SubtabTarieven", "SubtabRuns" })
        {
            Zoek(venster, subtab);
        }

        Bewaar(venster, "08-contributie");
        Zoek(venster, "SubtabRuns").AsTabItem().Select();
        Zoek(venster, "NieuweIncassorun").AsButton().Invoke();
        var wizard = Retry.WhileNull(() => venster.ModalWindows.FirstOrDefault(), Wachttijd).Result!;
        Assert.Equal("Nieuwe incassorun", wizard.Title);
        Zoek(wizard, "IncassoMaand");
        Bewaar(wizard, "09-incassowizard");
        Zoek(wizard, "IncassoAnnuleren").AsButton().Invoke();
        Assert.True(Retry.WhileFalse(() => venster.ModalWindows.Length == 0, Wachttijd).Result);

        Sluit(venster);
    }

    [Fact]
    public async Task Boekhouding_leeg_beginnen_toont_subtabbladen_en_nieuwe_post()
    {
        var db = Path.Combine(_datamap, "verenigd.vdb");
        await new DatabaseBeheer().MaakAanAsync(db, Wachtwoord);
        new JsonInstellingenOpslag(Path.Combine(_datamap, "instellingen.json"))
            .Bewaar(new GebruikersInstellingen { LaatsteDatabase = db });

        var venster = Start();
        var dialoog = Retry.WhileNull(() => venster.ModalWindows.FirstOrDefault(), Wachttijd).Result!;
        dialoog.FindFirstDescendant(c => c.ByAutomationId("Wachtwoord"))!.Focus();
        Keyboard.Type(Wachtwoord);
        Keyboard.Press(VirtualKeyShort.ENTER);
        Assert.True(Retry.WhileFalse(() => venster.Title == "verenigd.vdb - Verenigd", Wachttijd).Result, venster.Title);

        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_4); // Boekhouding (Vereniging)
        Zoek(venster, "BoekhoudingTabblad");
        Zoek(venster, "OvernemenUitExcel");
        Bewaar(venster, "10-boekhouding-leeg");
        Zoek(venster, "LeegBeginnen").AsButton().Invoke();

        foreach (var subtab in new[] { "SubtabJournaal", "SubtabRekeningschema", "SubtabGrootboekkaart", "SubtabRapporten", "SubtabBoekjaren" })
        {
            Zoek(venster, subtab);
        }

        Zoek(venster, "NieuwePost").AsButton().Invoke();
        Zoek(venster, "PostEditor");
        Zoek(venster, "PostOmschrijving");
        Bewaar(venster, "11-boekhouding-nieuwe-post");
        Zoek(venster, "SubtabRapporten").AsTabItem().Select();
        Zoek(venster, "Rapport");
        Bewaar(venster, "12-boekhouding-rapport");

        Sluit(venster);
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
