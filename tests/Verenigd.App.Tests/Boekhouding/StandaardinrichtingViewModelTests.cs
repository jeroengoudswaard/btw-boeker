using Verenigd.App.ViewModels.Boekhouding;
using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.Tests.Boekhouding;

/// <summary>Standaardinrichting en sjablonen (besluiten 114–120).</summary>
public sealed class StandaardinrichtingViewModelTests : IDisposable
{
    private readonly BoekhoudingTestomgeving _omgeving = new();
    private readonly List<string> _bestanden = [];

    public void Dispose()
    {
        foreach (var pad in _bestanden)
        {
            File.Delete(pad);
        }

        _omgeving.Dispose();
    }

    private BoekhoudingDienst Dienst => new(_omgeving.Sessie, _omgeving.Tijd);

    private StandaardinrichtingViewModel Wizard(AdministratieSoort soort = AdministratieSoort.Vereniging, Rekeningsjabloon? sjabloon = null) =>
        new(Dienst, soort, sjabloon ?? Rekeningsjablonen.Standaard(soort), 2026);

    private string Bestand(string inhoud)
    {
        var pad = Path.Combine(Path.GetTempPath(), $"sjabloon-{Guid.NewGuid():N}.vsjabloon");
        File.WriteAllText(pad, inhoud);
        _bestanden.Add(pad);
        return pad;
    }

    [Fact]
    public async Task Vereniging_inrichten_met_speltakken_keuzes_en_beginsaldi()
    {
        var wizard = Wizard();

        Assert.True(wizard.HeeftSpeltakken);
        Assert.Equal(InrichtStap.Speltakken, wizard.Stap);
        Assert.Equal(["Bevers", "Welpen", "Scouts", "Explorers", "Roverscouts"], wizard.Speltakken.Select(s => s.Naam));
        wizard.Speltakken[0].IsGekozen = false;
        wizard.NieuweSpeltak = "Stam";
        wizard.SpeltakToevoegenCommand.Execute(null);
        Assert.Equal(string.Empty, wizard.NieuweSpeltak);
        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.Equal(InrichtStap.Rekeningen, wizard.Stap);
        Assert.Equal("Contributie Welpen", wizard.Rekeningen.Single(r => r.Nummer == 8010).Naam);
        Assert.Equal("Contributie Stam", wizard.Rekeningen.Single(r => r.Nummer == 8050).Naam);
        var reserve = wizard.Rekeningen.Single(r => r.Nummer == 600);
        Assert.True(reserve.IsVerplicht);
        reserve.IsGekozen = false;
        Assert.True(reserve.IsGekozen);
        wizard.Rekeningen.Single(r => r.Nummer == 4120).IsGekozen = false;
        wizard.Rekeningen.Single(r => r.Nummer == 4100).Naam = "Opkomsten";
        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.Equal(InrichtStap.Beginsaldi, wizard.Stap);
        Assert.Equal("Inrichten", wizard.Volgendetekst);
        Assert.All(wizard.Balansrekeningen, r => Assert.True(r.IsBalans));
        Assert.True(wizard.IsInEvenwicht);
        wizard.Balansrekeningen.Single(r => r.Nummer == 1100).Debettekst = "1.200,00";
        wizard.Balansrekeningen.Single(r => r.Nummer == 600).Credittekst = "1000";
        Assert.False(wizard.IsInEvenwicht);
        Assert.Equal("€ 200,00", wizard.Verschiltekst);
        Assert.False(wizard.VolgendeCommand.CanExecute(null));
        wizard.VerschilBewustBoeken = true;
        Assert.True(wizard.VolgendeCommand.CanExecute(null));
        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.True(wizard.IsKlaar, wizard.Foutmelding);
        var stand = await Dienst.LaadAsync(AdministratieSoort.Vereniging);
        Assert.Equal(wizard.Rekeningen.Count(r => r.IsGekozen) + 1, stand.Rekeningen.Count); // + 1299
        Assert.DoesNotContain(stand.Rekeningen, r => r.Nummer == 4120);
        Assert.Equal("Opkomsten", stand.Rekeningen.Single(r => r.Nummer == 4100).Naam);
        Assert.Contains(stand.Dagboeken, d => d.Code == "VRK");
        Assert.Contains("1299", wizard.Samenvatting, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stichting_begint_bij_de_rekeningen_en_een_ongeldig_bedrag_houdt_tegen()
    {
        var wizard = Wizard(AdministratieSoort.Stichting);

        Assert.False(wizard.HeeftSpeltakken);
        Assert.Equal(InrichtStap.Rekeningen, wizard.Stap);
        Assert.False(wizard.VorigeCommand.CanExecute(null));
        await wizard.VolgendeCommand.ExecuteAsync(null);
        var bank = wizard.Balansrekeningen.Single(r => r.Nummer == 1100);
        bank.Debettekst = "abc";
        Assert.NotNull(bank.Fout);
        Assert.False(wizard.VolgendeCommand.CanExecute(null));
        bank.Debettekst = string.Empty;
        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.True(wizard.IsKlaar);
        var stand = await Dienst.LaadAsync(AdministratieSoort.Stichting);
        Assert.Contains(stand.Rekeningen, r => r.Nummer == 200);
        Assert.Empty(stand.Posten);
    }

    [Fact]
    public void Een_sjabloon_voor_de_andere_administratie_geeft_een_waarschuwing()
    {
        Assert.Null(Wizard().Waarschuwing);
        Assert.Contains("Stichting", Wizard(AdministratieSoort.Vereniging, Rekeningsjablonen.Standaard(AdministratieSoort.Stichting)).Waarschuwing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tabblad_richt_in_met_standaardschema_en_exporteert_het_als_sjabloon()
    {
        var vm = _omgeving.Maak();
        await vm.VernieuwCommand.ExecuteAsync(null);
        Assert.True(vm.IsLeeg);
        _omgeving.Dialogen.Standaardinrichting = async wizard =>
        {
            while (!wizard.IsKlaar && wizard.VolgendeCommand.CanExecute(null))
            {
                await wizard.VolgendeCommand.ExecuteAsync(null);
            }

            return wizard.IsKlaar;
        };

        await vm.StandaardInrichtenCommand.ExecuteAsync(null);

        Assert.True(vm.IsIngericht);
        Assert.Contains(vm.Rekeningkeuzes, r => r.Naam == "Contributie Bevers");
        var doel = Bestand(string.Empty);
        _omgeving.Dialogen.SjabloonDoel = doel;
        await vm.ExporteerSjabloonCommand.ExecuteAsync(null);
        var export = Rekeningsjablonen.Lees(await File.ReadAllTextAsync(doel));
        Assert.Equal(vm.Rekeningkeuzes.Count, export.Rekeningen.Count);
        Assert.Contains("rekeningschema-vereniging", _omgeving.Dialogen.SjabloonVoorstel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tabblad_leest_een_sjabloonbestand_en_meldt_een_ongeldig_bestand()
    {
        var vm = _omgeving.Maak();
        await vm.VernieuwCommand.ExecuteAsync(null);
        _omgeving.Dialogen.Sjabloonbestand = Bestand("{ \"formaat\": \"iets\" }");

        await vm.SjabloonInlezenCommand.ExecuteAsync(null);
        Assert.Contains("geen rekeningschema-sjabloon", vm.Foutmelding, StringComparison.Ordinal);
        Assert.False(vm.IsIngericht);

        StandaardinrichtingViewModel? getoond = null;
        _omgeving.Dialogen.Standaardinrichting = wizard =>
        {
            getoond = wizard;
            return Task.FromResult(false);
        };
        _omgeving.Dialogen.Sjabloonbestand = Bestand(Rekeningsjablonen.Schrijf(Rekeningsjablonen.Standaard(AdministratieSoort.Stichting)));
        await vm.SjabloonInlezenCommand.ExecuteAsync(null);
        Assert.NotNull(getoond);
        Assert.Equal("Standaardschema Stichting (scouting)", getoond.Sjabloonnaam);
        Assert.False(vm.IsIngericht);
    }
}
