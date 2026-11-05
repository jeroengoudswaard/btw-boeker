using Microsoft.Extensions.Logging.Abstractions;
using Verenigd.App.Diensten;
using Verenigd.App.ViewModels;
using Verenigd.App.ViewModels.Boekhouding;
using Verenigd.App.ViewModels.Contributie;
using Verenigd.Application.Instellingen;
using Verenigd.Application.Modules;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.Tests;

/// <summary>Herinrichting v0.8.0 (besluiten 168 en 177–183): navigatiebalk, administratiewisselaar, statusregel en menubalk.</summary>
public sealed class ShellNavigatieTests
{
    private const string Pad = @"C:\Data\verenigd.vdb";
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

        Assert.Equal(["Start", "Leden", "Contributie", "Boekhouding", "Begroting", "Email", "Webapp", "Instellingen"], shell.Navigatie.Select(n => n.Sleutel));
        Assert.Equal(AdministratieSoort.Vereniging, shell.GekozenAdministratie!.Soort);
        Assert.Same(Item(shell, "Start"), shell.GeselecteerdeNavigatie);
        Assert.IsType<OverzichtViewModel>(shell.GeselecteerdTabblad);
    }

    [Fact]
    public async Task Webapp_zonder_koppeling_toont_alleen_koppelen()
    {
        var shell = await OpenAsync();

        await Kies(shell, "Webapp");

        Assert.IsType<ViewModels.Webapp.WebappViewModel>(shell.GeselecteerdTabblad);
        Assert.Equal("Koppelen", Item(shell, "Webapp.Synchroniseren").Titel); // besluit 269: alleen wat nu nodig is
        Assert.DoesNotContain(shell.Navigatie, n => n.Sleutel is "Webapp.Voorstellen" or "Webapp.Leiding" or "Webapp.Meer");
    }

    [Fact]
    public async Task Boekhouding_toont_het_dagelijkse_werk_en_de_rest_onder_meer()
    {
        var shell = await OpenAsync();

        await Kies(shell, "Boekhouding");

        Assert.Equal(["Boekhouding.Journaal", "Boekhouding.Bank", "Boekhouding.Postvak", "Boekhouding.Facturen", "Boekhouding.Rapporten", "Boekhouding.Meer"],
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
        await Kies(shell, "Contributie");
        var pagina = shell.GeselecteerdTabblad;
        var meer = Item(shell, "Contributie.Meer");

        // Besluit 273: WPF selecteert soms twee keer (bij focus en door de actie zelf); pijltjes mogen niets omklappen.
        foreach (var _ in Enumerable.Range(0, 2))
        {
            shell.GeselecteerdeNavigatie = meer;

            Assert.False(meer.IsUitgeklapt);
            Assert.DoesNotContain(shell.Navigatie, n => n.Sleutel == "Contributie.Betalers");
            Assert.Same(pagina, shell.GeselecteerdTabblad);
            Assert.Same(Item(shell, "Contributie.Leden"), shell.GeselecteerdeNavigatie);
        }
    }

    [Fact]
    public async Task Meer_wisselen_klapt_precies_een_keer_om_en_negeert_een_andere_regel()
    {
        var shell = await OpenAsync();
        await Kies(shell, "Contributie");
        var meer = Item(shell, "Contributie.Meer");

        shell.MeerWisselenCommand.Execute(Item(shell, "Contributie.Runs")); // geen Meer…-regel
        Assert.False(meer.IsUitgeklapt);

        shell.MeerWisselenCommand.Execute(meer);
        Assert.True(meer.IsUitgeklapt);
        Assert.Same(Item(shell, "Contributie.Leden"), shell.GeselecteerdeNavigatie);
        Item(shell, "Contributie.Betalers");

        shell.MeerWisselenCommand.Execute(meer);
        Assert.False(meer.IsUitgeklapt);
        Assert.DoesNotContain(shell.Navigatie, n => n.Sleutel == "Contributie.Betalers");
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
    public async Task Openstaand_verschijnt_alleen_als_er_iets_openstaat()
    {
        var shell = await OpenAsync();

        await Kies(shell, "Contributie");

        Assert.DoesNotContain(shell.Navigatie, n => n.Sleutel == "Contributie.Openstaand");
        Assert.Equal(["Contributie.Leden", "Contributie.Runs", "Contributie.Meer"],
            shell.Navigatie.Where(n => n.Module == ModuleSoort.Contributie && n.IsSubitem).Select(n => n.Sleutel));
    }

    [Fact]
    public async Task Start_vernieuwen_laadt_de_boekhouding_per_administratie_maar_een_keer()
    {
        var shell = await OpenAsync();
        var boekhouding = (InMemoryBoekhoudingOpslag)_beheer.LaatsteSessie!.Boekhouding;
        await Task.Delay(50); // het vernieuwen na openen loopt op de achtergrond
        boekhouding.KeerGeladen = 0;

        shell.GekozenAdministratie = shell.Administraties.Single(a => a.Soort == AdministratieSoort.Stichting); // vernieuwt Start
        await Task.Delay(50);

        // Kaarten, inrichtingsstatus en kerncijfers samen: Vereniging en Stichting elk één keer (was 5 keer).
        Assert.Equal(2, boekhouding.KeerGeladen);
    }

    [Fact]
    public async Task Een_onderdeel_kiezen_klapt_de_module_uit_en_opent_het_subtabblad()
    {
        var shell = await OpenAsync();

        await Kies(shell, "Boekhouding"); // uitklappen en naar het huidige onderdeel (Journaal)
        Assert.Same(Item(shell, "Boekhouding.Journaal"), shell.GeselecteerdeNavigatie);
        Assert.True(Item(shell, "Boekhouding.Bank").IsSubitem);
        Assert.DoesNotContain(shell.Navigatie, n => n.Sleutel == "Contributie.Tarieven"); // alleen de gekozen module is uitgeklapt

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
        var leden = Item(shell, "Leden");

        await Kies(shell, "Boekhouding");

        Assert.Same(boekhouding, Item(shell, "Boekhouding"));
        Assert.Same(leden, Item(shell, "Leden"));
        Assert.True(boekhouding.IsUitgeklapt);
        Assert.Equal(Item(shell, "Boekhouding.Journaal"), shell.Navigatie[shell.Navigatie.IndexOf(boekhouding) + 1]);

        await Kies(shell, "Leden");
        Assert.Same(boekhouding, Item(shell, "Boekhouding"));
        Assert.False(boekhouding.IsUitgeklapt);
        Assert.DoesNotContain(shell.Navigatie, n => n.IsSubitem);
    }

    [Fact]
    public async Task Wisselen_van_administratie_houdt_dezelfde_pagina_en_past_de_navigatie_aan()
    {
        var shell = await OpenAsync();
        await Kies(shell, "Boekhouding");
        await Kies(shell, "Boekhouding.Bank");

        shell.GekozenAdministratie = shell.Administraties.Single(a => a.Soort == AdministratieSoort.Stichting);

        var boekhouding = Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdTabblad);
        Assert.Equal(AdministratieSoort.Stichting, boekhouding.Administratie!.Soort);
        Assert.Equal(BoekhoudingSubtab.Bank, boekhouding.Subtab);
        Assert.DoesNotContain(shell.Navigatie, n => n.Sleutel == "Contributie"); // besluit 16
        Assert.Equal("Meerjarenplan", Item(shell, "Begroting").Titel); // besluit 163
        Assert.DoesNotContain(shell.Navigatie, n => n.Sleutel == "Boekhouding.Begroting"); // de begroting staat als eigen item
        Assert.Equal("Scouting Stichting", shell.ActieveAdministratieNaam);
    }

    [Fact]
    public async Task Email_heeft_de_onderdelen_opstellen_verzonden_en_sjablonen_en_blijft_bij_wisselen_staan()
    {
        var shell = await OpenAsync();
        shell.GeselecteerdeNavigatie = Item(shell, "Email");
        Assert.Contains(shell.Navigatie, n => n.Sleutel == "Email.Verzonden");

        shell.GeselecteerdeNavigatie = Item(shell, "Email.Sjablonen");
        var email = Assert.IsType<ViewModels.Email.EmailViewModel>(shell.GeselecteerdTabblad);
        Assert.Equal(ViewModels.Email.EmailSubtab.Sjablonen, email.Subtab);

        shell.GekozenAdministratie = shell.Administraties.Single(a => a.Soort == AdministratieSoort.Stichting);
        Assert.Same(email, shell.GeselecteerdTabblad); // E-mail hoort niet bij één administratie
    }

    [Fact]
    public async Task Wisselen_vanaf_een_pagina_die_de_andere_administratie_niet_heeft_gaat_naar_start()
    {
        var shell = await OpenAsync();
        shell.GeselecteerdeNavigatie = Item(shell, "Contributie");
        shell.MeerWisselenCommand.Execute(Item(shell, "Contributie.Meer"));
        shell.GeselecteerdeNavigatie = Item(shell, "Contributie.Tarieven");
        Assert.Equal(ContributieSubtab.Tarieven, Assert.IsType<ContributieViewModel>(shell.GeselecteerdTabblad).Subtab);

        shell.GekozenAdministratie = shell.Administraties.Single(a => a.Soort == AdministratieSoort.Stichting);

        Assert.IsType<OverzichtViewModel>(shell.GeselecteerdTabblad);
        Assert.Same(Item(shell, "Start"), shell.GeselecteerdeNavigatie);
    }

    [Fact]
    public async Task Sneltoets_opent_de_module_bij_de_gekozen_administratie_en_markeert_de_navigatie()
    {
        var shell = await OpenAsync();
        shell.GekozenAdministratie = shell.Administraties.Single(a => a.Soort == AdministratieSoort.Stichting);

        shell.OpenModuleViaSneltoetsCommand.Execute("4");

        var boekhouding = Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdTabblad);
        Assert.Equal(AdministratieSoort.Stichting, boekhouding.Administratie!.Soort);
        Assert.Same(Item(shell, "Boekhouding.Journaal"), shell.GeselecteerdeNavigatie);
    }

    [Fact]
    public async Task Laatste_pagina_en_administratie_worden_na_herstart_hersteld()
    {
        var shell = await OpenAsync();
        shell.GekozenAdministratie = shell.Administraties.Single(a => a.Soort == AdministratieSoort.Stichting);
        await Kies(shell, "Boekhouding");
        await Kies(shell, "Boekhouding.Rapporten");
        Assert.True(await shell.MagAfsluitenAsync());

        Assert.Equal(new LaatstePagina(ModuleSoort.Boekhouding, AdministratieSoort.Stichting, (int)BoekhoudingSubtab.Rapporten), _instellingen.Instellingen.LaatstePagina);
        var tweede = await OpenAsync();

        var boekhouding = Assert.IsType<BoekhoudingViewModel>(tweede.GeselecteerdTabblad);
        Assert.Equal(AdministratieSoort.Stichting, tweede.GekozenAdministratie!.Soort);
        Assert.Equal(BoekhoudingSubtab.Rapporten, boekhouding.Subtab);
    }

    [Fact]
    public async Task Volgende_en_vorige_pagina_lopen_door_de_navigatie()
    {
        var shell = await OpenAsync();

        shell.VolgendePaginaCommand.Execute(null);
        Assert.Same(Item(shell, "Leden"), shell.GeselecteerdeNavigatie);
        shell.VorigePaginaCommand.Execute(null);
        shell.VorigePaginaCommand.Execute(null);
        Assert.Same(shell.Navigatie[^1], shell.GeselecteerdeNavigatie);
    }

    [Fact]
    public async Task Statusregel_toont_melding_en_fout_van_de_pagina_met_een_teken()
    {
        var shell = await OpenAsync();
        shell.GekozenAdministratie = shell.Administraties.Single(a => a.Soort == AdministratieSoort.Stichting);
        await Kies(shell, "Boekhouding");
        var boekhouding = Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdTabblad);
        await boekhouding.LeegBeginnenCommand.ExecuteAsync(null);

        await boekhouding.MeerjarenplanMakenCommand.ExecuteAsync(null);

        Assert.Equal(Statussoort.Gelukt, shell.Statussoort);
        Assert.Equal("✓", shell.Statusteken);
        Assert.Equal(boekhouding.Melding, shell.Statusmelding);
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
