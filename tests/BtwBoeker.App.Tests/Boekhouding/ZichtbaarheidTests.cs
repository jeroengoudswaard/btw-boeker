using BtwBoeker.App.ViewModels.Boekhouding;

namespace BtwBoeker.App.Tests.Boekhouding;

/// <summary>Alleen zichtbaar wat nu nodig is.</summary>
public sealed class ZichtbaarheidTests : IDisposable
{
    private readonly BoekhoudingTestomgeving _omgeving = new();

    public void Dispose() => _omgeving.Dispose();

    [Fact]
    public async Task Afdrukken_en_exporteren_alleen_waar_er_iets_af_te_drukken_is()
    {
        var vm = await _omgeving.OpenIngerichtAsync();

        vm.Subtab = BoekhoudingSubtab.Journaal;
        Assert.False(vm.HeeftUitvoer);
        vm.Subtab = BoekhoudingSubtab.Bank;
        Assert.False(vm.HeeftUitvoer);

        foreach (var subtab in new[] { BoekhoudingSubtab.Rapporten, BoekhoudingSubtab.Grootboekkaart })
        {
            vm.Subtab = subtab;
            Assert.True(vm.HeeftUitvoer);
        }
    }

    [Fact]
    public async Task Jaarkeuze_en_bankkeuze_alleen_bij_meer_dan_een()
    {
        var vm = await _omgeving.OpenIngerichtAsync();

        Assert.Single(vm.Boekjaren);
        Assert.False(vm.HeeftMeerdereBoekjaren);
        Assert.True(vm.Bankrekeningen.Count <= 1);
        Assert.False(vm.HeeftMeerdereBankrekeningen);
    }

    [Fact]
    public async Task Knoppen_tonen_het_aantal()
    {
        var vm = await _omgeving.OpenIngerichtAsync();

        Assert.EndsWith("(0)", vm.ConceptenBoekenTekst, StringComparison.Ordinal);
        Assert.Equal("Regels _toepassen (0)", vm.RegelsToepassenTekst);
        Assert.Equal(0, vm.AantalZonderBon);
        Assert.False(vm.IsZonderBonZichtbaar);
    }

    [Fact]
    public async Task Rapporten_melden_concepten_alleen_als_die_er_zijn()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        vm.Rapportsoort = Rapportsoort.Balans;
        Assert.Null(vm.Conceptmelding);

        vm.NieuwePostCommand.Execute(null);
        vm.Editor!.Omschrijving = "Half af";
        vm.Editor.NieuweRegelCommand.Execute(null);
        vm.Editor.Regels[0].GekozenRekening = vm.Editor.Rekeningen.Single(r => r.Nummer == 4115);
        vm.Editor.Regels[0].Debettekst = "10,00";
        Assert.True(await vm.BewaarAsync());
        vm.Rapportsoort = Rapportsoort.Saldibalans;

        Assert.StartsWith("⚠ 1 conceptpost telt niet mee", vm.Conceptmelding, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rubriekinvoer_verschijnt_na_een_klik_en_sluit_na_toevoegen()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        Assert.False(vm.IsRubriekinvoerOpen);

        vm.RubriekinvoerOpenenCommand.Execute(null);
        Assert.True(vm.IsRubriekinvoerOpen);
        vm.NieuweRubriekNaam = "Kampen";
        await vm.NieuweRubriekCommand.ExecuteAsync(null);

        Assert.False(vm.IsRubriekinvoerOpen);
        Assert.Contains(vm.Rubrieken, r => r.Naam == "Kampen");
    }
}
