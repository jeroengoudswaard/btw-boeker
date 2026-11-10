using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using BtwBoeker.Application.Bank;
using BtwBoeker.Application.Tests.Bank;
using BtwBoeker.Application.Bijlagen;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Instellingen;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Bank;
using BtwBoeker.Domain.Financien.Boekhouding;
using BtwBoeker.Infrastructure.Database;
using BtwBoeker.Infrastructure.Instellingen;
using BtwBoeker.Infrastructure.Pdf;
using BtwBoeker.Infrastructure.Tests.Demo;
using BtwBoeker.Tests.Gedeeld;
using FlauiApplicatie = FlaUI.Core.Application;

namespace BtwBoeker.App.Tests.UI;

/// <summary>
/// End-to-end tests die de echte applicatie starten en via UI Automation bedienen.
/// Vereist een interactieve Windows-sessie. Filter: <c>dotnet test --filter Categorie=UI</c>.
/// Typ of klik niet tijdens deze tests: FlaUI gebruikt het echte toetsenbord en de echte muis.
/// Elke test heeft een harde time-out, zodat een vastgelopen venster de run niet blokkeert; het eigen app-proces wordt altijd afgesloten.
/// Stel <c>BTWBOEKER_SCREENSHOTS</c> in op een map om schermafbeeldingen te bewaren.
/// </summary>
[Trait("Categorie", "UI")]
[Collection("UI")] // Nooit parallel: toetsenbord en muis zijn gedeeld.
public sealed class AppUiTests : IDisposable
{
    private const string Wachtwoord = "scouting kampvuur 2026";
    private const int Timeout = 180_000;
    private static readonly TimeSpan Wachttijd = TimeSpan.FromSeconds(20);
    private readonly string _datamap = Path.Combine(Path.GetTempPath(), "btw-boeker-ui", Guid.NewGuid().ToString("N"));
    private readonly UIA3Automation _automation = new();
    private FlauiApplicatie? _app;

    public AppUiTests() => Directory.CreateDirectory(_datamap);

    public void Dispose()
    {
        LaatToetsenLos();
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
    public async Task Start_zonder_database_toont_startpagina_en_menubalk()
    {
        await Task.Yield();
        BewaarInstellingen(new GebruikersInstellingen { StartwizardGetoond = true }); // geen automatische startwizard
        var venster = Start();

        Assert.Equal("BTW-Boeker", venster.Title);
        foreach (var menu in new[] { "Bestand", "Bewerken", "Beeld", "Modules", "Extra", "Help" })
        {
            Zoek(venster, $"Menu_{menu}");
        }

        Zoek(venster, "StartNieuw");
        Zoek(venster, "StartOpenen");
        Zoek(venster, "StartBackup");
        Bewaar(venster, "01-startpagina");

        var bestand = Zoek(venster, "Menu_Bestand").AsMenuItem();
        bestand.Expand();
        var importeren = Zoek(venster, "Menu_Importeren").AsMenuItem();
        importeren.Expand();
        Zoek(venster, "Menu_ImporteerSjabloon");
        importeren.Collapse();
        bestand.Collapse();

        Sluit(venster);
    }

    [Fact(Timeout = Timeout)]
    public async Task Laatste_database_openen_met_wachtwoord_en_modules_via_sneltoetsen()
    {
        var db = Path.Combine(_datamap, "btw-boeker.btwdb");
        await new DatabaseBeheer().MaakAanAsync(db, Wachtwoord);
        new JsonInstellingenOpslag(Path.Combine(_datamap, "instellingen.json"))
            .Bewaar(new GebruikersInstellingen { LaatsteDatabase = db });

        var venster = Start();

        VoerWachtwoordIn(venster); // het wachtwoordvenster verschijnt automatisch voor de laatst gebruikte database

        Assert.True(Retry.WhileFalse(() => venster.Title == "Vereniging - btw-boeker.btwdb - BTW-Boeker", Wachttijd).Result, venster.Title);
        Bewaar(venster, "02-overzicht");

        Sneltoets(venster, VirtualKeyShort.KEY_1); // Boekhouding (Vereniging)
        Assert.True(Retry.WhileFalse(() => venster.Title == "Vereniging - btw-boeker.btwdb - BTW-Boeker", Wachttijd).Result, Diagnose(venster));
        Zoek(venster, "AdministratieBand");
        Bewaar(venster, "03-boekhouding-vereniging");

        Sneltoets(venster, VirtualKeyShort.OEM_COMMA); // Instellingen
        Zoek(venster, "InstellingenOpslaan");
        Bewaar(venster, "04-instellingen");

        Sluit(venster);

        var bewaard = new JsonInstellingenOpslag(Path.Combine(_datamap, "instellingen.json")).Laad();
        Assert.Equal(Application.Modules.ModuleSoort.Instellingen, bewaard.LaatstePagina?.Module);
    }

    [Fact(Timeout = Timeout)]
    public async Task Eigen_titelbalk_maximaliseert_herstelt_en_toont_de_titel()
    {
        await Task.Yield(); // xUnit voert een test met Timeout alleen asynchroon uit
        BewaarInstellingen(new GebruikersInstellingen { StartwizardGetoond = true });
        var venster = Start();

        var maximaliseer = Zoek(venster, "VensterMaximaliseren").AsButton(); // besluit 221
        Zoek(venster, "VensterMinimaliseren");
        Zoek(venster, "VensterSluiten");
        maximaliseer.Invoke();
        Assert.True(Retry.WhileFalse(() => venster.Patterns.Window.Pattern.WindowVisualState.Value == WindowVisualState.Maximized, Wachttijd).Result);
        Assert.Equal("Herstellen", Zoek(venster, "VensterMaximaliseren").Name);
        Bewaar(venster, "17-gemaximaliseerd");

        Zoek(venster, "VensterMaximaliseren").AsButton().Invoke();
        Assert.True(Retry.WhileFalse(() => venster.Patterns.Window.Pattern.WindowVisualState.Value == WindowVisualState.Normal, Wachttijd).Result);
        Assert.Equal("BTW-Boeker", venster.Title);

        Sluit(venster);
    }

    /// <summary>
    /// Besluit 277, op een echte database: de teller volgt het regels toepassen zonder eerst naar Start te gaan, en de keuzelijst
    /// verklaart het verschil met het aantal van de gekozen rekening.
    /// </summary>
    [Fact(Timeout = Timeout)]
    public async Task Tellers_en_bankkeuze_volgen_het_regels_toepassen()
    {
        var db = Path.Combine(_datamap, "btw-boeker.btwdb");
        var beheer = new DatabaseBeheer();
        await beheer.MaakAanAsync(db, Wachtwoord);
        await using (var sessie = await beheer.OpenAsync(db, Wachtwoord))
        {
            const AdministratieSoort Ver = AdministratieSoort.Vereniging;
            var boekhouding = new BoekhoudingDienst(sessie, TimeProvider.System);
            var bank = new BankDienst(sessie, TimeProvider.System);
            await boekhouding.RichtInAsync(Ver);
            if ((await boekhouding.LaadAsync(Ver)).Boekjaar(2026) is null)
            {
                await boekhouding.OpenBoekjaarAsync(Ver, 2026);
            }

            foreach (var (nummer, naam, soort) in new[] { (1100, "Betaal", RekeningSoort.Balans), (1110, "Spaar", RekeningSoort.Balans), (4115, "Energie", RekeningSoort.Resultaat) })
            {
                await boekhouding.BewaarRekeningAsync(Ver, new RekeningInvoer(null, nummer, naam, null, soort, RekeningRol.Geen, true));
            }

            static CamtBestand Bestand(string iban, params VbTransactie[] t) =>
                Camt053Lezer.Lees(CamtVoorbeeld.Stroom(CamtVoorbeeld.Xml(iban, "08", new VbAfschrift(new DateOnly(2026, 3, 2), 0, t))));

            var stand = await boekhouding.LaadAsync(Ver);
            CamtBestand[] bestanden =
            [
                Bestand(CamtVoorbeeld.Betaal,
                    new VbTransactie(-4500, "ICDT", "ESCT", "520", Ref: "E1", Naam: "Energie BV"),
                    new VbTransactie(-1000, "ICDT", "ESCT", "520", Ref: "W1", Naam: "Waterbedrijf", NtryRef: "2")),
                Bestand(CamtVoorbeeld.Spaar, new VbTransactie(500, Ref: "R1", Naam: "Rente")),
            ];
            await bank.ImporteerAsync(Ver, BankimportPlanner.Plan(stand, bestanden), new Dictionary<string, NieuweBankrekening>
            {
                [CamtVoorbeeld.Betaal] = new(stand.Rekeningen.Single(r => r.Nummer == 1100).Id, "RABO", "Betaalrekening"),
                [CamtVoorbeeld.Spaar] = new(stand.Rekeningen.Single(r => r.Nummer == 1110).Id, "RSPA", "Spaarrekening"),
            });
            await bank.BewaarRegelAsync(Ver, new BoekingsregelInvoer(
                null, "Energie", stand.Rekeningen.Single(r => r.Nummer == 4115).Id, new BoekingsregelVoorwaarden(NaamBevat: "energie"), null, true));
        }

        new JsonInstellingenOpslag(Path.Combine(_datamap, "instellingen.json"))
            .Bewaar(new GebruikersInstellingen { LaatsteDatabase = db, StartwizardGetoond = true });
        var venster = Start();
        VoerWachtwoordIn(venster);
        Assert.True(Retry.WhileFalse(() => venster.Title == "Vereniging - btw-boeker.btwdb - BTW-Boeker", Wachttijd).Result, venster.Title);

        Sneltoets(venster, VirtualKeyShort.KEY_1); // Boekhouding (Vereniging)
        Zoek(venster, "BoekhoudingTabblad");
        Kies(venster, "Nav_Boekhouding.Bank");

        bool TellerIs(string sleutel, string tekst) =>
            venster.FindFirstDescendant(c => c.ByAutomationId(sleutel))?.Properties.HelpText.ValueOrDefault == tekst;
        bool KeuzeToont(string tekst)
        {
            // UI Automation toont de regels pas als de lijst open is.
            var keuze = Zoek(venster, "Bankrekeningkeuze").AsComboBox();
            keuze.Expand();
            try
            {
                return keuze.FindAllDescendants().Any(e => e.Properties.Name.ValueOrDefault == tekst);
            }
            finally
            {
                keuze.Collapse();
            }
        }

        Assert.True(Retry.WhileFalse(() => TellerIs("Nav_Boekhouding.Bank", "3 te doen"), Wachttijd).Result, "De teller begint niet op 3.");
        Assert.True(Retry.WhileFalse(() => KeuzeToont("Betaalrekening (2)"), Wachttijd).Result, "De keuzelijst toont niet 'Betaalrekening (2)'.");
        Assert.True(KeuzeToont("Spaarrekening (1)"), "De keuzelijst toont niet 'Spaarrekening (1)'.");
        Zoek(venster, "RegelsToepassen").AsButton().Invoke();

        Assert.True(Retry.WhileFalse(() => TellerIs("Nav_Boekhouding.Bank", "2 te doen"), Wachttijd).Result, "De teller van Bank liep niet mee.");
        Assert.True(Retry.WhileFalse(() => TellerIs("Nav_Boekhouding.Journaal", "1 te doen"), Wachttijd).Result, "Journaal kreeg geen teller voor het concept.");
        Assert.True(Retry.WhileFalse(() => KeuzeToont("Betaalrekening (1)"), Wachttijd).Result, "De keuzelijst liep niet mee.");
        Zoek(venster, "BoekhoudingTabblad"); // de pagina blijft staan

        Sluit(venster);
    }

    [Fact(Timeout = Timeout)]
    public async Task Meer_boekhouding_blijft_dicht_met_de_pijltjes_en_klapt_om_met_enter_en_spatie()
    {
        var db = Path.Combine(_datamap, "btw-boeker.btwdb");
        await new DatabaseBeheer().MaakAanAsync(db, Wachtwoord);
        new JsonInstellingenOpslag(Path.Combine(_datamap, "instellingen.json"))
            .Bewaar(new GebruikersInstellingen { LaatsteDatabase = db });

        var venster = Start();
        VoerWachtwoordIn(venster);
        Assert.True(Retry.WhileFalse(() => venster.Title == "Vereniging - btw-boeker.btwdb - BTW-Boeker", Wachttijd).Result, venster.Title);

        Sneltoets(venster, VirtualKeyShort.KEY_1); // Boekhouding (Vereniging)
        Zoek(venster, "BoekhoudingTabblad");
        var rapporten = Zoek(venster, "Nav_Boekhouding.Rapporten");
        Assert.True(Retry.WhileFalse(
            () =>
            {
                venster.SetForeground();
                rapporten.Focus();
                return HeeftEigenFocus("Nav_Boekhouding.Rapporten");
            },
            Wachttijd,
            TimeSpan.FromMilliseconds(500)).Result, "Geen focus op de navigatieregel. " + Diagnose(venster));

        TypOpRegel("Nav_Boekhouding.Rapporten", VirtualKeyShort.DOWN);
        Assert.True(Retry.WhileFalse(() => HeeftEigenFocus("Nav_Boekhouding.Meer"), Wachttijd).Result, Diagnose(venster));
        Thread.Sleep(400);
        Assert.Null(venster.FindFirstDescendant(c => c.ByAutomationId("Nav_Boekhouding.Boekingsregels")));
        Zoek(venster, "BoekhoudingTabblad");

        TypOpRegel("Nav_Boekhouding.Meer", VirtualKeyShort.ENTER);
        Zoek(venster, "Nav_Boekhouding.Boekingsregels");
        TypOpRegel("Nav_Boekhouding.Meer", VirtualKeyShort.SPACE);
        Assert.True(WachtTotWeg(venster, "Nav_Boekhouding.Boekingsregels"), "Spatie klapte Meer… niet in.");

        TypOpRegel("Nav_Boekhouding.Meer", VirtualKeyShort.SPACE);
        Zoek(venster, "Nav_Boekhouding.Boekingsregels");
        TypOpRegel("Nav_Boekhouding.Meer", VirtualKeyShort.DOWN);
        Assert.True(Retry.WhileFalse(() => Zoek(venster, "Nav_Boekhouding.Boekingsregels").AsListBoxItem().IsSelected, Wachttijd).Result, Diagnose(venster));

        Sluit(venster);
    }

    [Fact(Timeout = Timeout)]
    public async Task Boekhouding_leeg_beginnen_toont_subtabbladen_en_nieuwe_post()
    {
        var db = Path.Combine(_datamap, "btw-boeker.btwdb");
        await new DatabaseBeheer().MaakAanAsync(db, Wachtwoord);
        new JsonInstellingenOpslag(Path.Combine(_datamap, "instellingen.json"))
            .Bewaar(new GebruikersInstellingen { LaatsteDatabase = db });

        var venster = Start();
        VoerWachtwoordIn(venster);
        Assert.True(Retry.WhileFalse(() => venster.Title == "Vereniging - btw-boeker.btwdb - BTW-Boeker", Wachttijd).Result, venster.Title);

        Sneltoets(venster, VirtualKeyShort.KEY_1); // Boekhouding (Vereniging)
        Zoek(venster, "BoekhoudingTabblad");
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

        foreach (var onderdeel in new[] { "Journaal", "Bank", "Postvak", "Rapporten" })
        {
            Zoek(venster, $"Nav_Boekhouding.{onderdeel}"); // besluit 178
        }

        KlikOp(venster, "Nav_Boekhouding.Meer"); // besluit 269: inrichten onder Meer…
        foreach (var onderdeel in new[] { "Boekingsregels", "Relaties", "Rekeningschema", "Grootboekkaart", "Boekjaren" })
        {
            Zoek(venster, $"Nav_Boekhouding.{onderdeel}");
        }

        Zoek(venster, "NieuwePost").AsButton().Invoke();
        Zoek(venster, "PostEditor");
        Zoek(venster, "PostOmschrijving");
        Bewaar(venster, "11-boekhouding-nieuwe-post");
        Kies(venster, "Nav_Boekhouding.Bank");
        Zoek(venster, "BankInlezen");
        Bewaar(venster, "13-boekhouding-bank");
        Kies(venster, "Nav_Boekhouding.Boekingsregels");
        Zoek(venster, "Boekingsregels");
        Kies(venster, "Nav_Boekhouding.Relaties");
        Zoek(venster, "NieuweRelatie").AsButton().Invoke();
        Zoek(venster, "RelatieNaam");
        Kies(venster, "Nav_Boekhouding.Rapporten");
        Zoek(venster, "Rapport");
        Bewaar(venster, "12-boekhouding-rapport");

        Sluit(venster);
    }

    [Fact(Timeout = Timeout)]
    public async Task Bijlage_bekijken_toont_de_pdf_in_het_voorbeeldvenster()
    {
        var db = Path.Combine(_datamap, "btw-boeker.btwdb");
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
        VoerWachtwoordIn(venster);
        Assert.True(Retry.WhileFalse(() => venster.Title == "Vereniging - btw-boeker.btwdb - BTW-Boeker", Wachttijd).Result, venster.Title);

        Sneltoets(venster, VirtualKeyShort.KEY_1); // Boekhouding (Vereniging)
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
        Assert.Equal("BTW-Boeker instellen", wizard.Title);
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

    /// <summary>Besluit 179: in Eenvoudig is de menubalk weg; Alt toont hem tijdelijk. De wisselaar en de navigatiebalk staan links.</summary>
    [Fact(Timeout = Timeout)]
    public async Task Eenvoudig_verbergt_de_menubalk_en_alt_toont_hem_tijdelijk()
    {
        var db = Path.Combine(_datamap, "btw-boeker.btwdb");
        await new DatabaseBeheer().MaakAanAsync(db, Wachtwoord);
        BewaarInstellingen(new GebruikersInstellingen { LaatsteDatabase = db, Weergave = Weergavemodus.Eenvoudig, StartwizardGetoond = true });

        var venster = Start();
        VoerWachtwoordIn(venster);
        Assert.True(Retry.WhileFalse(() => venster.Title == "Vereniging - btw-boeker.btwdb - BTW-Boeker", Wachttijd).Result, venster.Title);

        Zoek(venster, "Administratiewisselaar");
        Zoek(venster, "Startkaarten");
        // Een ingeklapt element staat niet in de UI Automation-boom.
        Assert.Null(venster.FindFirstDescendant(c => c.ByAutomationId("Menubalk")));
        Bewaar(venster, "16-eenvoudig-zonder-menubalk");

        // Zoals Sneltoets: Alt pas sturen als de toetsenbordfocus aantoonbaar in dit app-proces ligt (in 1 van 6 runs kwam hij
        // anders niet aan); bij falen een schermafbeelding en Diagnose().
        if (!Retry.WhileFalse(() => { venster.SetForeground(); return HeeftEigenFocus(); }, Wachttijd, TimeSpan.FromMilliseconds(500)).Result)
        {
            throw new Xunit.Sdk.XunitException("De app kreeg de voorgrond niet; Alt is niet verstuurd. " + Diagnose(venster));
        }

        Keyboard.Type(VirtualKeyShort.ALT);
        ZoekMetDiagnose(venster, "Menubalk");
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        Assert.True(Retry.WhileFalse(() => venster.FindFirstDescendant(c => c.ByAutomationId("Menubalk")) is null, Wachttijd).Result,
            "De tijdelijke menubalk verdween niet na Escape.");

        Kies(venster, "Nav_Boekhouding");
        Zoek(venster, "Nav_Boekhouding.Bank");
        Sluit(venster);
    }

    /// <summary>
    /// Besluit 252: schermafbeeldingen voor de website, uit een demodatabase met verzonnen gegevens, in licht en donker.
    /// Draait alleen als <c>BTWBOEKER_WEBSITE_SCHERMAFBEELDINGEN</c> naar een map wijst (de release-workflow zet dat).
    /// </summary>
    [Fact(Timeout = Timeout * 2)]
    [Trait("Categorie", "Schermafbeeldingen")]
    public async Task Schermafbeeldingen_voor_de_website_uit_de_demodatabase()
    {
        var uitvoer = Environment.GetEnvironmentVariable("BTWBOEKER_WEBSITE_SCHERMAFBEELDINGEN");
        if (string.IsNullOrWhiteSpace(uitvoer))
        {
            return;
        }

        Directory.CreateDirectory(uitvoer);
        var db = Path.Combine(_datamap, "voorbeeldgroep.btwdb");
        await Demodatabase.MaakAsync(db, DateTimeOffset.Now);

        foreach (var (thema, naam) in new[] { (Thema.Licht, "licht"), (Thema.Donker, "donker") })
        {
            BewaarInstellingen(new GebruikersInstellingen { LaatsteDatabase = db, Thema = thema, StartwizardGetoond = true });
            var venster = Start();
            VoerWachtwoordIn(venster, Demodatabase.Wachtwoord);
            Assert.True(
                Retry.WhileFalse(() => venster.Title == $"{Demodatabase.NaamVereniging} - voorbeeldgroep.btwdb - BTW-Boeker", Wachttijd).Result,
                venster.Title);
            ZetVenstergrootte(venster);

            Zoek(venster, "Startkaarten");
            Schermafbeelding(venster, uitvoer, $"start-{naam}");
            Kies(venster, "Nav_Boekhouding");
            Zoek(venster, "Journaalposten");
            Schermafbeelding(venster, uitvoer, $"boekhouding-{naam}");
            Kies(venster, "Nav_Boekhouding.Rapporten");
            Zoek(venster, "Rapport");
            Schermafbeelding(venster, uitvoer, $"rapport-{naam}");
            Sluit(venster);
            _app!.Dispose();
            _app = null;
        }
    }

    /// <summary>
    /// Gemaximaliseerd, zodat het venster altijd binnen het scherm valt. Op de GitHub-runner is het scherm 1024×768: een
    /// vaste grootte van 1280×800 viel daar rechts buiten beeld en onder de taakbalk (release v0.15.0).
    /// </summary>
    private static void ZetVenstergrootte(Window venster)
    {
        if (venster.Patterns.Window.IsSupported)
        {
            venster.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
            Retry.WhileFalse(() => venster.Patterns.Window.Pattern.WindowVisualState.Value == WindowVisualState.Maximized, Wachttijd);
        }
    }

    private static void Schermafbeelding(Window venster, string map, string naam)
    {
        Thread.Sleep(600); // Animaties van het Fluent-thema en het laden van de pagina laten uitlopen.
        Capture.Element(venster).ToFile(Path.Combine(map, $"btw-boeker-{naam}.png"));
    }

    private static void Kies(AutomationElement venster, string automationId) => Zoek(venster, automationId).AsListBoxItem().Select();

    /// <summary>Een echte muisklik. <i>Meer…</i> klapt alleen om op een bewuste actie, niet op een selectie (besluit 273).</summary>
    private static void KlikOp(Window venster, string automationId)
    {
        venster.SetForeground();
        Zoek(venster, automationId).Click();
    }

    /// <summary>Typt alleen als de focus op deze regel van dit app-proces ligt; zo komen de toetsen nooit in een ander venster.</summary>
    private void TypOpRegel(string automationId, VirtualKeyShort toets)
    {
        Assert.True(Retry.WhileFalse(() => HeeftEigenFocus(automationId), Wachttijd).Result, $"Geen focus op {automationId}.");
        Keyboard.Type(toets);
    }

    private static bool WachtTotWeg(AutomationElement venster, string automationId) =>
        Retry.WhileFalse(() => venster.FindFirstDescendant(c => c.ByAutomationId(automationId)) is null, Wachttijd).Result;

    /// <summary>
    /// Typt het wachtwoord alleen als het wachtwoordveld van déze app de toetsenbordfocus heeft. Zonder voorgrond (Windows
    /// foreground lock) kwamen de toetsen elders terecht; nu stopt de test dan zonder te typen, zodat het testwachtwoord
    /// nooit in een ander venster belandt.
    /// </summary>
    private void VoerWachtwoordIn(Window venster, string wachtwoord = Wachtwoord)
    {
        var dialoog = Retry.WhileNull(() => venster.ModalWindows.FirstOrDefault(), Wachttijd).Result
            ?? throw new Xunit.Sdk.XunitException("Het wachtwoordvenster verscheen niet. " + Diagnose(venster));
        Assert.Equal("Database openen", dialoog.Title);
        var veld = Zoek(dialoog, "Wachtwoord");
        var gefocust = Retry.WhileFalse(
            () =>
            {
                venster.SetForeground();
                dialoog.SetForeground();
                veld.Focus();
                return HeeftEigenFocus("Wachtwoord");
            },
            Wachttijd,
            TimeSpan.FromMilliseconds(500)).Result;
        if (!gefocust)
        {
            throw new Xunit.Sdk.XunitException("Het wachtwoordveld kreeg geen toetsenbordfocus; er is niets getypt. " + Diagnose(venster));
        }

        Keyboard.Type(wachtwoord);
        Keyboard.Type(VirtualKeyShort.ENTER);
    }

    /// <summary>
    /// Vangnet: een toets die nog ingedrukt staat (bijvoorbeeld Alt) maakt van elke volgende toets een sneltoets, ook in
    /// volgende tests. <c>Keyboard.Press</c> drukt alleen in; deze tests gebruiken daarom <c>Keyboard.Type</c> (in en uit).
    /// </summary>
    private static void LaatToetsenLos()
    {
        foreach (var toets in new[] { VirtualKeyShort.ALT, VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.ESCAPE, VirtualKeyShort.ENTER })
        {
            Keyboard.Release(toets);
        }
    }

    /// <summary>De systeembrede focus ligt in dit app-proces (en, als opgegeven, op het element met deze AutomationId).</summary>
    private bool HeeftEigenFocus(string? automationId = null)
    {
        var focus = _automation.FocusedElement();
        return focus is not null && focus.Properties.ProcessId.ValueOrDefault == _app!.ProcessId
            && (automationId is null || focus.Properties.AutomationId.ValueOrDefault == automationId);
    }

    private Window Start()
    {
        var info = new ProcessStartInfo(ExePad()) { UseShellExecute = false };
        info.Environment["BTW_BOEKER_DATAMAP"] = _datamap;
        _app = FlauiApplicatie.Launch(info);
        var venster = _app.GetMainWindow(_automation, Wachttijd);
        Assert.NotNull(venster);
        venster.SetForeground();
        return venster;
    }

    private static AutomationElement Zoek(AutomationElement bron, string automationId) =>
        Retry.WhileNull(() => bron.FindFirstDescendant(c => c.ByAutomationId(automationId)), Wachttijd).Result
        ?? throw new Xunit.Sdk.XunitException($"Element '{automationId}' niet gevonden.");

    /// <summary>
    /// Zoals <see cref="Zoek"/>, maar bij falen altijd een schermafbeelding (in %TEMP%\btw-boeker-ui-fouten, of in
    /// BTWBOEKER_SCREENSHOTS) en de diagnose plus een eventuele foutmelding van de pagina in de melding.
    /// </summary>
    private AutomationElement ZoekMetDiagnose(Window venster, string automationId, string? foutmeldingId = null)
    {
        var element = Retry.WhileNull(() => venster.FindFirstDescendant(c => c.ByAutomationId(automationId)), Wachttijd).Result;
        if (element is not null)
        {
            return element;
        }

        var map = Environment.GetEnvironmentVariable("BTWBOEKER_SCREENSHOTS") ?? Path.Combine(Path.GetTempPath(), "btw-boeker-ui-fouten"); // blijft na de test bestaan
        var bestand = Path.Combine(map, $"fout-{automationId}-{DateTime.Now:HHmmss}.png");
        try
        {
            Directory.CreateDirectory(map);
            Capture.Element(venster).ToFile(bestand);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            bestand = "(geen schermafbeelding: " + ex.Message + ")";
        }

        var fout = foutmeldingId is null ? null : venster.FindFirstDescendant(c => c.ByAutomationId(foutmeldingId))?.Name;
        throw new Xunit.Sdk.XunitException(
            $"Element '{automationId}' niet gevonden. {Diagnose(venster)}; foutmelding pagina: '{fout ?? "(geen)"}'; schermafbeelding: {bestand}");
    }

    private string Diagnose(Window venster)
    {
        var focus = _automation.FocusedElement();
        return $"Titel: '{venster.Title}'; focus: {focus?.ControlType} '{focus?.Name}' (proces {focus?.Properties.ProcessId.ValueOrDefault}, app {_app?.ProcessId})";
    }

    /// <summary>Toetsenbordinvoer gaat alleen naar de app als die de voorgrond heeft en niet in de menumodus staat.</summary>
    private void Sneltoets(Window venster, VirtualKeyShort toets)
    {
        EindigMenumodus(venster);
        if (!Retry.WhileFalse(() => { venster.SetForeground(); return HeeftEigenFocus(); }, Wachttijd, TimeSpan.FromMilliseconds(500)).Result)
        {
            throw new Xunit.Sdk.XunitException("De app kreeg de voorgrond niet; de sneltoets is niet verstuurd. " + Diagnose(venster));
        }

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
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            Thread.Sleep(200);
        }

        Assert.True(_automation.FocusedElement()?.ControlType != ControlType.MenuItem, "De menumodus eindigt niet. " + Diagnose(venster));
    }

    private void Sluit(Window venster)
    {
        EindigMenumodus(venster);
        Assert.True(
            Retry.WhileFalse(() => { venster.SetForeground(); return HeeftEigenFocus(); }, Wachttijd, TimeSpan.FromMilliseconds(500)).Result,
            "De app kreeg de voorgrond niet; sluiten is afgebroken. " + Diagnose(venster));
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
        var map = Environment.GetEnvironmentVariable("BTWBOEKER_SCREENSHOTS");
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
        // tests/BtwBoeker.App.Tests/bin/<Configuratie>/<tfm>/ -> src/BtwBoeker.App/bin/<Configuratie>/<tfm>/ (nu net10.0-windows10.0.19041.0)
        var testmap = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuratie = testmap.Parent!.Name;
        var root = testmap.Parent.Parent!.Parent!.Parent!.Parent!.FullName;
        var pad = Path.Combine(root, "src", "BtwBoeker.App", "bin", configuratie, testmap.Name, "BTW-Boeker.exe");
        Assert.True(File.Exists(pad), $"BTW-Boeker.exe niet gevonden op {pad}. Bouw eerst de oplossing.");
        return pad;
    }
}
