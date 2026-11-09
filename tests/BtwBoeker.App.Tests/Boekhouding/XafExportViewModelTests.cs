using BtwBoeker.Application.Boekhouding;

namespace BtwBoeker.App.Tests.Boekhouding;

/// <summary>Besluiten 197 en 255–259: Bestand › Exporteren › Auditfile (XAF) en de knop bij Boekjaren.</summary>
public sealed class XafExportViewModelTests : IDisposable
{
    private readonly BoekhoudingTestomgeving _omgeving = new();
    private readonly string _doel = Path.Combine(Path.GetTempPath(), $"auditfile-{Guid.NewGuid():N}.xaf");

    public void Dispose()
    {
        File.Delete(_doel);
        _omgeving.Dispose();
    }

    /// <summary>Ingericht via de Excel-overname: rekeningschema plus een geboekte openingsbalans 2026.</summary>
    private async Task<ViewModels.Boekhouding.BoekhoudingViewModel> IngerichtAsync()
    {
        var vm = _omgeving.Maak();
        await vm.VernieuwCommand.ExecuteAsync(null);
        _omgeving.Dialogen.ExcelBestand = "boekhouding.xlsx";
        _omgeving.Dialogen.BoekhoudingImport = async wizard =>
        {
            await wizard.VolgendeCommand.ExecuteAsync(null);
            await wizard.VolgendeCommand.ExecuteAsync(null);
            await wizard.VolgendeCommand.ExecuteAsync(null);
            return wizard.IsKlaar;
        };
        await vm.OvernemenUitExcelCommand.ExecuteAsync(null);
        Assert.True(vm.IsIngericht);
        return vm;
    }

    [Fact]
    public async Task Exporteert_het_gekozen_boekjaar_als_geldige_auditfile()
    {
        var vm = await IngerichtAsync();
        _omgeving.Dialogen.XafDoel = _doel;

        await vm.ExporteerXafCommand.ExecuteAsync(null);

        Assert.Null(vm.Foutmelding);
        var inhoud = await File.ReadAllBytesAsync(_doel);
        Assert.Empty(XafSchrijver.Valideer(inhoud));
        Assert.Contains("<openingBalance>", System.Text.Encoding.UTF8.GetString(inhoud), StringComparison.Ordinal);
        Assert.Equal(XafDienst.Bestandsnaam(BoekhoudingTestomgeving.Vereniging.Naam, vm.Jaar), _omgeving.Dialogen.XafVoorstel);
        Assert.Matches("^[✓⚠] Auditfile 2026", vm.Melding);
        Assert.Contains("auditfile", vm.Melding, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Annuleren_schrijft_niets()
    {
        var vm = await IngerichtAsync();
        _omgeving.Dialogen.XafDoel = null;

        await vm.ExporteerXafCommand.ExecuteAsync(null);

        Assert.False(File.Exists(_doel));
        Assert.Null(vm.Foutmelding);
    }
}
