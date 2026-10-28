using Verenigd.App.ViewModels.Boekhouding;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Tests.Boekhouding;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.Tests.Boekhouding;

/// <summary>Historisch boekjaar 2025 in de app (besluiten 122–126). Gegevens zijn verzonnen.</summary>
public sealed class HistorieViewModelTests : IDisposable
{
    private readonly BoekhoudingTestomgeving _omgeving = new();

    public HistorieViewModelTests()
    {
        _omgeving.Lezer.Blad = HistorieVoorbeeld.Voorblad();
        _omgeving.Lezer.Bladen = HistorieVoorbeeld.Bladen();
    }

    public void Dispose() => _omgeving.Dispose();

    private Task RichtInAsync() => new BoekhoudingDienst(_omgeving.Sessie, _omgeving.Tijd)
        .ImporteerAsync(AdministratieSoort.Vereniging, VoorbladInlezer.Lees(HistorieVoorbeeld.Voorblad()), new VerschilKeuze(1299, "Te onderzoeken verschillen"));

    private HistorieImportViewModel Wizard() =>
        new(new HistorieDienst(_omgeving.Sessie, _omgeving.Tijd), _omgeving.Lezer, AdministratieSoort.Vereniging, "boekhouding.xlsx");

    private static async Task<bool> Doorloop(HistorieImportViewModel wizard)
    {
        while (!wizard.IsKlaar && wizard.VolgendeCommand.CanExecute(null))
        {
            await wizard.VolgendeCommand.ExecuteAsync(null);
        }

        return wizard.IsKlaar;
    }

    [Fact]
    public async Task Wizard_toont_per_rekening_de_vergelijking_en_neemt_over()
    {
        await RichtInAsync();
        var wizard = Wizard();

        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.Equal(HistorieImportStap.Voorbeeld, wizard.Stap);
        Assert.Equal("Historie 2025", wizard.Titel);
        Assert.Equal("Overnemen", wizard.Volgendetekst);
        Assert.Equal(6, wizard.Rekeningen.Count);
        Assert.StartsWith("✓", wizard.Rekeningen.Single(r => r.Nummer == 1100).Status, StringComparison.Ordinal);
        Assert.StartsWith("⚠", wizard.Rekeningen.Single(r => r.Nummer == 4115).Status, StringComparison.Ordinal);
        Assert.Equal("Niet op het Voorblad", wizard.Rekeningen.Single(r => r.Nummer == 9999).Status);
        Assert.Equal("Geen regels", wizard.Rekeningen.Single(r => r.Nummer == 4200).Status);
        Assert.Contains("1 rekening wijkt af", wizard.Overzicht, StringComparison.Ordinal);
        Assert.Single(wizard.Meldingen);

        wizard.OnbekendeToevoegen = true;
        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.True(wizard.IsKlaar, wizard.Foutmelding);
        Assert.Equal(7, wizard.AantalOvergenomen);
        var stand = await _omgeving.Opslag.LaadAsync(AdministratieSoort.Vereniging);
        Assert.False(stand.Rekeningen.Single(r => r.Nummer == 9999).IsActief);
    }

    [Fact]
    public async Task Wizard_meldt_dat_eerst_het_rekeningschema_nodig_is()
    {
        var wizard = Wizard();

        await wizard.VolgendeCommand.ExecuteAsync(null);
        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.False(wizard.IsKlaar);
        Assert.Contains("rekeningschema", wizard.Foutmelding, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Overname_uit_excel_opent_daarna_de_historie_en_2025_is_alleen_lezen()
    {
        var vm = _omgeving.Maak();
        await vm.VernieuwCommand.ExecuteAsync(null);
        _omgeving.Dialogen.ExcelBestand = "boekhouding.xlsx";
        _omgeving.Dialogen.BoekhoudingImport = async wizard =>
        {
            Assert.True(wizard.OokHistorie);
            await wizard.VolgendeCommand.ExecuteAsync(null);
            await wizard.VolgendeCommand.ExecuteAsync(null);
            wizard.VerschilBewustBoeken = true;
            await wizard.VolgendeCommand.ExecuteAsync(null);
            return wizard.IsKlaar;
        };
        _omgeving.Dialogen.HistorieImport = Doorloop;

        await vm.OvernemenUitExcelCommand.ExecuteAsync(null);

        Assert.Equal([2025, 2026], vm.Boekjaren);
        Assert.False(vm.IsHistorischJaar);
        vm.Jaar = 2025;
        Assert.True(vm.IsHistorischJaar);
        Assert.Empty(vm.Posten);
        vm.KaartRekening = vm.Rekeningkeuzes.Single(r => r.Nummer == 4115);
        Assert.Equal("Stroom", Assert.Single(vm.KaartMutaties).Omschrijving);
        Assert.True(vm.KaartWijktAf);
        Assert.Contains("€ 30,00", vm.KaartVoorbladtekst, StringComparison.Ordinal);
        var jaar2025 = vm.Jaren.Single(j => j.Jaar == 2025);
        Assert.Equal("Historisch", jaar2025.Statustekst);
        Assert.False(vm.HeropenBoekjaarCommand.CanExecute(jaar2025));
        vm.Jaar = 2026;
        Assert.Null(vm.KaartVoorbladtekst);
    }

    [Fact]
    public async Task Historie_uit_excel_via_het_menu_voor_een_ingerichte_administratie()
    {
        await RichtInAsync();
        var vm = _omgeving.Maak();
        await vm.VernieuwCommand.ExecuteAsync(null);
        _omgeving.Dialogen.ExcelBestand = "boekhouding.xlsx";
        _omgeving.Dialogen.HistorieImport = Doorloop;

        await vm.HistorieInlezenCommand.ExecuteAsync(null);

        Assert.Contains(2025, vm.Boekjaren);
        Assert.Contains("historie", vm.Melding, StringComparison.OrdinalIgnoreCase);

        await vm.HistorieInlezenCommand.ExecuteAsync(null); // bestaat al
        Assert.Contains("bestaat al", _omgeving.Dialogen.Informatie, StringComparison.Ordinal);
    }
}
