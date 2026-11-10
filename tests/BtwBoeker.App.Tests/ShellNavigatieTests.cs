using Microsoft.Extensions.Logging.Abstractions;
using BtwBoeker.App.Diensten;
using BtwBoeker.App.ViewModels;
using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.Application.Instellingen;
using BtwBoeker.Application.Modules;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.App.Tests;

/// <summary>Herinrichting v0.8.0: navigatiebalk, administratiewisselaar, statusregel en menubalk.</summary>
public sealed class ShellNavigatieTests
{
    private const string Pad = @"C:\Data\btw-boeker.btwdb";
    private const string Wachtwoord = "geheim wachtwoord";

    private readonly FakeDatabaseBeheer _beheer = new();
    private readonly FakeDialoogService _dialogen = new();
    private readonly InMemoryInstellingenOpslag _instellingen = new() { Instellingen = new GebruikersInstellingen { StartwizardGetoond = true } };

    private ShellViewModel MaakShell() =>
        new(_beheer, _instellingen, _dialogen, new FakeThemaService(), FakeBoekhouding.Hulpmiddelen(),
            new VasteTijd(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero)), NullLogger<ShellViewModel>.Instance);

    private async Task<ShellViewModel> OpenAsync()
    {
        _beheer.Voeg(Pad, Wachtwoord);
        _instellingen.Instellingen = _instellingen.Instellingen with { LaatsteDatabase = Pad };
        _dialogen.WachtwoordAntwoorden.Enqueue(new WachtwoordInvoer(Wachtwoord, HerstelGevraagd: false));
        var shell = MaakShell();
        await shell.InitialiseerAsync();
        Assert.True(shell.IsDatabaseOpen);
        return shell;
    }

    private static NavigatieItem Item(ShellViewModel shell, string sleutel) => Assert.Single(shell.Navigatie, n => n.Sleutel == sleutel);

    private static async Task Kies(ShellViewModel shell, string sleutel)
    {
        shell.GeselecteerdeNavigatie = Item(shell, sleutel);
        if (shell.GeselecteerdTabblad is BoekhoudingViewModel { VernieuwCommand.ExecutionTask: { } laden })
        {
            await laden;
        }
    }

    [Fact]
    public async Task Zonder_database_staat_alleen_start_in_de_navigatie()
    {
        var shell = MaakShell();
        await shell.InitialiseerAsync();

        Assert.Equal(["Start"], shell.Navigatie.Select(n => n.Sleutel));
        Assert.Null(shell.GekozenAdministratie);
    }

    [Fact]
    public async Task Na_openen_staan_de_modules_met_ingeklapte_onderdelen_en_is_de_vereniging_gekozen()
    {
        var shell = await OpenAsync();

        Assert.Equal(["Start", "Boekhouding", "Instellingen"], shell.Navigatie.Select(n => n.Sleutel));
        Assert.Equal(AdministratieSoort.Vereniging, shell.GekozenAdministratie!.Soort);
        Assert.Same(Item(shell, "Start"), shell.GeselecteerdeNavigatie);
        Assert.IsType<OverzichtViewModel>(shell.GeselecteerdTabblad);
    }

    [Fact]
    public async Task Boekhouding_toont_het_dagelijkse_werk_en_de_rest_onder_meer()
    {
        var shell = await OpenAsync();

        await Kies(shell, "Boekhouding");

        Assert.Equal(["Boekhouding.Journaal", "Boekhouding.Bank", "Boekhouding.Postvak", "Boekhouding.Rapporten", "Boekhouding.Meer"],
            shell.Navigatie.Where(n => n.Module == ModuleSoort.Boekhouding && n.IsSubitem).Select(n => n.Sleutel));
        var meer = Item(shell, "Boekhouding.Meer");
        Assert.True(meer.IsMeer);
        Assert.False(meer.IsUitgeklapt);
        var pagina = shell.GeselecteerdTabblad;

        shell.MeerWisselenCommand.Execute(meer); // uitklappen, zonder van pagina te wisselen

        Assert.True(meer.IsUitgeklapt);
        Assert.Same(pagina, shell.GeselecteerdTabblad);
        Assert.Same(Item(shell, "Boekhouding.Journaal"), shell.GeselecteerdeNavigatie);
        Assert.True(Item(shell, "Boekhouding.Rekeningschema").IsDiep);
        Assert.Equal(["Boekingsregels", "Relaties", "Rekeningschema", "Grootboekkaart", "Boekjaren"],
            shell.Navigatie.Where(n => n.IsDiep && n.Module == ModuleSoort.Boekhouding).Select(n => n.Sleutel.Split('.')[1]));

        shell.MeerWisselenCommand.Execute(meer); // weer inklappen
        Assert.DoesNotContain(shell.Navigatie, n => n.Sleutel == "Boekhouding.Rekeningschema");
    }

    [Fact]
    public async Task Meer_selecteren_klapt_niet_om_en_de_selectie_gaat_terug_naar_de_pagina()
    {
        var shell = await OpenAsync();
        await Kies(shell, "Boekhouding");
        var pagina = shell.GeselecteerdTabblad;
        var meer = Item(shell, "Boekhouding.Meer");

        // WPF selecteert soms twee keer (bij focus en door de actie zelf); pijltjes mogen niets omklappen.
        foreach (var _ in Enumerable.Range(0, 2))
        {
            shell.GeselecteerdeNavigatie = meer;

            Assert.False(meer.IsUitgeklapt);
            Assert.DoesNotContain(shell.Navigatie, n => n.Sleutel == "Boekhouding.Boekingsregels");
            Assert.Same(pagina, shell.GeselecteerdTabblad);
            Assert.Same(Item(shell, "Boekhouding.Journaal"), shell.GeselecteerdeNavigatie);
        }
    }

    [Fact]
    public async Task Meer_wisselen_klapt_precies_een_keer_om_en_negeert_een_andere_regel()
    {
        var shell = await OpenAsync();
        await Kies(shell, "Boekhouding");
        var meer = Item(shell, "Boekhouding.Meer");

        shell.MeerWisselenCommand.Execute(Item(shell, "Boekhouding.Bank")); // geen Meer…-regel
        Assert.False(meer.IsUitgeklapt);

        shell.MeerWisselenCommand.Execute(meer);
        Assert.True(meer.IsUitgeklapt);
        Assert.Same(Item(shell, "Boekhouding.Journaal"), shell.GeselecteerdeNavigatie);
        Item(shell, "Boekhouding.Boekingsregels");

        shell.MeerWisselenCommand.Execute(meer);
        Assert.False(meer.IsUitgeklapt);
        Assert.DoesNotContain(shell.Navigatie, n => n.Sleutel == "Boekhouding.Boekingsregels");
    }

    [Fact]
    public async Task Een_pagina_onder_meer_klapt_meer_vanzelf_uit()
    {
        var shell = await OpenAsync();
        await Kies(shell, "Boekhouding");
        var boekhouding = Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdTabblad);

        boekhouding.Subtab = BoekhoudingSubtab.Rekeningschema; // bijvoorbeeld via het menu

        Assert.True(Item(shell, "Boekhouding.Meer").IsUitgeklapt);
        Assert.Same(Item(shell, "Boekhouding.Rekeningschema"), shell.GeselecteerdeNavigatie);
    }

    [Fact]
    public async Task Start_vernieuwen_laadt_de_boekhouding_maar_een_keer()
    {
        var shell = await OpenAsync();
        var boekhouding = (InMemoryBoekhoudingOpslag)_beheer.LaatsteSessie!.Boekhouding;
        await Task.Delay(50); // het vernieuwen na openen loopt op de achtergrond
        boekhouding.KeerGeladen = 0;

        shell.OpenModuleCommand.Execute(new ModuleVerzoek(ModuleSoort.Overzicht, null)); // vernieuwt Start
        await Task.Delay(50);

        // Kaarten, inrichtingsstatus en kerncijfers samen: één keer laden.
        Assert.Equal(1, boekhouding.KeerGeladen);
    }

    [Fact]
    public async Task Een_onderdeel_kiezen_klapt_de_module_uit_en_opent_het_subtabblad()
    {
        var shell = await OpenAsync();

        await Kies(shell, "Boekhouding"); // uitklappen en naar het huidige onderdeel (Journaal)
        Assert.Same(Item(shell, "Boekhouding.Journaal"), shell.GeselecteerdeNavigatie);
        Assert.True(Item(shell, "Boekhouding.Bank").IsSubitem);

        await Kies(shell, "Boekhouding.Bank");

        var boekhouding = Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdTabblad);
        Assert.Equal(BoekhoudingSubtab.Bank, boekhouding.Subtab);
        Assert.Equal(AdministratieSoort.Vereniging, boekhouding.Administratie!.Soort);
        Assert.Equal("Bank", Item(shell, "Boekhouding.Bank").Titel); // zonder toegangstoets
    }

    /// <summary>
    /// Uitklappen werkt de lijst bij in plaats van hem opnieuw op te bouwen: de gekozen regel blijft hetzelfde element, zodat
    /// focus en schermlezer (en UI Automation) niet hun element kwijtraken.
    /// </summary>
    [Fact]
    public async Task Uitklappen_houdt_de_bestaande_regels_en_voegt_alleen_onderdelen_toe()
    {
        var shell = await OpenAsync();
        var boekhouding = Item(shell, "Boekhouding");
        await Kies(shell, "Boekhouding");

        Assert.Same(boekhouding, Item(shell, "Boekhouding"));
        Assert.True(boekhouding.IsUitgeklapt);
        Assert.Equal(Item(shell, "Boekhouding.Journaal"), shell.Navigatie[shell.Navigatie.IndexOf(boekhouding) + 1]);

        await Kies(shell, "Instellingen");
        Assert.Same(boekhouding, Item(shell, "Boekhouding"));
        Assert.False(boekhouding.IsUitgeklapt);
        Assert.DoesNotContain(shell.Navigatie, n => n.IsSubitem);
    }

    [Fact]
    public async Task Sneltoets_opent_de_module_bij_de_gekozen_administratie_en_markeert_de_navigatie()
    {
        var shell = await OpenAsync();

        shell.OpenModuleViaSneltoetsCommand.Execute("1");

        var boekhouding = Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdTabblad);
        Assert.Equal(AdministratieSoort.Vereniging, boekhouding.Administratie!.Soort);
        Assert.Same(Item(shell, "Boekhouding.Journaal"), shell.GeselecteerdeNavigatie);
    }

    [Fact]
    public async Task Laatste_pagina_en_administratie_worden_na_herstart_hersteld()
    {
        var shell = await OpenAsync();
        await Kies(shell, "Boekhouding");
        await Kies(shell, "Boekhouding.Rapporten");
        Assert.True(await shell.MagAfsluitenAsync());

        Assert.Equal(new LaatstePagina(ModuleSoort.Boekhouding, AdministratieSoort.Vereniging, (int)BoekhoudingSubtab.Rapporten), _instellingen.Instellingen.LaatstePagina);
        var tweede = await OpenAsync();

        var boekhouding = Assert.IsType<BoekhoudingViewModel>(tweede.GeselecteerdTabblad);
        Assert.Equal(AdministratieSoort.Vereniging, tweede.GekozenAdministratie!.Soort);
        Assert.Equal(BoekhoudingSubtab.Rapporten, boekhouding.Subtab);
    }

    [Fact]
    public async Task Volgende_en_vorige_pagina_lopen_door_de_navigatie()
    {
        var shell = await OpenAsync();

        shell.VolgendePaginaCommand.Execute(null);
        Assert.Same(Item(shell, "Boekhouding.Journaal"), shell.GeselecteerdeNavigatie);
        shell.VorigePaginaCommand.Execute(null);
        Assert.Same(Item(shell, "Start"), shell.GeselecteerdeNavigatie);
        shell.VorigePaginaCommand.Execute(null);
        Assert.Same(shell.Navigatie[^1], shell.GeselecteerdeNavigatie);
    }

    [Fact]
    public async Task Statusregel_toont_melding_en_fout_van_de_pagina_met_een_teken()
    {
        var shell = await OpenAsync();
        await Kies(shell, "Boekhouding");
        var boekhouding = Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdTabblad);
        await boekhouding.LeegBeginnenCommand.ExecuteAsync(null);

        await boekhouding.ConceptenBoekenCommand.ExecuteAsync(null); // geen concepten: een melding

        Assert.Equal(Statussoort.Gelukt, shell.Statussoort);
        Assert.Equal("✓", shell.Statusteken);
        Assert.Equal(boekhouding.Melding, shell.Statusmelding);
    }

    [Fact]
    public async Task Statusregel_toont_een_waarschuwing_als_een_halve_boekingsregel_niet_is_opgeslagen()
    {
        var shell = await OpenAsync();
        await Kies(shell, "Boekhouding");
        var boekhouding = Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdTabblad);
        await boekhouding.LeegBeginnenCommand.ExecuteAsync(null);
        boekhouding.NieuweRegelCommand.Execute(null);
        boekhouding.NieuweRekeningCommand.Execute(null);
        var rekening = boekhouding.Rekeningen.Single(r => r.IsNieuw);
        rekening.Nummertekst = "4220";
        rekening.Naam = "Huur Terrein";

        await shell.OpslaanCommand.ExecuteAsync(null);

        Assert.Equal(Statussoort.Waarschuwing, shell.Statussoort); // niet "Opgeslagen."
        Assert.Equal("⚠", shell.Statusteken);
        Assert.Equal(boekhouding.Waarschuwing, shell.Statusmelding);
        Assert.Contains("1 boekingsregel nog niet", shell.Statusmelding, StringComparison.Ordinal);
    }

    private static async Task Wacht(Func<bool> voorwaarde)
    {
        for (var i = 0; i < 100 && !voorwaarde(); i++)
        {
            await Task.Delay(50);
        }

        Assert.True(voorwaarde());
    }

    private static async Task<BoekhoudingViewModel> OpenLegeBoekhoudingMetRekening(ShellViewModel shell)
    {
        await Kies(shell, "Boekhouding");
        var boekhouding = Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdTabblad);
        await boekhouding.LeegBeginnenCommand.ExecuteAsync(null);
        boekhouding.NieuweRekeningCommand.Execute(null);
        var rekening = boekhouding.Rekeningen.Single(r => r.IsNieuw);
        rekening.Nummertekst = "4115";
        rekening.Naam = "Energie";
        Assert.True(await boekhouding.BewaarAsync());
        return boekhouding;
    }

    [Fact]
    public async Task Tellers_volgen_een_opslag_zonder_eerst_naar_start_te_gaan()
    {
        var shell = await OpenAsync();
        var boekhouding = await OpenLegeBoekhoudingMetRekening(shell);
        Assert.Null(Item(shell, "Boekhouding.Journaal").Teller);

        boekhouding.NieuwePostCommand.Execute(null);
        boekhouding.Editor!.Omschrijving = "Concept";
        if (boekhouding.Editor.Regels.Count == 0)
        {
            boekhouding.Editor.NieuweRegelCommand.Execute(null);
        }

        boekhouding.Editor.Regels[0].GekozenRekening = boekhouding.Editor.Rekeningen.Single(r => r.Nummer == 4115);
        boekhouding.Editor.Regels[0].Debettekst = "10,00";
        Assert.True(await boekhouding.BewaarAsync());

        _beheer.LaatsteSessie!.MeldWijziging(); // De echte sessie doet dit bij elke opslag

        await Wacht(() => Item(shell, "Boekhouding.Journaal").Teller == 1);
        Assert.Equal(1, Item(shell, "Boekhouding").Teller);
        Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdTabblad); // de pagina blijft staan
    }

    [Fact]
    public async Task Meerdere_opslagen_kort_na_elkaar_geven_een_herberekening()
    {
        var shell = await OpenAsync();
        await OpenLegeBoekhoudingMetRekening(shell);
        var opslag = (InMemoryBoekhoudingOpslag)_beheer.LaatsteSessie!.Boekhouding;
        await Task.Delay(800); // lopende vernieuwingen na het openen laten uitlopen
        opslag.KeerGeladen = 0;

        foreach (var _ in Enumerable.Range(0, 5))
        {
            _beheer.LaatsteSessie.MeldWijziging();
            await Task.Delay(20);
        }

        await Wacht(() => opslag.KeerGeladen > 0);
        await Task.Delay(800);
        Assert.Equal(1, opslag.KeerGeladen); // één keer, en het laden zelf meldt geen wijziging
    }

    [Fact]
    public async Task Na_sluiten_luistert_de_app_niet_meer_naar_de_sessie()
    {
        var shell = await OpenAsync();
        var sessie = _beheer.LaatsteSessie!;
        Assert.True(sessie.HeeftLuisteraar);

        await shell.SluitDatabaseCommand.ExecuteAsync(null);

        Assert.False(sessie.HeeftLuisteraar);
    }

    [Fact]
    public async Task Menubalk_in_eenvoudig_alleen_tijdelijk_via_alt()
    {
        var shell = await OpenAsync();
        Assert.True(shell.IsMenubalkZichtbaar);

        shell.KiesWeergaveCommand.Execute(Weergavemodus.Eenvoudig);
        Assert.False(shell.IsMenubalkZichtbaar);
        shell.ToonMenubalkTijdelijk();
        Assert.True(shell.IsMenubalkZichtbaar);
        shell.VerbergTijdelijkeMenubalk();
        Assert.False(shell.IsMenubalkZichtbaar);
    }
}
