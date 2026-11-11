using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.Application.Bank;
using BtwBoeker.Application.Tests.Bank;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.App.Tests.Boekhouding;

public sealed class BankImportViewModelTests : IDisposable
{
    private readonly BoekhoudingTestomgeving _omgeving = new();

    public void Dispose() => _omgeving.Dispose();

    private async Task<BankImportViewModel> MaakAsync(params string[] paden)
    {
        await _omgeving.OpenMetBankrekeningenAsync();
        var stand = await _omgeving.Opslag.LaadAsync(AdministratieSoort.Standaard);
        return new BankImportViewModel(new BankDienst(_omgeving.Sessie, _omgeving.Tijd), stand, AdministratieSoort.Standaard, paden);
    }

    private string Betaal(long begin = 79950) => _omgeving.CamtBestand(CamtVoorbeeld.Betaal,
        new VbAfschrift(new DateOnly(2026, 1, 1), begin),
        new VbAfschrift(new DateOnly(2026, 1, 2), begin, new VbTransactie(-4500, "ICDT", "ESCT", "520", Ref: "E1", Naam: "Energie BV")));

    [Fact]
    public async Task Nieuwe_rekening_kiezen_en_importeren()
    {
        var wizard = await MaakAsync(Betaal(), _omgeving.CamtBestand(CamtVoorbeeld.Spaar, new VbAfschrift(new DateOnly(2026, 1, 1), 0)));

        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.Equal(BankImportStap.Controleren, wizard.Stap);
        Assert.Equal(2, wizard.Rekeningen.Count);
        var betaal = wizard.Rekeningen.Single(r => r.Iban == CamtVoorbeeld.Betaal);
        Assert.True(betaal.IsNieuw);
        Assert.Equal(1, betaal.AantalNieuw);
        Assert.Equal("RABO", betaal.Code);
        Assert.NotEqual(betaal.Code, wizard.Rekeningen.Single(r => r.Iban == CamtVoorbeeld.Spaar).Code);
        Assert.False(wizard.VolgendeCommand.CanExecute(null)); // eerst een grootboekrekening kiezen

        betaal.GekozenRekening = wizard.Balansrekeningen.Single(r => r.Nummer == 1100);
        wizard.Rekeningen.Single(r => r.Iban == CamtVoorbeeld.Spaar).GekozenRekening = wizard.Balansrekeningen.Single(r => r.Nummer == 1150);
        Assert.Empty(betaal.Meldingen); // beginsaldo € 799,50 = beginbalans 1100
        Assert.True(wizard.VolgendeCommand.CanExecute(null));

        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.True(wizard.IsKlaar);
        Assert.Equal(new BankimportResultaat(1, 0, 1), wizard.Resultaat);
        Assert.Contains("werkvoorraad", wizard.Samenvatting, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Afwijkend_beginsaldo_geeft_waarschuwing_maar_blokkeert_niet()
    {
        var wizard = await MaakAsync(Betaal(begin: 80674));
        await wizard.VolgendeCommand.ExecuteAsync(null);
        var rekening = wizard.Rekeningen.Single();

        rekening.GekozenRekening = wizard.Balansrekeningen.Single(r => r.Nummer == 1100);

        Assert.Contains(rekening.Meldingen, m => m.Contains("€ 806,74", StringComparison.Ordinal));
        Assert.True(wizard.VolgendeCommand.CanExecute(null));
    }

    [Fact]
    public async Task Onleesbaar_bestand_geeft_foutmelding()
    {
        var pad = Path.Combine(Path.GetTempPath(), $"kapot-{Guid.NewGuid():N}.xml");
        await File.WriteAllTextAsync(pad, "geen xml");
        try
        {
            var wizard = await MaakAsync(pad);

            await wizard.VolgendeCommand.ExecuteAsync(null);

            Assert.Equal(BankImportStap.Bestanden, wizard.Stap);
            Assert.Contains(Path.GetFileName(pad), wizard.Foutmelding, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(pad);
        }
    }
}
