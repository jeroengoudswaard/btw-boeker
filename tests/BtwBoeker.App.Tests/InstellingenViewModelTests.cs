using BtwBoeker.App.ViewModels;

namespace BtwBoeker.App.Tests;

/// <summary>
/// UX-review stap 6 (besluit 42): Instellingen bewaart direct, zoals Windows-Instellingen. De naam wordt bewaard bij het verlaten
/// van het veld of bij Enter; een ongeldige naam wordt niet bewaard.
/// </summary>
public sealed class InstellingenViewModelTests
{
    private readonly InMemoryAdministratieRepository _repository = new();
    private readonly AdministratieViewModel _administratie;

    public InstellingenViewModelTests()
    {
        _administratie = new AdministratieViewModel(_repository.Opgeslagen);
    }

    private InstellingenViewModel Maak() => new(_repository, _administratie);

    [Fact]
    public void Zonder_wijzigingen_is_er_niets_te_bewaren()
    {
        var vm = Maak();

        Assert.Equal(_administratie.Naam, vm.Naam);
        Assert.False(vm.HeeftOnopgeslagenWijzigingen);
        Assert.True(vm.BewaartDirect);
    }

    [Fact]
    public async Task Een_bevestigde_naam_wordt_direct_bewaard()
    {
        var vm = Maak();
        vm.Naam = "Vakantiehuisje";

        await vm.BevestigNaamCommand.ExecuteAsync(null);

        Assert.Equal("Vakantiehuisje", _administratie.Naam);
        Assert.Equal(1, _repository.AantalKeerBewaard);
        Assert.False(vm.HeeftOnopgeslagenWijzigingen);
    }

    [Fact]
    public async Task Een_ongewijzigde_naam_wordt_niet_opnieuw_bewaard()
    {
        var vm = Maak();

        await vm.BevestigNaamCommand.ExecuteAsync(null);

        Assert.Equal(0, _repository.AantalKeerBewaard);
    }

    [Fact]
    public async Task Een_lege_naam_wordt_niet_bewaard()
    {
        var vm = Maak();
        var oud = _administratie.Naam;
        vm.Naam = " ";

        await vm.BevestigNaamCommand.ExecuteAsync(null);

        Assert.NotNull(vm.Fout);
        Assert.Equal(oud, _administratie.Naam);
        Assert.Equal(0, _repository.AantalKeerBewaard);
    }

    [Fact]
    public void Verlaten_met_een_ongeldige_naam_zet_de_bewaarde_naam_terug()
    {
        var vm = Maak();
        var oud = _administratie.Naam;
        vm.Naam = string.Empty;

        var melding = vm.Verlaten();

        Assert.Equal(oud, vm.Naam);
        Assert.Null(vm.Fout);
        Assert.Contains("niet bewaard", melding, StringComparison.Ordinal);
    }

    [Fact]
    public void Verlaten_zonder_fout_geeft_geen_melding()
    {
        Assert.Null(Maak().Verlaten());
    }
}
