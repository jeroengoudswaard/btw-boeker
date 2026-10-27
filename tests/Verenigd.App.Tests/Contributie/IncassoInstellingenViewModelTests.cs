using Verenigd.App.ViewModels.Contributie;
using Verenigd.Domain.Financien;

namespace Verenigd.App.Tests.Contributie;

public sealed class IncassoInstellingenViewModelTests
{
    private readonly ContributieTestomgeving _omgeving = new();

    [Fact]
    public async Task Lege_instellingen_vragen_om_invoer_en_valideren()
    {
        var vm = new IncassoInstellingenViewModel(_omgeving.Opslag, _omgeving.Tijd);
        await vm.LaadAsync();

        Assert.False(vm.IsGewijzigd);
        vm.IncassantId = "DE97ZZZ09999999999";
        vm.Naam = "Scouting";
        vm.Iban = ContributieTestomgeving.IbanA;

        Assert.Contains("incassant", vm.Fout, StringComparison.OrdinalIgnoreCase);
        vm.IncassantId = "DE98ZZZ09999999999";
        vm.Incassodag = 29;
        Assert.NotNull(vm.Fout);
        vm.Incassodag = 25;
        Assert.Null(vm.Fout);
        Assert.True(vm.IsGewijzigd);

        await vm.BewaarAsync();

        var opgeslagen = _omgeving.Opslag.Instellingen!;
        Assert.Equal(25, opgeslagen.Incassodag);
        Assert.Equal(new Maand(2027, 2), opgeslagen.EersteIncassomaand); // standaard: huidige maand
        Assert.False(vm.IsGewijzigd);
    }

    [Fact]
    public async Task Bestaande_instellingen_worden_geladen()
    {
        await _omgeving.RichtIncassoInAsync();
        var vm = new IncassoInstellingenViewModel(_omgeving.Opslag, _omgeving.Tijd);

        await vm.LaadAsync();

        Assert.Equal("DE98ZZZ09999999999", vm.IncassantId);
        Assert.Equal(new Maand(2027, 1), vm.EersteIncassomaand);
        Assert.Equal(Incassoinstellingen.StandaardSjabloon, vm.Omschrijvingssjabloon);
        Assert.Contains("maart 2027", vm.Voorbeeld, StringComparison.Ordinal);
    }
}
