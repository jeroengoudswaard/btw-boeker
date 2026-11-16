using Microsoft.Extensions.Logging.Abstractions;
using BtwBoeker.App.Diensten;
using BtwBoeker.App.ViewModels;
using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.Application.Instellingen;
using BtwBoeker.Application.Modules;
using BtwBoeker.Application.Weergaven;

namespace BtwBoeker.App.Tests;

/// <summary>
/// UX-review ronde 2, stappen 1 en 2 (besluiten 32 en 33): een rustiger schil (venstertitel, paginakop per onderdeel,
/// meldingsbalk) en een platte navigatie zonder de laag Boekhouding.
/// </summary>
public sealed class ShellSchilTests
{
    private const string Pad = @"C:\Data\btw-boeker.btwdb";
    private const string Wachtwoord = "geheim wachtwoord";

    private static readonly string[] PlatteNavigatie =
    [
        "Start", "Boekhouding.Journaal", "Boekhouding.Bank", "Boekhouding.Postvak", "Boekhouding.Rapporten", "Boekhouding.Meer", "Instellingen",
    ];

    private static readonly string[] PlatteTitels = ["Start", "Journaal", "Bank", "Bonnen", "Rapporten", "Meer…", "Instellingen"];

    private readonly FakeDatabaseBeheer _beheer = new();
    private readonly FakeDialoogService _dialogen = new();
    private readonly InMemoryInstellingenOpslag _instellingen = new() { Instellingen = new GebruikersInstellingen { StartwizardGetoond = true } };

    private ShellViewModel MaakShell() =>
        new(_beheer, _instellingen, _dialogen, new FakeThemaService(), FakeBoekhouding.Hulpmiddelen(),
            new VasteTijd(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero)), NullLogger<ShellViewModel>.Instance);

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
        if (shell.GeselecteerdePagina is BoekhoudingViewModel { VernieuwCommand.ExecutionTask: { } laden })
        {
            await laden;
        }
    }

    private static async Task<BoekhoudingViewModel> IngerichtAsync(ShellViewModel shell)
    {
        await Kies(shell, "Boekhouding.Journaal");
        var boekhouding = Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdePagina);
        await boekhouding.LeegBeginnenCommand.ExecuteAsync(null);
        return boekhouding;
    }

    // ---- Venster en menubalk -----------------------------------------------------------------

    [Fact]
    public async Task Venstertitel_is_bestand_en_app_zonder_de_administratie()
    {
        var shell = MaakShell();
        await shell.InitialiseerAsync();
        Assert.Equal("BTW-Boeker", shell.Venstertitel);

        shell = await OpenAsync();

        Assert.Equal("btw-boeker.btwdb – BTW-Boeker", shell.Venstertitel);
    }

    // ---- Platte navigatie --------------------------------------------------------------------

    [Fact]
    public async Task Na_openen_staan_de_onderdelen_direct_in_de_navigatie_zonder_de_laag_boekhouding()
    {
        var shell = await OpenAsync();

        Assert.Equal(PlatteNavigatie, shell.Navigatie.Select(n => n.Sleutel));
        Assert.Equal(PlatteTitels, shell.Navigatie.Select(n => n.Titel));
        Assert.DoesNotContain(shell.Navigatie, n => n.IsSubitem);
    }

    [Fact]
    public async Task Elk_dagelijks_onderdeel_heeft_een_eigen_pictogram_en_een_eigen_kleur()
    {
        var shell = await OpenAsync();
        var dagelijks = PlatteNavigatie[1..5].Select(s => Item(shell, s)).ToList();

        Assert.All(dagelijks, n => Assert.False(string.IsNullOrEmpty(n.Pictogram), n.Sleutel));
        Assert.Equal(dagelijks.Count, dagelijks.Select(n => n.Pictogram).Distinct().Count());
        var kleuren = dagelijks.Select(n => Onderdeelkleuren.Voor(n.Sleutel)).ToList();
        Assert.Equal(kleuren.Count, kleuren.Distinct().Count());
        Assert.DoesNotContain(Onderdeelkleuren.Grijs, kleuren);
        Assert.Equal(Onderdeelkleuren.Grijs, Onderdeelkleuren.Voor("Start"));
        Assert.Equal(Onderdeelkleuren.Grijs, Onderdeelkleuren.Voor("Instellingen"));
    }

    [Fact]
    public async Task Meer_klapt_de_inrichting_uit_als_subitems()
    {
        var shell = await OpenAsync();
        await Kies(shell, "Boekhouding.Journaal");
        var meer = Item(shell, "Boekhouding.Meer");

        shell.MeerWisselenCommand.Execute(meer);

        Assert.True(meer.IsUitgeklapt);
        Assert.Equal(["Boekingsregels", "Relaties", "Rekeningschema", "Grootboekkaart", "Boekjaren"],
            shell.Navigatie.Where(n => n.IsSubitem).Select(n => n.Sleutel.Split('.')[1]));
        Assert.Same(Item(shell, "Boekhouding.Journaal"), shell.GeselecteerdeNavigatie);
    }

    [Fact]
    public async Task Een_onderdeel_kiezen_vanaf_start_opent_de_boekhouding_op_dat_onderdeel()
    {
        var shell = await OpenAsync();

        await Kies(shell, "Boekhouding.Bank");

        var boekhouding = Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdePagina);
        Assert.Equal(BoekhoudingSubtab.Bank, boekhouding.Subtab);
        Assert.Same(Item(shell, "Boekhouding.Bank"), shell.GeselecteerdeNavigatie);
        Assert.Equal(PlatteNavigatie, shell.Navigatie.Select(n => n.Sleutel)); // de navigatie verspringt niet
    }

    [Fact]
    public async Task De_band_boven_de_pagina_volgt_het_onderdeel()
    {
        var shell = await OpenAsync();
        Assert.Equal("Start", shell.GeselecteerdePagina!.Navigatiesleutel);

        await Kies(shell, "Boekhouding.Bank");
        var pagina = shell.GeselecteerdePagina!;
        Assert.Equal("Boekhouding.Bank", pagina.Navigatiesleutel);
        var gewijzigd = new List<string?>();
        pagina.PropertyChanged += (_, e) => gewijzigd.Add(e.PropertyName);

        await Kies(shell, "Boekhouding.Rapporten");

        Assert.Equal("Boekhouding.Rapporten", pagina.Navigatiesleutel);
        Assert.Contains(nameof(PaginaViewModel.Navigatiesleutel), gewijzigd);
    }

    [Fact]
    public async Task Een_teller_staat_alleen_bij_het_onderdeel()
    {
        var shell = await OpenAsync();

        Assert.DoesNotContain(shell.Navigatie, n => n.Sleutel == "Boekhouding");
        Assert.False(Navigatietellers.Bepaal([new(BtwBoeker.Application.Inrichting.Startkaartsoort.Bank, 2, "t", "t", "k")]).ContainsKey("Boekhouding"));
    }

    // ---- Paginakop ---------------------------------------------------------------------------

    [Fact]
    public async Task Titel_en_uitleg_zijn_die_van_het_onderdeel()
    {
        var shell = await OpenAsync();
        var boekhouding = await IngerichtAsync(shell);

        await Kies(shell, "Boekhouding.Bank");

        Assert.Equal("Bank", boekhouding.Paginatitel);
        Assert.Equal(Woordenlijst.Zoek("Kop.Bank").Expert, boekhouding.Omschrijving);
    }

    [Fact]
    public async Task Een_ander_onderdeel_meldt_de_nieuwe_kop()
    {
        var shell = await OpenAsync();
        var boekhouding = await IngerichtAsync(shell);
        var gewijzigd = new List<string?>();
        boekhouding.PropertyChanged += (_, e) => gewijzigd.Add(e.PropertyName);

        await Kies(shell, "Boekhouding.Rapporten");

        Assert.Equal("Rapporten", boekhouding.Paginatitel);
        Assert.Contains(nameof(PaginaViewModel.Paginatitel), gewijzigd);
        Assert.Contains(nameof(PaginaViewModel.Omschrijving), gewijzigd);
    }

    [Fact]
    public async Task Titel_en_uitleg_volgen_de_weergave_eenvoudig()
    {
        var shell = await OpenAsync();
        var boekhouding = await IngerichtAsync(shell);
        var gewijzigd = new List<string?>();
        boekhouding.PropertyChanged += (_, e) => gewijzigd.Add(e.PropertyName);

        shell.KiesWeergaveCommand.Execute(Weergavemodus.Eenvoudig);

        Assert.Equal("Alle boekingen", boekhouding.Paginatitel);
        Assert.Equal(Woordenlijst.Zoek("Kop.Journaal").Eenvoudig, boekhouding.Omschrijving);
        Assert.Contains(nameof(PaginaViewModel.Paginatitel), gewijzigd);
        Assert.Equal("Alle boekingen", Item(shell, "Boekhouding.Journaal").Titel);
    }

    [Fact]
    public async Task Een_lege_boekhouding_heet_boekhouding_tot_ze_is_ingericht()
    {
        var shell = await OpenAsync();
        await Kies(shell, "Boekhouding.Bank");
        var boekhouding = Assert.IsType<BoekhoudingViewModel>(shell.GeselecteerdePagina);
        Assert.True(boekhouding.IsLeeg);
        Assert.Equal("Boekhouding", boekhouding.Paginatitel);
        var gewijzigd = new List<string?>();
        boekhouding.PropertyChanged += (_, e) => gewijzigd.Add(e.PropertyName);

        await boekhouding.LeegBeginnenCommand.ExecuteAsync(null);

        Assert.Equal("Bank", boekhouding.Paginatitel);
        Assert.Contains(nameof(PaginaViewModel.Paginatitel), gewijzigd);
    }

    [Fact]
    public void Elk_onderdeel_heeft_een_zin_uitleg_in_beide_weergaven()
    {
        foreach (var onderdeel in Enum.GetValues<BoekhoudingSubtab>())
        {
            var term = Woordenlijst.Zoek($"Kop.{onderdeel}");
            Assert.EndsWith(".", term.Expert, StringComparison.Ordinal);
            Assert.EndsWith(".", term.Eenvoudig, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Start_en_instellingen_houden_hun_eigen_titel()
    {
        var shell = await OpenAsync();
        Assert.Equal("Start", shell.GeselecteerdePagina!.Paginatitel);

        await Kies(shell, "Instellingen");

        Assert.Equal("Instellingen", shell.GeselecteerdePagina!.Paginatitel);
        Assert.Equal(ModuleCatalogus.Geef(ModuleSoort.Instellingen).Omschrijving, shell.GeselecteerdePagina.Omschrijving);
    }

    // ---- Meldingsbalk ------------------------------------------------------------------------

    [Fact]
    public async Task Een_melding_van_de_pagina_staat_in_de_meldingsbalk()
    {
        var shell = await OpenAsync();
        var boekhouding = await IngerichtAsync(shell);

        await boekhouding.ConceptenBoekenCommand.ExecuteAsync(null); // geen concepten: een melding

        Assert.True(shell.Meldingsbalk.IsZichtbaar);
        Assert.Equal(Statussoort.Gelukt, shell.Meldingsbalk.Soort);
        Assert.Equal(boekhouding.Melding, shell.Meldingsbalk.Tekst);
    }

    [Fact]
    public async Task Een_waarschuwing_verdwijnt_bij_een_ander_onderdeel_en_bij_een_andere_pagina()
    {
        var shell = await OpenAsync();
        await IngerichtAsync(shell);

        shell.Meldingsbalk.Toon(Statussoort.Waarschuwing, "Let op");
        await Kies(shell, "Boekhouding.Bank");
        Assert.False(shell.Meldingsbalk.IsZichtbaar);

        shell.Meldingsbalk.Toon(Statussoort.Fout, "Mislukt");
        await Kies(shell, "Instellingen");
        Assert.False(shell.Meldingsbalk.IsZichtbaar);
    }

    [Fact]
    public async Task Na_openen_is_de_meldingsbalk_leeg()
    {
        var shell = await OpenAsync();

        Assert.False(shell.Meldingsbalk.IsZichtbaar);
    }
}
