using Verenigd.App.ViewModels.Leden;
using Verenigd.Application.Modules;
using Verenigd.Domain.Leden;
using static Verenigd.App.Tests.Leden.LedenTestdata;

namespace Verenigd.App.Tests.Leden;

public sealed class LedenViewModelTests
{
    private readonly InMemoryLedenOpslag _opslag = new();
    private readonly VasteTijd _tijd = new(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));

    private async Task<LedenViewModel> MaakAsync()
    {
        await VulAsync(_opslag,
            Lid("100000001", "Anna", "Vries"),
            Lid("100000002", "Bo", "Jansen", groep: "Junioren", lidtype: Lidtype.Kaderlid, rol: "Leider"),
            Lid("100000003", "Cas", "Bakker", groep: "Junioren"));
        var vm = new LedenViewModel(_opslag, _tijd);
        await vm.VernieuwCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Tabblad_hoort_bij_module_leden_zonder_administratie()
    {
        var vm = await MaakAsync();

        Assert.Equal(ModuleSoort.Leden, vm.Module.Soort);
        Assert.Null(vm.Administratie);
    }

    [Fact]
    public async Task Leden_worden_gesorteerd_op_achternaam_en_geteld()
    {
        var vm = await MaakAsync();

        Assert.Equal(["Bakker", "Jansen", "Vries"], vm.Leden.Select(l => l.Achternaam));
        Assert.Equal("3 van 3 personen", vm.Aantaltekst);
    }

    [Fact]
    public async Task Technische_groepen_staan_niet_in_groepfilter_en_groepkolom()
    {
        var vm = await MaakAsync();

        Assert.Equal([LedenViewModel.AlleGroepen, "Junioren", "Welpen"], vm.Groepfilters);
        Assert.Equal("Welpen", vm.Leden.Single(l => l.Achternaam == "Vries").Groepen);
    }

    [Fact]
    public async Task Detail_toont_alle_functies_inclusief_technische_en_lid_sinds()
    {
        var vm = await MaakAsync();
        var anna = vm.Leden.Single(l => l.Achternaam == "Vries");

        Assert.Equal(2, anna.Functies.Count);
        Assert.Contains(anna.Functies, f => f.IsTechnisch);
        Assert.Equal("07-11-2018", anna.LidSinds);
        Assert.Equal("06 12345678", anna.Telefoon);
        Assert.Equal("(naam onbekend)", Assert.Single(anna.Voogden).Naam);
    }

    [Theory]
    [InlineData("jan", "Jansen")]
    [InlineData("100000003", "Bakker")]
    [InlineData("anna@", "Vries")]
    public async Task Zoeken_op_naam_lidnummer_of_e_mail(string zoektekst, string verwacht)
    {
        var vm = await MaakAsync();

        vm.Zoektekst = zoektekst;

        Assert.Equal(verwacht, Assert.Single(vm.Leden).Achternaam);
        Assert.Equal("1 van 3 personen", vm.Aantaltekst);
    }

    [Fact]
    public async Task Filteren_op_groep_en_lidtype()
    {
        var vm = await MaakAsync();

        vm.Groepfilter = "Junioren";
        Assert.Equal(2, vm.Leden.Count);

        vm.Lidtypefilter = Lidtype.Kaderlid;
        Assert.Equal("Jansen", Assert.Single(vm.Leden).Achternaam);
    }

    [Fact]
    public async Task Uitgeschreven_leden_alleen_op_verzoek_zichtbaar()
    {
        var vm = await MaakAsync();
        Assert.False(vm.HeeftUitgeschreven); // besluit 270: het vakje verschijnt pas als er uitgeschreven leden zijn
        _opslag.Stand.Personen.Single(p => p.Achternaam == "Bakker").Uitschrijven(Vandaag, DateTime.UtcNow);
        await vm.VernieuwCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Leden.Count);
        Assert.True(vm.HeeftUitgeschreven);

        vm.ToonUitgeschreven = true;
        var cas = vm.Leden.Single(l => l.Achternaam == "Bakker");
        Assert.True(cas.IsUitgeschreven);
        Assert.Equal("Uitgeschreven per 27-10-2026", cas.Statustekst);
    }

    [Fact]
    public async Task Notities_wijzigen_markeert_tabblad_en_ctrl_s_slaat_op()
    {
        var vm = await MaakAsync();
        var anna = vm.Leden.Single(l => l.Achternaam == "Vries");
        Assert.False(vm.HeeftOnopgeslagenWijzigingen);

        anna.Notities = "Zwemdiploma A";

        Assert.True(vm.HeeftOnopgeslagenWijzigingen);
        Assert.True(vm.KanOpslaan);
        Assert.True(await vm.BewaarAsync());
        Assert.Equal("Zwemdiploma A", Assert.Single(_opslag.BewaardeNotities).Notities);
        Assert.False(vm.HeeftOnopgeslagenWijzigingen);
    }

    [Fact]
    public async Task Vernieuwen_behoudt_niet_opgeslagen_notities_en_selectie()
    {
        var vm = await MaakAsync();
        var anna = vm.Leden.Single(l => l.Achternaam == "Vries");
        vm.GeselecteerdLid = anna;
        anna.Notities = "Nog niet opgeslagen";

        await vm.VernieuwCommand.ExecuteAsync(null);

        Assert.Equal("Nog niet opgeslagen", vm.Leden.Single(l => l.Achternaam == "Vries").Notities);
        Assert.Equal("Vries", vm.GeselecteerdLid?.Achternaam);
        Assert.True(vm.HeeftOnopgeslagenWijzigingen);
    }

    [Fact]
    public async Task Te_lange_notitie_kan_niet_worden_opgeslagen()
    {
        var vm = await MaakAsync();
        var anna = vm.Leden.Single(l => l.Achternaam == "Vries");

        anna.Notities = new string('x', Persoon.MaximaleNotitieLengte + 1);

        Assert.NotNull(anna.Fout);
        Assert.False(vm.KanOpslaan);
    }

    [Fact]
    public async Task Lege_database_toont_hint_voor_import()
    {
        var vm = new LedenViewModel(new InMemoryLedenOpslag(), _tijd);
        await vm.VernieuwCommand.ExecuteAsync(null);

        Assert.True(vm.IsLeeg);
        Assert.Empty(vm.Leden);
    }
}
