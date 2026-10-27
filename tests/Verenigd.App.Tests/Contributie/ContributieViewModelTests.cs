using Verenigd.App.ViewModels.Contributie;
using Verenigd.Application.Modules;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Gedeeld;

namespace Verenigd.App.Tests.Contributie;

public sealed class ContributieViewModelTests
{
    private readonly ContributieTestomgeving _omgeving = new();

    private async Task<ContributieViewModel> OpenMetLedenAsync()
    {
        await _omgeving.VulLedenAsync();
        await _omgeving.RichtIncassoInAsync();
        return await _omgeving.OpenAsync();
    }

    [Fact]
    public async Task Tabblad_hoort_bij_contributie_van_de_vereniging()
    {
        var vm = await OpenMetLedenAsync();

        Assert.Equal(ModuleSoort.Contributie, vm.Module.Soort);
        Assert.Equal("Contributie · Vereniging", vm.Titel);
    }

    [Fact]
    public async Task Bij_openen_ontstaan_standaardcategorieen_voor_speltakken()
    {
        var vm = await OpenMetLedenAsync();

        Assert.Equal(["Jeugdlid Junioren", "Jeugdlid Welpen", "Leiding", "Vrijgesteld"], vm.Categorieen.Select(c => c.Naam).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(_omgeving.Opslag.Categorieen, c => c.Naam.Contains("Migratie", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Leden_tonen_voorstel_en_waarschuwing_zonder_categorie()
    {
        var vm = await OpenMetLedenAsync();

        var anna = vm.Leden.Single(l => l.Naam == "Anna Vries");
        Assert.Null(anna.GekozenCategorie);
        Assert.Equal("Jeugdlid Welpen", anna.Voorstel?.Naam);
        Assert.Contains("categorie", anna.Waarschuwing, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Leiding", vm.Leden.Single(l => l.Naam == "Bo Jansen").Voorstel?.Naam);
    }

    [Fact]
    public async Task Voorstellen_overnemen_en_opslaan_met_ingangsdatum_lid_sinds()
    {
        var vm = await OpenMetLedenAsync();

        vm.VoorstellenOvernemenCommand.Execute(null);
        Assert.True(vm.HeeftOnopgeslagenWijzigingen);
        Assert.True(await vm.BewaarAsync());

        Assert.Equal(3, _omgeving.Opslag.LidCategorieen.Count);
        var anna = _omgeving.Opslag.LidCategorieen.Single(c => c.Lidnummer == Lidnummer.Parse("100000001"));
        Assert.Equal("Jeugdlid Welpen", anna.Categorie.Naam);
        Assert.Equal(new DateOnly(2018, 11, 7), anna.Ingangsdatum); // Lid sinds (inclusief technische groep)
        Assert.False(vm.HeeftOnopgeslagenWijzigingen);
    }

    [Fact]
    public async Task Categoriewissel_krijgt_standaard_de_eerste_van_volgende_maand()
    {
        var vm = await OpenMetLedenAsync();
        vm.VoorstellenOvernemenCommand.Execute(null);
        await vm.BewaarAsync();

        var anna = vm.Leden.Single(l => l.Naam == "Anna Vries");
        anna.GekozenCategorie = vm.Categorieen.Single(c => c.Naam == "Jeugdlid Junioren");

        Assert.Equal(new DateTime(2027, 3, 1), anna.Ingangsdatum);
        await vm.BewaarAsync();
        Assert.Equal(4, _omgeving.Opslag.LidCategorieen.Count); // historie blijft bewaard
    }

    [Fact]
    public async Task Tarieven_per_boekjaar_bewerken_en_kopieren()
    {
        var vm = await OpenMetLedenAsync();
        Assert.Equal(2027, vm.Boekjaar);

        var welpen = vm.Tarieven.Single(t => t.Categorie == "Jeugdlid Welpen");
        Assert.False(vm.Tarieven.Single(t => t.Categorie == "Leiding").IsBewerkbaar);
        welpen.Bedragtekst = "12,5o";
        Assert.NotNull(welpen.Fout);
        Assert.False(vm.KanOpslaan);

        welpen.Bedragtekst = "12,50";
        Assert.True(await vm.BewaarAsync());
        Assert.Equal(new Bedrag(1250), _omgeving.Opslag.Tarieven.Single(t => t.Boekjaar == 2027 && t.Categorie.Naam == "Jeugdlid Welpen").Maandbedrag);

        vm.Boekjaar = 2028;
        Assert.Equal("", vm.Tarieven.Single(t => t.Categorie == "Jeugdlid Welpen").Bedragtekst);
        vm.KopieerVorigJaarCommand.Execute(null);
        Assert.Equal("12,50", vm.Tarieven.Single(t => t.Categorie == "Jeugdlid Welpen").Bedragtekst);
    }

    [Fact]
    public async Task Nieuwe_betaler_met_machtiging_en_leden()
    {
        var vm = await OpenMetLedenAsync();

        vm.NieuweBetalerCommand.Execute(null);
        var editor = vm.Betaler!;
        editor.Iban = "nl91 abna 0417 1643 00";
        editor.Rekeninghouder = "P. de Vries";
        editor.Mandaatkenmerk = "VER-0001";
        Assert.NotEmpty(editor.Fouten); // ondertekeningsdatum ontbreekt
        editor.OndertekendOp = new DateTime(2019, 7, 1);
        editor.LidToevoegen = editor.BeschikbareLeden.Single(l => l.Naam == "Anna Vries");
        editor.VoegLidToeCommand.Execute(null);
        Assert.Empty(editor.Fouten);

        Assert.True(await vm.BewaarAsync());

        var betaler = Assert.Single(_omgeving.Opslag.Betalers);
        Assert.Equal("VER-0001", betaler.Mandaatkenmerk);
        Assert.Equal(betaler.Id, Assert.Single(_omgeving.Opslag.Koppelingen).BetalerId);
        var regel = Assert.Single(vm.Betalers);
        Assert.Equal("NL91 ABNA 0417 1643 00", regel.Iban);
        Assert.Equal(1, regel.AantalLeden);
        Assert.Equal("P. de Vries", vm.Leden.Single(l => l.Naam == "Anna Vries").Betalertekst);
    }

    [Fact]
    public async Task Ongeldig_iban_kan_niet_worden_opgeslagen()
    {
        var vm = await OpenMetLedenAsync();
        vm.NieuweBetalerCommand.Execute(null);

        vm.Betaler!.Iban = "NL92ABNA0417164300";

        Assert.Contains(vm.Betaler.Fouten, f => f.Contains("ongeldig", StringComparison.OrdinalIgnoreCase));
        Assert.False(vm.KanOpslaan);
    }

    [Fact]
    public async Task Run_annuleren_vraagt_bevestiging()
    {
        var vm = await OpenMetLedenAsync();
        var run = new Incassorun(new Maand(2027, 3), new DateOnly(2027, 3, 1), DateTime.UtcNow);
        run.VoegPostToe(Guid.NewGuid(), Iban.Maak(ContributieTestomgeving.IbanA), "x", "VER-1", new DateOnly(2020, 1, 1), "x")
            .VoegRegelToe(Lidnummer.Parse("100000001"), new Maand(2027, 3), "Jeugdlid Welpen", new Bedrag(1250));
        _omgeving.Opslag.Runs.Add(run);
        await vm.VernieuwCommand.ExecuteAsync(null);
        var regel = Assert.Single(vm.Runs);
        Assert.Equal("Definitief", regel.Statustekst);

        _omgeving.Dialogen.BevestigAntwoord = false;
        await vm.AnnuleerRunCommand.ExecuteAsync(regel);
        Assert.Equal(IncassorunStatus.Definitief, run.Status);

        _omgeving.Dialogen.BevestigAntwoord = true;
        await vm.AnnuleerRunCommand.ExecuteAsync(regel);
        Assert.Equal(IncassorunStatus.Geannuleerd, run.Status);
        Assert.Equal("Geannuleerd", Assert.Single(vm.Runs).Statustekst);
    }

    [Fact]
    public async Task Bestand_van_bestaande_run_opnieuw_opslaan()
    {
        var vm = await OpenMetLedenAsync();
        var run = new Incassorun(new Maand(2027, 3), new DateOnly(2027, 3, 1), DateTime.UtcNow);
        run.VoegPostToe(Guid.NewGuid(), Iban.Maak(ContributieTestomgeving.IbanA), "x", "VER-1", new DateOnly(2020, 1, 1), "x")
            .VoegRegelToe(Lidnummer.Parse("100000001"), new Maand(2027, 3), "Jeugdlid Welpen", new Bedrag(1250));
        _omgeving.Opslag.Runs.Add(run);
        await vm.VernieuwCommand.ExecuteAsync(null);
        var doel = Path.Combine(Path.GetTempPath(), $"incasso-{Guid.NewGuid():N}.xml");
        _omgeving.Dialogen.XmlDoel = doel;

        try
        {
            await vm.BestandOpslaanCommand.ExecuteAsync(vm.Runs[0]);

            Assert.Contains("<SeqTp>RCUR</SeqTp>", await File.ReadAllTextAsync(doel), StringComparison.Ordinal);
            Assert.NotNull(run.BestandHash);
        }
        finally
        {
            File.Delete(doel);
        }
    }
}
