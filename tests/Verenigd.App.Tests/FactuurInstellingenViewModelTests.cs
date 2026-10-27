using Verenigd.App.ViewModels.Boekhouding;
using Verenigd.Application.Facturen;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.Tests;

/// <summary>Instellingen › Facturen (besluit 112). Het IBAN is een openbaar voorbeeldnummer.</summary>
public sealed class FactuurInstellingenViewModelTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private readonly FakeSessie _sessie = new(Path.Combine(Path.GetTempPath(), "facturen-test.vdb"), new InMemoryAdministratieRepository(), new InMemoryLedenOpslag());
    private readonly FakeDialoogService _dialogen = new();
    private readonly string _logo = Path.Combine(Path.GetTempPath(), $"logo-{Guid.NewGuid():N}.png");

    public void Dispose() => File.Delete(_logo);

    private FactuurInstellingenViewModel Maak() => new(new FactuurDienst(_sessie, TimeProvider.System), _dialogen);

    [Fact]
    public async Task Gegevens_en_logo_worden_gecontroleerd_en_opgeslagen()
    {
        var vm = Maak();
        await vm.LaadAsync();
        Assert.Equal("14", vm.Termijn);
        Assert.Equal("Geen logo", vm.Logotekst);
        Assert.False(vm.IsGewijzigd);

        vm.Naam = "Scouting Voorbeeld";
        vm.Iban = "NL91ABNA0417164301";
        Assert.NotNull(vm.Fout);
        vm.Iban = "NL91ABNA0417164300";
        vm.Termijn = "abc";
        Assert.NotNull(vm.Fout);
        vm.Termijn = "30";
        Assert.Null(vm.Fout);
        await File.WriteAllBytesAsync(_logo, Png);
        _dialogen.Afbeelding = _logo;
        vm.KiesLogoCommand.Execute(null);
        Assert.Contains("logo", vm.Logotekst, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.IsGewijzigd);

        await vm.BewaarAsync();

        Assert.False(vm.IsGewijzigd);
        var instellingen = (await new FactuurDienst(_sessie, TimeProvider.System).LaadAsync(AdministratieSoort.Vereniging)).Factuurinstellingen!;
        Assert.Equal("Scouting Voorbeeld", instellingen.Naam);
        Assert.Equal(30, instellingen.Termijn);
        Assert.Equal(Png, instellingen.Logo);

        var opnieuw = Maak();
        await opnieuw.LaadAsync();
        Assert.Equal("NL91 ABNA 0417 1643 00", opnieuw.Iban);
        opnieuw.WisLogoCommand.Execute(null);
        Assert.Equal("Geen logo", opnieuw.Logotekst);
        await opnieuw.BewaarAsync();
        Assert.Null((await new FactuurDienst(_sessie, TimeProvider.System).LaadAsync(AdministratieSoort.Vereniging)).Factuurinstellingen!.Logo);
    }

    [Fact]
    public async Task Een_ongeldige_afbeelding_geeft_een_foutmelding()
    {
        var vm = Maak();
        await vm.LaadAsync();
        await File.WriteAllBytesAsync(_logo, [1, 2, 3]);
        _dialogen.Afbeelding = _logo;

        vm.KiesLogoCommand.Execute(null);

        Assert.NotNull(vm.Fout);
        Assert.Equal("Geen logo", vm.Logotekst);
    }
}
