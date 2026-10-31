using Verenigd.App.ViewModels;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.Tests;

public sealed class InstellingenViewModelTests
{
    private readonly InMemoryAdministratieRepository _repository = new();
    private readonly List<AdministratieViewModel> _administraties;

    public InstellingenViewModelTests()
    {
        _administraties = _repository.Opgeslagen.Select(a => new AdministratieViewModel(a)).ToList();
    }

    private InstellingenViewModel Maak() => new(_repository, _administraties);

    [Fact]
    public void Zonder_wijzigingen_is_opslaan_niet_mogelijk()
    {
        var vm = Maak();

        Assert.False(vm.HeeftOnopgeslagenWijzigingen);
        Assert.False(vm.OpslaanCommand.CanExecute(null));
    }

    [Fact]
    public async Task Opslaan_werkt_gedeelde_administratie_bij()
    {
        var vm = Maak();
        vm.Regels[1].KleurHex = "#6A1B9A";
        vm.Regels[1].Naam = "Stichting Clubhuis";

        await vm.OpslaanCommand.ExecuteAsync(null);

        Assert.Equal(Kleur.Parse("#6A1B9A"), _administraties[1].Accentkleur);
        Assert.Equal("Stichting Clubhuis", _administraties[1].Naam);
        Assert.Equal(1, _repository.AantalKeerBewaard);
        Assert.False(vm.HeeftOnopgeslagenWijzigingen);
    }

    [Theory]
    [InlineData("groen")]
    [InlineData("#12345")]
    public void Ongeldige_kleur_geeft_foutmelding_en_blokkeert_opslaan(string kleur)
    {
        var vm = Maak();

        vm.Regels[0].KleurHex = kleur;

        Assert.NotNull(vm.Regels[0].Fout);
        Assert.False(vm.OpslaanCommand.CanExecute(null));
    }

    [Fact]
    public void Lege_naam_geeft_foutmelding()
    {
        var vm = Maak();

        vm.Regels[0].Naam = " ";

        Assert.NotNull(vm.Regels[0].Fout);
        Assert.False(vm.OpslaanCommand.CanExecute(null));
    }

    [Fact]
    public void Kleurvoorstel_kiezen_vult_hexcode()
    {
        var vm = Maak();

        vm.Regels[0].KiesKleurCommand.Execute("#C62828");

        Assert.Equal("#C62828", vm.Regels[0].KleurHex);
    }

    [Fact]
    public void Kleurvoorstellen_zijn_leesbaar_met_witte_of_zwarte_tekst()
    {
        // WCAG AA voor grote tekst (3:1) op de gekleurde band.
        Assert.All(
            AdministratieInstellingRegel.Kleurvoorstellen,
            hex =>
            {
                var kleur = Kleur.Parse(hex);
                Assert.True(kleur.Contrastverhouding(kleur.LeesbareTekstkleur()) >= 3.0, hex);
            });
    }

    [Fact]
    public async Task Drempel_zonder_bon_is_per_administratie_in_te_stellen()
    {
        var vm = Maak();
        Assert.Equal("10,00", vm.Regels[1].Drempeltekst);

        vm.Regels[1].Drempeltekst = "geen bedrag";
        Assert.NotNull(vm.Regels[1].Fout);
        Assert.False(vm.KanOpslaan);

        vm.Regels[1].Drempeltekst = "25";
        Assert.True(vm.KanOpslaan);
        await vm.OpslaanCommand.ExecuteAsync(null);

        Assert.Equal(2500, _administraties[1].Model.ZonderBonDrempel);
        Assert.Equal(Administratie.StandaardZonderBonDrempel, _administraties[0].Model.ZonderBonDrempel);
        Assert.Equal("25,00", vm.Regels[1].Drempeltekst);
        Assert.False(vm.HeeftOnopgeslagenWijzigingen);
    }
}
