using Microsoft.Extensions.Logging.Abstractions;
using BtwBoeker.App.Diensten;
using BtwBoeker.App.ViewModels;
using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.Application.Instellingen;
using BtwBoeker.Application.Modules;

namespace BtwBoeker.App.Tests.Boekhouding;

/// <summary>Weergaven Expert en Eenvoudig: alleen de woorden veranderen, niet de gegevens.</summary>
public sealed class WeergaveTests : IDisposable
{
    private readonly BoekhoudingTestomgeving _omgeving = new();

    public void Dispose() => _omgeving.Dispose();

    private Woordenboek Woorden => _omgeving.Hulpmiddelen.Woorden;

    [Fact]
    public void Woordenboek_wisselt_en_meldt_de_indexer()
    {
        var gemeld = new List<string?>();
        Woorden.PropertyChanged += (_, e) => gemeld.Add(e.PropertyName);

        Assert.Equal(Weergavemodus.Expert, Woorden.Weergave);
        Assert.Equal("Journaal", Woorden["Journaal"]);
        Assert.Null(Woorden.Uitleg("Rekeningschema"));

        Woorden.Weergave = Weergavemodus.Eenvoudig;

        Assert.Equal("Alle boekingen", Woorden["Journaal"]);
        Assert.NotNull(Woorden.Uitleg("Rekeningschema"));
        Assert.Contains("Item[]", gemeld);
        Assert.True(Woorden.IsEenvoudig);
        Assert.Equal("1 boeking", Woorden.Aantal(1, "Post", "Posten"));
        Assert.Equal("3 boekingen", Woorden.Aantal(3, "Post", "Posten"));
    }

    [Fact]
    public async Task Pagina_volgt_de_weergave_titel_status_rapporten_en_kolommen()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        Assert.Equal("Boekhouding", vm.Titel);
        Assert.Equal("Geboekt", vm.Posten.Single().Status);
        vm.KaartRekening = vm.Rekeningkeuzes.Single(r => r.Nummer == 4115);
        Assert.Equal(("Debet", "Credit"), (vm.KaartDebetkop, vm.KaartCreditkop));

        Woorden.Weergave = Weergavemodus.Eenvoudig;

        Assert.Equal("Financiën", vm.Titel);
        Assert.Equal("Definitief", vm.Posten.Single().Status);
        Assert.Equal("Stand van alle potjes", vm.Rapportkeuzes.Single(r => r.Soort == Rapportsoort.Saldibalans).Naam);
        Assert.Equal(("Uitgave", "Inkomst"), (vm.KaartDebetkop, vm.KaartCreditkop)); // inkomsten- en uitgavenpotje
        vm.KaartRekening = vm.Rekeningkeuzes.Single(r => r.Nummer == 1000);
        Assert.Equal(("Erbij", "Eraf"), (vm.KaartDebetkop, vm.KaartCreditkop)); // geldpotje (kas)
        vm.KaartRekening = vm.Rekeningkeuzes.Single(r => r.Nummer == 600);
        Assert.Equal(("Debet", "Credit"), (vm.KaartDebetkop, vm.KaartCreditkop)); // overig balanspotje
        Assert.NotNull(vm.KaartKoptip);
    }

    [Fact]
    public async Task Meldingen_volgen_de_weergave_maar_de_gegevens_blijven_formeel()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        Woorden.Weergave = Weergavemodus.Eenvoudig;
        vm.NieuwePostCommand.Execute(null);
        var editor = vm.Editor!;
        editor.Omschrijving = "Energie juni";
        while (editor.Regels.Count < 2)
        {
            editor.NieuweRegelCommand.Execute(null);
        }

        editor.Regels[0].GekozenRekening = editor.Rekeningen.Single(r => r.Nummer == 4115);
        editor.Regels[0].Debettekst = "10,00";
        editor.Regels[1].GekozenRekening = editor.Rekeningen.Single(r => r.Nummer == 1100);
        editor.Regels[1].Credittekst = "10,00";

        await vm.BoekenCommand.ExecuteAsync(null);

        Assert.StartsWith("Boeking MEM-2026-0002 is definitief", vm.Melding, StringComparison.Ordinal);
        var stand = await _omgeving.Opslag.LaadAsync(Domain.Administraties.AdministratieSoort.Standaard);
        Assert.Equal("MEM-2026-0002", stand.Posten.Single(p => p.Omschrijving == "Energie juni").Nummer);
    }

    [Fact]
    public async Task Shell_wisselt_via_beeld_weergave_en_onthoudt_de_keuze()
    {
        var opslag = new InMemoryInstellingenOpslag();
        var hulpmiddelen = FakeBoekhouding.Hulpmiddelen();
        var shell = new ShellViewModel(new FakeDatabaseBeheer(), opslag, new FakeDialoogService(), new FakeThemaService(), hulpmiddelen,
            _omgeving.Tijd, NullLogger<ShellViewModel>.Instance);
        await shell.InitialiseerAsync();
        Assert.Equal(Weergavemodus.Expert, shell.Weergave);
        Assert.Contains(shell.ModuleMenu, m => m.Titel == "Boekhouding");

        shell.KiesWeergaveCommand.Execute(Weergavemodus.Eenvoudig);

        Assert.Equal(Weergavemodus.Eenvoudig, shell.Weergave);
        Assert.Equal(Weergavemodus.Eenvoudig, hulpmiddelen.Woorden.Weergave);
        Assert.Equal(Weergavemodus.Eenvoudig, opslag.Instellingen.Weergave);
        Assert.Contains(shell.ModuleMenu, m => m.Titel == "Financiën");

        var opnieuw = new ShellViewModel(new FakeDatabaseBeheer(), opslag, new FakeDialoogService(), new FakeThemaService(), FakeBoekhouding.Hulpmiddelen(),
            _omgeving.Tijd, NullLogger<ShellViewModel>.Instance);
        await opnieuw.InitialiseerAsync();
        Assert.Equal(Weergavemodus.Eenvoudig, opnieuw.Weergave);
    }
}
