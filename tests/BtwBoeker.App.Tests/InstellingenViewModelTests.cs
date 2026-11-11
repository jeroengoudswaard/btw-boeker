using BtwBoeker.App.ViewModels;

namespace BtwBoeker.App.Tests;

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
    public void Zonder_wijzigingen_is_opslaan_niet_mogelijk()
    {
        var vm = Maak();

        Assert.Equal(_administratie.Naam, vm.Naam);
        Assert.False(vm.HeeftOnopgeslagenWijzigingen);
        Assert.False(vm.OpslaanCommand.CanExecute(null));
    }

    [Fact]
    public async Task Opslaan_werkt_gedeelde_administratie_bij()
    {
        var vm = Maak();
        vm.Naam = "Vakantiehuisje";

        await vm.OpslaanCommand.ExecuteAsync(null);

        Assert.Equal("Vakantiehuisje", _administratie.Naam);
        Assert.Equal(1, _repository.AantalKeerBewaard);
        Assert.False(vm.HeeftOnopgeslagenWijzigingen);
    }

    [Fact]
    public void Lege_naam_geeft_foutmelding()
    {
        var vm = Maak();

        vm.Naam = " ";

        Assert.NotNull(vm.Fout);
        Assert.False(vm.OpslaanCommand.CanExecute(null));
    }

    [Fact]
    public async Task Drempel_zonder_bon_is_in_te_stellen()
    {
        var vm = Maak();
        Assert.Equal("10,00", vm.Drempeltekst);

        vm.Drempeltekst = "geen bedrag";
        Assert.NotNull(vm.Fout);
        Assert.False(vm.KanOpslaan);

        vm.Drempeltekst = "25";
        Assert.True(vm.KanOpslaan);
        await vm.OpslaanCommand.ExecuteAsync(null);

        Assert.Equal(2500, _administratie.Model.ZonderBonDrempel);
        Assert.Equal("25,00", vm.Drempeltekst);
        Assert.False(vm.HeeftOnopgeslagenWijzigingen);
    }
}
