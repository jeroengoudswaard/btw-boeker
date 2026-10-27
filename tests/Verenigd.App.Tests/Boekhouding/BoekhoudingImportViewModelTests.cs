using Verenigd.App.ViewModels.Boekhouding;
using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.App.Tests.Boekhouding;

public sealed class BoekhoudingImportViewModelTests : IDisposable
{
    private readonly BoekhoudingTestomgeving _omgeving = new();

    public void Dispose() => _omgeving.Dispose();

    private BoekhoudingImportViewModel Maak() =>
        new(new BoekhoudingDienst(_omgeving.Sessie, _omgeving.Tijd), _omgeving.Lezer, AdministratieSoort.Vereniging, "boekhouding.xlsx");

    [Fact]
    public async Task Leest_rekeningen_en_beginbalans()
    {
        var wizard = Maak();

        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.Equal(BoekhoudingImportStap.Rekeningen, wizard.Stap);
        Assert.Equal(5, wizard.Rekeningen.Count);
        Assert.Equal(RekeningSoort.Resultaat, wizard.Rekeningen.Single(r => r.Nummer == 4115).Soort);

        await wizard.VolgendeCommand.ExecuteAsync(null);
        Assert.Equal(BoekhoudingImportStap.Beginbalans, wizard.Stap);
        Assert.True(wizard.IsInEvenwicht);
        Assert.Equal("Beginbalans per 01-01-2026", wizard.Beginbalanstitel);
    }

    [Fact]
    public async Task Soort_aanpassen_en_rekening_overslaan()
    {
        var wizard = Maak();
        await wizard.VolgendeCommand.ExecuteAsync(null);

        wizard.Rekeningen.Single(r => r.Nummer == 8020).Overslaan = true;
        await wizard.VolgendeCommand.ExecuteAsync(null);
        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.True(wizard.IsKlaar);
        var stand = await _omgeving.Opslag.LaadAsync(AdministratieSoort.Vereniging);
        Assert.DoesNotContain(stand.Rekeningen, r => r.Nummer == 8020);
    }

    [Fact]
    public async Task Verschil_alleen_na_bewuste_keuze()
    {
        _omgeving.Lezer.Blad = BoekhoudingTestomgeving.Voorblad(bank: 806.74);
        var wizard = Maak();
        await wizard.VolgendeCommand.ExecuteAsync(null);
        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.False(wizard.IsInEvenwicht);
        Assert.Equal("€ 7,24", wizard.Verschiltekst);
        Assert.False(wizard.VolgendeCommand.CanExecute(null));

        wizard.VerschilBewustBoeken = true;
        Assert.Equal("1299", wizard.Verschilnummer);
        Assert.True(wizard.VolgendeCommand.CanExecute(null));
        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.True(wizard.IsKlaar);
        Assert.True(wizard.VerschilGeboekt);
    }

    [Fact]
    public async Task Onleesbaar_bestand_geeft_foutmelding()
    {
        _omgeving.Lezer.Blad = null;
        var wizard = Maak();

        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.Equal(BoekhoudingImportStap.Bestand, wizard.Stap);
        Assert.Contains("gelezen", wizard.Foutmelding, StringComparison.Ordinal);
    }
}
