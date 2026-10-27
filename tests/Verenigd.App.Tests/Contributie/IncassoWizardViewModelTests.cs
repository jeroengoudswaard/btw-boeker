using Verenigd.App.ViewModels.Contributie;
using Verenigd.Application.Incasso;
using Verenigd.Domain.Financien;

namespace Verenigd.App.Tests.Contributie;

public sealed class IncassoWizardViewModelTests : IDisposable
{
    private readonly ContributieTestomgeving _omgeving = new();
    private readonly DirectoryInfo _map = Directory.CreateTempSubdirectory("verenigd-incasso-");

    public void Dispose() => _map.Delete(recursive: true);

    private async Task<IncassoWizardViewModel> MaakAsync(bool metBetaler = true)
    {
        await _omgeving.VulLedenAsync();
        await _omgeving.RichtIncassoInAsync();
        var vm = await _omgeving.OpenAsync();
        vm.VoorstellenOvernemenCommand.Execute(null);
        vm.Tarieven.Single(t => t.Categorie == "Jeugdlid Welpen").Bedragtekst = "12,50";
        vm.Tarieven.Single(t => t.Categorie == "Jeugdlid Junioren").Bedragtekst = "15,00";
        await vm.BewaarAsync();
        if (metBetaler)
        {
            await _omgeving.Opslag.BewaarBetalerAsync(new BetalerInvoer(null, ContributieTestomgeving.IbanA, "P. Test", "VER-0001",
                new DateOnly(2019, 7, 1), null, [Domain.Gedeeld.Lidnummer.Parse("100000001")]));
        }

        return new IncassoWizardViewModel(new IncassoDienst(_omgeving.Sessie, _omgeving.Tijd), _omgeving.Dialogen, _omgeving.Tijd);
    }

    [Fact]
    public async Task Start_met_maandkeuze_volgende_maand()
    {
        var wizard = await MaakAsync();

        Assert.Equal(IncassoStap.Maand, wizard.Stap);
        Assert.Equal(new Maand(2027, 3), wizard.GekozenMaand); // 20 feb, incassodag 1 → maart
        Assert.True(wizard.VolgendeCommand.CanExecute(null));
    }

    [Fact]
    public async Task Berekening_toont_posten_en_zelf_te_innen()
    {
        var wizard = await MaakAsync();

        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.Equal(IncassoStap.Berekening, wizard.Stap);
        var post = Assert.Single(wizard.Posten);
        Assert.Equal("€ 37,50", post.Bedrag); // jan–mrt naheffing
        Assert.Equal(3, post.Regels.Count);
        Assert.Contains(wizard.ZelfTeInnen, z => z.Naam == "Cas Bakker" && z.Reden.Contains("betaler", StringComparison.Ordinal));
        Assert.Equal("1 post · € 37,50", wizard.Totaaltekst);
    }

    [Fact]
    public async Task Definitief_maken_slaat_xml_en_csv_op()
    {
        var wizard = await MaakAsync();
        _omgeving.Dialogen.XmlDoel = Path.Combine(_map.FullName, "incasso.xml");
        _omgeving.Dialogen.CsvDoel = Path.Combine(_map.FullName, "zelf.csv");

        await wizard.VolgendeCommand.ExecuteAsync(null);
        await wizard.VolgendeCommand.ExecuteAsync(null);
        Assert.Equal(IncassoStap.Controleren, wizard.Stap);
        Assert.Equal("Definitief maken", wizard.Volgendetekst);
        Assert.False(wizard.VolgendeCommand.CanExecute(null)); // eerst bevestigen
        wizard.IsGecontroleerd = true;
        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.Equal(IncassoStap.Klaar, wizard.Stap);
        Assert.Single(_omgeving.Opslag.Runs);
        Assert.Single(_omgeving.Sessie.Backups);
        Assert.Contains("<SeqTp>RCUR</SeqTp>", await File.ReadAllTextAsync(_omgeving.Dialogen.XmlDoel), StringComparison.Ordinal);
        Assert.Contains("Cas Bakker", await File.ReadAllTextAsync(_omgeving.Dialogen.CsvDoel), StringComparison.Ordinal);
        Assert.Contains("incasso.xml", wizard.Samenvatting, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Opslaan_geannuleerd_laat_run_definitief_met_hint()
    {
        var wizard = await MaakAsync();
        await wizard.VolgendeCommand.ExecuteAsync(null);
        await wizard.VolgendeCommand.ExecuteAsync(null);
        wizard.IsGecontroleerd = true;

        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.Equal(IncassoStap.Klaar, wizard.Stap);
        Assert.Single(_omgeving.Opslag.Runs);
        Assert.Contains("later", wizard.Samenvatting, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Niets_te_incasseren_houdt_volgende_uit()
    {
        var wizard = await MaakAsync(metBetaler: false);

        await wizard.VolgendeCommand.ExecuteAsync(null);
        await wizard.VolgendeCommand.ExecuteAsync(null);
        wizard.IsGecontroleerd = true;

        Assert.True(wizard.IsLeeg);
        Assert.False(wizard.VolgendeCommand.CanExecute(null));
    }

    [Fact]
    public async Task Zonder_incassogegevens_foutmelding()
    {
        await _omgeving.VulLedenAsync();
        var wizard = new IncassoWizardViewModel(new IncassoDienst(_omgeving.Sessie, _omgeving.Tijd), _omgeving.Dialogen, _omgeving.Tijd);

        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.Equal(IncassoStap.Maand, wizard.Stap);
        Assert.Contains("Instellingen", wizard.Foutmelding, StringComparison.Ordinal);
    }
}
