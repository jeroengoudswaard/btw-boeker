using BtwBoeker.App.ViewModels;
using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.Application.Begrotingen;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.Tests.Boekhouding;

/// <summary>Subtabblad Begroting en de kolom Baten/lasten (besluiten 146–155). Vandaag is 15-06-2026.</summary>
public sealed class BegrotingViewModelTests : IDisposable
{
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private readonly BoekhoudingTestomgeving _omgeving = new();

    public void Dispose() => _omgeving.Dispose();

    private async Task<BoekhoudingViewModel> OpenAsync(int jaar = 2027)
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        vm.Subtab = BoekhoudingSubtab.Begroting;
        vm.Begrotingsjaar = jaar;
        return vm;
    }

    [Fact]
    public async Task Zonder_begroting_kan_er_een_worden_gemaakt()
    {
        var vm = await OpenAsync();
        Assert.False(vm.HeeftBegroting);
        Assert.Contains(2027, vm.Begrotingsjaren);

        await vm.BegrotingMakenCommand.ExecuteAsync(null);

        Assert.True(vm.HeeftBegroting);
        Assert.True(vm.IsBegrotingBewerkbaar);
        Assert.Equal("Versie 1 · Oorspronkelijk · Concept", vm.GekozenBegrotingsversie?.Tekst);
        Assert.Contains(vm.Begrotingsrijen, r => r.Rekeningtekst == "4115 Energie" && r.Kanttekst == "Lasten");
        Assert.Contains(vm.Begrotingsrijen, r => r.Rekeningtekst == "8020 Contributie Welpen" && r.Kanttekst == "Baten");
        Assert.DoesNotContain(vm.Begrotingsrijen, r => r.Rekeningtekst.StartsWith("1100", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Bedragen_met_de_hand_en_opslaan_met_ctrl_s()
    {
        var vm = await OpenAsync();
        await vm.BegrotingMakenCommand.ExecuteAsync(null);
        var energie = vm.Begrotingsrijen.Single(r => r.Rekeningtekst == "4115 Energie");

        energie.Bedragtekst = "1.300,00";
        energie.Toelichting = "Nieuw contract";
        vm.Begrotingsrijen.Single(r => r.Rekeningtekst == "8020 Contributie Welpen").Bedragtekst = "4400";

        Assert.True(vm.HeeftOnopgeslagenWijzigingen);
        Assert.True(await vm.BewaarAsync());
        Assert.False(vm.HeeftOnopgeslagenWijzigingen);
        var begroting = Assert.Single((await _omgeving.Opslag.LaadAsync(Ver)).Begrotingen);
        Assert.Equal(new Bedrag(130000), begroting.Regels.Single(r => r.Rekening.Nummer == 4115).Bedrag);
        Assert.Equal("Nieuw contract", begroting.Regels.Single(r => r.Rekening.Nummer == 4115).Toelichting);
        Assert.Contains("resultaat € 3.100,00", vm.Begrotingstotalen, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Een_ongeldig_bedrag_blokkeert_opslaan()
    {
        var vm = await OpenAsync();
        await vm.BegrotingMakenCommand.ExecuteAsync(null);
        var energie = vm.Begrotingsrijen.Single(r => r.Rekeningtekst == "4115 Energie");

        energie.Bedragtekst = "veel";

        Assert.NotNull(energie.Fout);
        Assert.False(vm.KanOpslaan);
    }

    [Fact]
    public async Task Overnemen_met_percentage_daarna_vaststellen_en_herzien()
    {
        var vm = await OpenAsync(2026);
        await vm.BegrotingMakenCommand.ExecuteAsync(null);
        vm.OvernemenBron = BegrotingBron.Werkelijk;
        vm.OvernemenJaar = 2025;
        vm.OvernemenPercentage = "5";

        await vm.BegrotingOvernemenCommand.ExecuteAsync(null);

        // 2025 bestaat alleen als beginbalans; er zijn geen werkelijke resultaten, dus alles nul.
        Assert.All(vm.Begrotingsrijen, r => Assert.Equal(string.Empty, r.Bedragtekst));

        vm.Begrotingsrijen.Single(r => r.Rekeningtekst == "4115 Energie").Bedragtekst = "1.200,00";
        await vm.BewaarAsync();
        vm.VaststellenOp = new DateTime(2026, 1, 10);
        vm.VaststellenOmschrijving = "ALV 10-01-2026";
        await vm.BegrotingVaststellenCommand.ExecuteAsync(null);

        Assert.False(vm.IsBegrotingBewerkbaar);
        Assert.Equal("Versie 1 · ALV 10-01-2026 · Vastgesteld 10-01-2026", vm.GekozenBegrotingsversie?.Tekst);
        Assert.Equal("€ 545,75", vm.Begrotingsrijen.Single(r => r.Rekeningtekst == "4115 Energie").NaarRato); // 1.200 × 166/365 (15 juni)

        await vm.BegrotingHerzienCommand.ExecuteAsync(null);

        Assert.True(vm.IsBegrotingBewerkbaar);
        Assert.Equal(2, vm.Begrotingsversies.Count);
        Assert.Equal("Versie 2 · Herziening 1 · Concept", vm.GekozenBegrotingsversie?.Tekst);
    }

    [Fact]
    public async Task Ctrl_p_op_het_subtabblad_drukt_de_alv_begroting_af()
    {
        var vm = await OpenAsync();
        await vm.BegrotingMakenCommand.ExecuteAsync(null);

        Assert.Equal("Begroting 2027", vm.Afdruktabel?.Titel);
        vm.Subtab = BoekhoudingSubtab.Rapporten;
        vm.Rapportsoort = Rapportsoort.Begroting;
        Assert.StartsWith("Begroting en werkelijk", vm.Afdruktabel?.Titel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Kolom_baten_of_lasten_in_het_rekeningschema()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        var energie = vm.Rekeningen.Single(r => r.Nummertekst == "4115");
        Assert.Equal(ResultaatKant.Lasten, energie.Kant);
        Assert.True(energie.KantIsKiesbaar);
        Assert.False(vm.Rekeningen.Single(r => r.Nummertekst == "1100").KantIsKiesbaar);

        energie.Kant = ResultaatKant.Baten;
        await vm.BewaarAsync();

        Assert.Equal(ResultaatKant.Baten, (await _omgeving.Opslag.LaadAsync(Ver)).Rekeningen.Single(r => r.Nummer == 4115).Kant);
    }

    [Fact]
    public async Task Eenvoudig_noemt_begroot_verwacht()
    {
        var vm = await OpenAsync();

        vm.Woorden.Weergave = Application.Instellingen.Weergavemodus.Eenvoudig;

        Assert.Equal("Verwacht", vm.Woorden["Begroot"]);
        Assert.Equal("Uitgegeven/ontvangen", vm.Woorden["Werkelijk"]);
        Assert.Equal("Verwacht tot nu toe", vm.Woorden["NaarRato"]);
    }
}
