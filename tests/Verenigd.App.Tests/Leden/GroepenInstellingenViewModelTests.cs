using Verenigd.App.ViewModels.Leden;
using Verenigd.Domain.Leden;
using static Verenigd.App.Tests.Leden.LedenTestdata;

namespace Verenigd.App.Tests.Leden;

/// <summary>Instellingen › Groepen (besluit 215): de groepsindeling ook buiten de import wijzigen.</summary>
public sealed class GroepenInstellingenViewModelTests
{
    private readonly InMemoryLedenOpslag _opslag = new();

    [Fact]
    public async Task Soort_per_groep_wijzigen_en_opslaan()
    {
        await VulAsync(_opslag, Lid("100000001", "Anna", "Vries", "Welpen"), Lid("100000002", "Kees", "Jansen", "Bestuur", Lidtype.Kaderlid));
        var vm = new GroepenInstellingenViewModel(_opslag);
        await vm.LaadCommand.ExecuteAsync(null);

        Assert.Equal(["Bestuur", "Vrijwilligers (Migratie)", "Welpen"], vm.Regels.Select(r => r.Naam));
        Assert.False(vm.IsGewijzigd);

        vm.Regels.Single(r => r.Naam == "Bestuur").Soort = GroepSoort.Overig;
        Assert.True(vm.IsGewijzigd);
        await vm.BewaarAsync();

        Assert.False(vm.IsGewijzigd);
        Assert.Equal(GroepSoort.Overig, _opslag.Stand.Groepen.Single(g => g.Naam == "Bestuur").Soort);
    }

    [Fact]
    public async Task Zonder_groepen_is_er_een_uitleg()
    {
        var vm = new GroepenInstellingenViewModel(_opslag);
        await vm.LaadCommand.ExecuteAsync(null);

        Assert.Empty(vm.Regels);
        Assert.False(vm.IsGewijzigd);
    }
}
