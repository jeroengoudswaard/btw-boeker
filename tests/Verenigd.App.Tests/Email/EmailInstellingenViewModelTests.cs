using Verenigd.App.ViewModels.Email;
using Verenigd.Application.Email;

namespace Verenigd.App.Tests.Email;

/// <summary>Instellingen › E-mail (besluiten 192 en 193): afzender, clientbestand en koppelen met Google. Gegevens zijn verzonnen.</summary>
public sealed class EmailInstellingenViewModelTests : IDisposable
{
    private readonly InMemoryEmailOpslag _opslag = new();
    private readonly FakeDialoogService _dialogen = new();
    private readonly NepKoppeling _koppeling = new();
    private readonly string _clientbestand = Path.Combine(Path.GetTempPath(), $"client-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_clientbestand);

    private async Task<EmailInstellingenViewModel> MaakAsync()
    {
        var sessie = new FakeSessie("test.vdb", new InMemoryAdministratieRepository(), new InMemoryLedenOpslag()) { Email = _opslag };
        var vm = new EmailInstellingenViewModel(new EmailDienst(sessie, new NepVerzender(), TimeProvider.System), _koppeling, _dialogen);
        await vm.LaadCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Afzender_wordt_gecontroleerd_en_opgeslagen()
    {
        var vm = await MaakAsync();
        Assert.False(vm.IsGewijzigd);
        Assert.Contains("Niet gekoppeld", vm.Status, StringComparison.Ordinal);

        vm.Afzendernaam = "Penningmeester";
        vm.Afzenderadres = "geen adres";
        Assert.True(vm.IsGewijzigd);
        Assert.NotNull(vm.Fout);

        vm.Afzenderadres = "penningmeester@example.org";
        Assert.Null(vm.Fout);
        await vm.BewaarAsync();

        Assert.False(vm.IsGewijzigd);
        Assert.Equal("penningmeester@example.org", _opslag.Instellingen.Afzenderadres);
    }

    [Fact]
    public async Task Clientbestand_inlezen_en_koppelen_bewaart_de_sleutel()
    {
        var vm = await MaakAsync();
        Assert.False(vm.KoppelCommand.CanExecute(null));

        await File.WriteAllTextAsync(_clientbestand, """{"installed":{"client_id":"123.apps.googleusercontent.com","client_secret":"geheim"}}""");
        _dialogen.Clientbestand = _clientbestand;
        await vm.LeesClientbestandCommand.ExecuteAsync(null);
        Assert.Equal("123.apps.googleusercontent.com", _opslag.Instellingen.ClientId);
        Assert.True(vm.KoppelCommand.CanExecute(null));

        await vm.KoppelCommand.ExecuteAsync(null);

        Assert.Equal(1, _koppeling.Aantal);
        Assert.True(vm.IsGekoppeld);
        Assert.Contains("Gekoppeld", vm.Status, StringComparison.Ordinal);
        Assert.Equal("vernieuwd", _opslag.Instellingen.Vernieuwingssleutel);
    }

    [Fact]
    public async Task Ongeldig_clientbestand_geeft_een_melding()
    {
        var vm = await MaakAsync();
        await File.WriteAllTextAsync(_clientbestand, """{"web":{}}""");
        _dialogen.Clientbestand = _clientbestand;

        await vm.LeesClientbestandCommand.ExecuteAsync(null);

        Assert.NotNull(vm.Fout);
        Assert.Null(_opslag.Instellingen.ClientId);
    }

    [Fact]
    public async Task Ontkoppelen_na_bevestiging_wist_de_sleutel()
    {
        _opslag.Koppel();
        var vm = await MaakAsync();
        Assert.True(vm.IsGekoppeld);

        await vm.OntkoppelCommand.ExecuteAsync(null);

        Assert.Single(_dialogen.Bevestigingsvragen);
        Assert.False(vm.IsGekoppeld);
        Assert.Null(_opslag.Instellingen.Vernieuwingssleutel);
    }
}
