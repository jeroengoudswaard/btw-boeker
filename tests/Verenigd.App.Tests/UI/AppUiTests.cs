using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Verenigd.Application.Bijlagen;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Import;
using Verenigd.Application.Instellingen;
using Verenigd.Infrastructure.Database;
using Verenigd.Infrastructure.Instellingen;
using Verenigd.Infrastructure.Pdf;
using Verenigd.Tests.Gedeeld;
using static Verenigd.Application.Tests.Import.Sol3Voorbeeld;
using FlauiApplicatie = FlaUI.Core.Application;

namespace Verenigd.App.Tests.UI;

/// <summary>
/// End-to-end tests die de echte applicatie starten en via UI Automation bedienen.
/// Vereist een interactieve Windows-sessie. Filter: <c>dotnet test --filter Categorie=UI</c>.
/// Typ of klik niet tijdens deze tests: FlaUI gebruikt het echte toetsenbord en de echte muis.
/// Elke test heeft een harde time-out, zodat een vastgelopen venster de run niet blokkeert; het eigen app-proces wordt altijd afgesloten.
/// Stel <c>VERENIGD_SCREENSHOTS</c> in op een map om schermafbeeldingen te bewaren.
/// </summary>
[Trait("Categorie", "UI")]
[Collection("UI")] // Nooit parallel: toetsenbord en muis zijn gedeeld.
public sealed class AppUiTests : IDisposable
{
    private const string Wachtwoord = "scouting kampvuur 2026";
    private const int Timeout = 180_000;
    private static readonly TimeSpan Wachttijd = TimeSpan.FromSeconds(20);
    private readonly string _datamap = Path.Combine(Path.GetTempPath(), "verenigd-ui", Guid.NewGuid().ToString("N"));
    private readonly UIA3Automation _automation = new();
    private FlauiApplicatie? _app;

    public AppUiTests() => Directory.CreateDirectory(_datamap);

    public void Dispose()
    {
        try
        {
            if (_app is { HasExited: false })
            {
                _app.Kill();
                Retry.WhileFalse(() => _app.HasExited, TimeSpan.FromSeconds(5));
            }
        }
        catch (InvalidOperationException)
        {
            // Het proces was al afgesloten.
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

    [Fact(Timeout = Timeout)]
    public async Task Start_zonder_database_toont_startpagina_menubalk_en_uitgeschakelde_modules()
    {
        await Task.Yield();
        BewaarInstellingen(new GebruikersInstellingen { StartwizardGetoond = true }); // geen automatische startwizard
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

        // Dichtklappen via UI Automation: na Expand() heeft het menu niet altijd de toetsenbordfocus, dus Escape is onbetrouwbaar.
        var bestand = Zoek(venster, "Menu_Bestand").AsMenuItem();
        bestand.Expand();
        var importeren = Zoek(venster, "Menu_Importeren").AsMenuItem();
        importeren.Expand();
        Assert.False(Zoek(venster, "Menu_ImporteerHistorie").IsEnabled); // zonder database
        Zoek(venster, "Menu_ImporteerSjabloon");
        importeren.Collapse();
        bestand.Collapse();

        Sluit(venster);
    }

    [Fact(Timeout = Timeout)]
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

        Sneltoets(venster, VirtualKeyShort.KEY_4); // Boekhouding (Vereniging)
        Assert.True(Retry.WhileFalse(() => venster.Title == "Vereniging - verenigd.vdb - Verenigd", Wachttijd).Result, Diagnose(venster));
        Zoek(venster, "AdministratieBand");
        Bewaar(venster, "03-boekhouding-vereniging");

        Sneltoets(venster, VirtualKeyShort.OEM_COMMA); // Instellingen
        Zoek(venster, "InstellingenOpslaan");
        Bewaar(venster, "04-instellingen");

        Sluit(venster);

        var bewaard = new JsonInstellingenOpslag(Path.Combine(_datamap, "instellingen.json")).Laad();
        Assert.Contains(bewaard.OpenTabbladen, t => t.Module == Application.Modules.ModuleSoort.Boekhouding);
    }

    [Fact(Timeout = Timeout)]
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

        Sneltoets(venster, VirtualKeyShort.KEY_2); // Leden
        Zoek(venster, "Ledenlijst");
        Assert.True(
            Retry.WhileFalse(() => Zoek(venster, "LedenAantal").Name == "2 van 2 personen", Wachttijd).Result,
            Zoek(venster, "LedenAantal").Name);
        Bewaar(venster, "06-leden");

        Zoek(venster, "LedenImporteren").AsButton().Invoke();
        var wizard = Retry.WhileNull(() => venster.ModalWindows.FirstOrDefault(), Wachttijd).Result!;
        Assert.Equal("SOL3-import", wizard.Title);
        Assert.False(Zoek(wizard, "WizardVolgende").IsEnabled); // nog geen bestanden gekozen
        var uitleg = Zoek(wizard, "WizardSol3Uitleg"); // besluit 185
        uitleg.Patterns.ExpandCollapse.Pattern.Expand();
        Assert.NotNull(Retry.WhileNull(() => uitleg.FindFirstDescendant(c => c.ByName("Kolomnamen kopiëren van Export 4 · Functies")), Wachttijd).Result);
        uitleg.Patterns.ExpandCollapse.Pattern.Collapse();
        Bewaar(wizard, "07-importwizard");
        Zoek(wizard, "WizardAnnuleren").AsButton().Invoke();
        Assert.True(Retry.WhileFalse(() => venster.ModalWindows.Length == 0, Wachttijd).Result);

        Sluit(venster);
    }

    [Fact(Timeout = Timeout)]
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

        Sneltoets(venster, VirtualKeyShort.KEY_3); // Contributie (Vereniging)
        Zoek(venster, "ContributieTabblad");
        foreach (var subtab in new[] { "SubtabLeden", "SubtabBetalers", "SubtabTarieven", "SubtabRuns", "SubtabOpenstaand" })
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

    [Fact(Timeout = Timeout)]
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

        Sneltoets(venster, VirtualKeyShort.KEY_4); // Boekhouding (Vereniging)
        Zoek(venster, "BoekhoudingTabblad");
        Zoek(venster, "OvernemenUitExcel");
        Zoek(venster, "SjabloonInlezen");
        Bewaar(venster, "10-boekhouding-leeg");
        Zoek(venster, "StandaardInrichten").AsButton().Invoke();
        Assert.True(Retry.WhileFalse(() => venster.ModalWindows.Length == 1, Wachttijd).Result, "De wizard Standaardinrichting opende niet.");
        var inrichten = venster.ModalWindows[0];
        Zoek(inrichten, "Speltakken");
        Bewaar(inrichten, "15-standaardinrichting");
        Zoek(inrichten, "InrichtenAnnuleren").AsButton().Invoke();
        Assert.True(Retry.WhileFalse(() => venster.ModalWindows.Length == 0, Wachttijd).Result);
        Zoek(venster, "LeegBeginnen").AsButton().Invoke();

        foreach (var subtab in new[] { "SubtabJournaal", "SubtabBank", "SubtabPostvak", "SubtabBoekingsregels", "SubtabFacturen", "SubtabRelaties", "SubtabRekeningschema", "SubtabGrootboekkaart", "SubtabRapporten", "SubtabBegroting", "SubtabBoekjaren" })
        {
            Zoek(venster, subtab);
        }

        Zoek(venster, "NieuwePost").AsButton().Invoke();
        Zoek(venster, "PostEditor");
        Zoek(venster, "PostOmschrijving");
        Bewaar(venster, "11-boekhouding-nieuwe-post");
        Zoek(venster, "SubtabBank").AsTabItem().Select();
        Zoek(venster, "BankInlezen");
        Bewaar(venster, "13-boekhouding-bank");
        Zoek(venster, "SubtabBoekingsregels").AsTabItem().Select();
        Zoek(venster, "Boekingsregels");
        Zoek(venster, "SubtabRelaties").AsTabItem().Select();
        Zoek(venster, "NieuweRelatie").AsButton().Invoke();
        Zoek(venster, "RelatieNaam");
        Zoek(venster, "SubtabFacturen").AsTabItem().Select();
        Zoek(venster, "NieuweFactuur").AsButton().Invoke();
        Zoek(venster, "FactuurEditor");
        Bewaar(venster, "14-boekhouding-factuur");
        Zoek(venster, "SubtabRapporten").AsTabItem().Select();
        Zoek(venster, "Rapport");
        Bewaar(venster, "12-boekhouding-rapport");

        Sluit(venster);
    }

    [Fact(Timeout = Timeout)]
    public async Task Bijlage_bekijken_toont_de_pdf_in_het_voorbeeldvenster()
    {
        var db = Path.Combine(_datamap, "verenigd.vdb");
        await new DatabaseBeheer().MaakAanAsync(db, Wachtwoord);
        await using (var sessie = await new DatabaseBeheer().OpenAsync(db, Wachtwoord))
        {
            var dienst = new BoekhoudingDienst(sessie, TimeProvider.System);
            var ver = Domain.Administraties.AdministratieSoort.Vereniging;
            await dienst.RichtInAsync(ver);
            var energie = await dienst.BewaarRekeningAsync(ver, new RekeningInvoer(null, 4115, "Energie", null, Domain.Financien.Boekhouding.RekeningSoort.Resultaat, Domain.Financien.Boekhouding.RekeningRol.Geen, true));
            var bank = await dienst.BewaarRekeningAsync(ver, new RekeningInvoer(null, 1100, "Bank", null, Domain.Financien.Boekhouding.RekeningSoort.Balans, Domain.Financien.Boekhouding.RekeningRol.Geen, true));
            var stand = await dienst.LaadAsync(ver);
            var post = await dienst.BewaarConceptAsync(ver, new PostInvoer(null, stand.Dagboeken.Single().Id, DateOnly.FromDateTime(DateTime.Today), "Bouwmarkt",
            [
                new RegelInvoerId(energie, null, new Domain.Financien.Bedrag(2345), Domain.Financien.Bedrag.Nul),
                new RegelInvoerId(bank, null, Domain.Financien.Bedrag.Nul, new Domain.Financien.Bedrag(2345)),
            ]));
            await new BijlageDienst(sessie, new PdfPigTekstLezer(), TimeProvider.System)
                .VoegToeAsync(ver, [new BijlageBestand("bon.pdf", Testpdf.Maak("Bouwmarkt Voorbeeld", "Te betalen 23,45"))], post);
        }

        new JsonInstellingenOpslag(Path.Combine(_datamap, "instellingen.json"))
            .Bewaar(new GebruikersInstellingen { LaatsteDatabase = db });

        var venster = Start();
        var dialoog = Retry.WhileNull(() => venster.ModalWindows.FirstOrDefault(), Wachttijd).Result!;
        dialoog.FindFirstDescendant(c => c.ByAutomationId("Wachtwoord"))!.Focus();
        Keyboard.Type(Wachtwoord);
        Keyboard.Press(VirtualKeyShort.ENTER);
        Assert.True(Retry.WhileFalse(() => venster.Title == "verenigd.vdb - Verenigd", Wachttijd).Result, venster.Title);

        Sneltoets(venster, VirtualKeyShort.KEY_4); // Boekhouding (Vereniging)
        var posten = Zoek(venster, "Journaalposten").AsGrid();
        Assert.True(Retry.WhileFalse(() => posten.RowCount == 1, Wachttijd).Result, "De post staat niet in het Journaal.");
        posten.Rows[0].Select();
        var bekijken = Retry.WhileNull(() => venster.FindFirstDescendant(c => c.ByName("Bekijk bon.pdf")), Wachttijd).Result
            ?? throw new Xunit.Sdk.XunitException("Knop 'Bekijk bon.pdf' niet gevonden.");
        Bewaar(venster, "16-bijlagen");
        bekijken.AsButton().Invoke();

        var voorbeeld = Retry.WhileNull(() => venster.ModalWindows.FirstOrDefault(), Wachttijd).Result!;
        Assert.Equal("bon.pdf – Bijlage", voorbeeld.Title);
        var paginas = Zoek(voorbeeld, "BijlagePaginas");
        Assert.True(Retry.WhileFalse(() => paginas.Name == "1 pagina", Wachttijd).Result, $"Paginatekst: '{paginas.Name}'.");
        Bewaar(voorbeeld, "17-bijlage-voorbeeld");
        Zoek(voorbeeld, "BijlageSluiten").AsButton().Invoke();
        Assert.True(Retry.WhileFalse(() => venster.ModalWindows.Length == 0, Wachttijd).Result);

        Sluit(venster);
    }

    [Fact(Timeout = Timeout)]
    public async Task Donker_thema_wordt_toegepast_zonder_fouten()
    {
        await Task.Yield();
        BewaarInstellingen(new GebruikersInstellingen { Thema = Thema.Donker, StartwizardGetoond = true });

        var venster = Start();

        Zoek(venster, "StartNieuw");
        Bewaar(venster, "05-startpagina-donker");
        Sluit(venster);
    }

    /// <summary>Besluit 169: de eerste start opent de startwizard; ✕ werkt als "Later verder" en de wizard komt niet terug.</summary>
    [Fact(Timeout = Timeout)]
    public async Task Eerste_start_opent_de_startwizard_en_sluiten_werkt_als_later_verder()
    {
        await Task.Yield();
        var venster = Start();

        var wizard = Retry.WhileNull(() => venster.ModalWindows.FirstOrDefault(), Wachttijd).Result
            ?? throw new Xunit.Sdk.XunitException("De startwizard verscheen niet. " + Diagnose(venster));
        Assert.Equal("Verenigd instellen", wizard.Title);
        Zoek(wizard, "StapWelkom");
        Bewaar(wizard, "06-startwizard-welkom");

        Zoek(wizard, "StartwizardVolgende").AsButton().Invoke();
        Zoek(wizard, "StartwizardNieuweDatabase");
        Assert.False(Zoek(wizard, "StartwizardVolgende").IsEnabled); // zonder database niet verder
        Bewaar(wizard, "07-startwizard-database");

        wizard.Close();
        Assert.True(Retry.WhileFalse(() => venster.ModalWindows.Length == 0, Wachttijd).Result, "De startwizard sloot niet. " + Diagnose(venster));
        Zoek(venster, "StartNieuw");
        Sluit(venster);

        var bewaard = new JsonInstellingenOpslag(Path.Combine(_datamap, "instellingen.json")).Laad();
        Assert.True(bewaard.StartwizardGetoond);
    }

    private void BewaarInstellingen(GebruikersInstellingen instellingen) =>
        new JsonInstellingenOpslag(Path.Combine(_datamap, "instellingen.json")).Bewaar(instellingen);

    private Window Start()
    {
        var info = new ProcessStartInfo(ExePad()) { UseShellExecute = false };
        info.Environment["VERENIGD_DATAMAP"] = _datamap;
        _app = FlauiApplicatie.Launch(info);
        var venster = _app.GetMainWindow(_automation, Wachttijd);
        Assert.NotNull(venster);
        venster.SetForeground();
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

    /// <summary>Toetsenbordinvoer gaat alleen naar de app als die de voorgrond heeft en niet in de menumodus staat.</summary>
    private void Sneltoets(Window venster, VirtualKeyShort toets)
    {
        EindigMenumodus(venster);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, toets);
    }

    /// <summary>
    /// Na het openklappen van een menu via UI Automation kan het venster in de menumodus blijven (focus op een MenuItem);
    /// dan komen sneltoetsen en <c>Close()</c> niet aan. Escape beëindigt de menumodus.
    /// </summary>
    private void EindigMenumodus(Window venster)
    {
        venster.SetForeground();
        for (var poging = 0; poging < 4 && _automation.FocusedElement()?.ControlType == ControlType.MenuItem; poging++)
        {
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Thread.Sleep(200);
        }

        Assert.True(_automation.FocusedElement()?.ControlType != ControlType.MenuItem, "De menumodus eindigt niet. " + Diagnose(venster));
    }

    private void Sluit(Window venster)
    {
        EindigMenumodus(venster);
        var voorSluiten = Diagnose(venster);
        venster.Close();
        if (!Retry.WhileFalse(() => _app!.HasExited, Wachttijd).Result)
        {
            var vensters = _app!.GetAllTopLevelWindows(_automation).Select(v => $"{v.ControlType} '{v.Title}'").ToList();
            // Altijd opruimen, zodat een achtergebleven venster de volgende tests niet beïnvloedt.
            _app.Kill();
            throw new Xunit.Sdk.XunitException(
                $"De applicatie sloot niet af. Vóór het sluiten: {voorSluiten}. Nog open: {string.Join(", ", vensters)}.");
        }
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
        // tests/Verenigd.App.Tests/bin/<Configuratie>/<tfm>/ -> src/Verenigd.App/bin/<Configuratie>/<tfm>/ (nu net10.0-windows10.0.19041.0)
        var testmap = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuratie = testmap.Parent!.Name;
        var root = testmap.Parent.Parent!.Parent!.Parent!.Parent!.FullName;
        var pad = Path.Combine(root, "src", "Verenigd.App", "bin", configuratie, testmap.Name, "Verenigd.exe");
        Assert.True(File.Exists(pad), $"Verenigd.exe niet gevonden op {pad}. Bouw eerst de oplossing.");
        return pad;
    }
}
