using Verenigd.App.ViewModels.Boekhouding;
using Verenigd.Application.Modules;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.App.Tests.Boekhouding;

public sealed class BoekhoudingViewModelTests : IDisposable
{
    private readonly BoekhoudingTestomgeving _omgeving = new();

    public void Dispose() => _omgeving.Dispose();

    private static void VulRegel(PostEditor editor, int index, int nummer, string debet, string credit)
    {
        while (editor.Regels.Count <= index)
        {
            editor.NieuweRegelCommand.Execute(null);
        }

        editor.Regels[index].GekozenRekening = editor.Rekeningen.Single(r => r.Nummer == nummer);
        editor.Regels[index].Debettekst = debet;
        editor.Regels[index].Credittekst = credit;
    }

    [Fact]
    public async Task Na_laden_van_een_lege_administratie_meldt_IsLeeg_zich_zodat_de_startkaart_verschijnt()
    {
        var vm = _omgeving.Maak();
        var gemeld = new List<string?>();
        vm.PropertyChanged += (_, e) => gemeld.Add(e.PropertyName);

        await vm.VernieuwCommand.ExecuteAsync(null);

        Assert.True(vm.IsLeeg);
        Assert.Contains(nameof(BoekhoudingViewModel.IsLeeg), gemeld);
    }

    [Fact]
    public async Task Lege_administratie_toont_overname_en_leeg_beginnen()
    {
        var vm = _omgeving.Maak();
        await vm.VernieuwCommand.ExecuteAsync(null);

        Assert.Equal(ModuleSoort.Boekhouding, vm.Module.Soort);
        Assert.False(vm.IsIngericht);

        await vm.LeegBeginnenCommand.ExecuteAsync(null);

        Assert.True(vm.IsIngericht);
        Assert.Equal([2026], vm.Boekjaren);
    }

    [Fact]
    public async Task Overname_via_wizard_richt_de_administratie_in()
    {
        var vm = _omgeving.Maak();
        await vm.VernieuwCommand.ExecuteAsync(null);
        _omgeving.Dialogen.ExcelBestand = "boekhouding.xlsx";
        _omgeving.Dialogen.BoekhoudingImport = async wizard =>
        {
            await wizard.VolgendeCommand.ExecuteAsync(null); // bestand → rekeningen
            await wizard.VolgendeCommand.ExecuteAsync(null); // rekeningen → beginbalans
            await wizard.VolgendeCommand.ExecuteAsync(null); // importeren
            return wizard.IsKlaar;
        };

        await vm.OvernemenUitExcelCommand.ExecuteAsync(null);

        Assert.True(vm.IsIngericht);
        Assert.Equal("MEM-2026-0001", Assert.Single(vm.Posten).Nummer);
        Assert.Equal(5, vm.Rekeningen.Count);
    }

    [Fact]
    public async Task Nieuwe_post_boeken_met_ctrl_enter()
    {
        var vm = await _omgeving.OpenIngerichtAsync();

        vm.NieuwePostCommand.Execute(null);
        var editor = vm.Editor!;
        Assert.Equal("MEM", editor.GekozenDagboek?.Code);
        Assert.Equal(new DateTime(2026, 6, 15), editor.Datum);
        editor.Omschrijving = "Energie juni";
        VulRegel(editor, 0, 4115, "300,00", "");
        VulRegel(editor, 1, 1100, "", "250,00");

        Assert.Equal("€ 50,00", editor.Verschiltekst);
        Assert.False(vm.BoekenCommand.CanExecute(null));

        editor.Regels[1].Credittekst = "300,00";
        Assert.True(editor.IsInEvenwicht);
        Assert.True(vm.BoekenCommand.CanExecute(null));
        await vm.BoekenCommand.ExecuteAsync(null);

        Assert.Contains(vm.Posten, p => p.Nummer == "MEM-2026-0002" && p.Status == "Geboekt");
        Assert.True(vm.Editor!.IsAlleenLezen);
        Assert.False(vm.HeeftOnopgeslagenWijzigingen);
    }

    [Fact]
    public async Task Concept_opslaan_met_ctrl_s_en_verwijderen_na_bevestiging()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        vm.NieuwePostCommand.Execute(null);
        vm.Editor!.Omschrijving = "Half af";
        VulRegel(vm.Editor, 0, 4115, "10,00", "");
        Assert.True(vm.HeeftOnopgeslagenWijzigingen);

        Assert.True(await vm.BewaarAsync());
        var concept = vm.Posten.Single(p => p.Status == "Concept");

        vm.GeselecteerdePost = concept;
        _omgeving.Dialogen.BevestigAntwoord = true;
        await vm.VerwijderConceptCommand.ExecuteAsync(null);

        Assert.DoesNotContain(vm.Posten, p => p.Status == "Concept");
    }

    [Fact]
    public async Task Geboekte_post_tegenboeken()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        vm.GeselecteerdePost = vm.Posten.Single();
        Assert.True(vm.Editor!.IsAlleenLezen);
        Assert.False(vm.VerwijderConceptCommand.CanExecute(null));
        Assert.False(vm.TegenboekenCommand.CanExecute(null)); // openingsbalans: via het boekjaar, niet via tegenboeken

        vm.NieuwePostCommand.Execute(null);
        vm.Editor!.Omschrijving = "Energie juni";
        VulRegel(vm.Editor, 0, 4115, "25,00", "");
        VulRegel(vm.Editor, 1, 1000, "", "25,00");
        await vm.BoekenCommand.ExecuteAsync(null);
        Assert.Equal("MEM-2026-0002", vm.GeselecteerdePost?.Nummer);

        _omgeving.Dialogen.BevestigAntwoord = true;
        await vm.TegenboekenCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Posten.Count);
        Assert.Contains(vm.Posten, p => p.Nummer == "MEM-2026-0003" && p.Omschrijving.StartsWith("Tegenboeking MEM-2026-0002", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rekening_toevoegen_en_dubbel_nummer_geeft_fout()
    {
        var vm = await _omgeving.OpenIngerichtAsync();

        vm.NieuweRekeningCommand.Execute(null);
        var nieuw = vm.Rekeningen.Single(r => r.IsNieuw);
        nieuw.Nummertekst = "4116";
        nieuw.Naam = "Water";
        nieuw.Soort = RekeningSoort.Resultaat;
        nieuw.GekozenRubriek = vm.Rubrieken.Single(r => r.Naam == "Terreinkosten");
        Assert.True(await vm.BewaarAsync());
        Assert.Contains(vm.Rekeningen, r => r.Nummertekst == "4116" && !r.IsNieuw);

        vm.NieuweRekeningCommand.Execute(null);
        var dubbel = vm.Rekeningen.Single(r => r.IsNieuw);
        dubbel.Nummertekst = "4116";
        dubbel.Naam = "Nog eens";
        Assert.False(await vm.BewaarAsync());
        Assert.Contains("bestaat al", vm.Foutmelding, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rapporten_tonen_afdrukken_en_exporteren()
    {
        var vm = await _omgeving.OpenIngerichtAsync();

        vm.Rapportsoort = Rapportsoort.Balans;
        Assert.Equal("Balans per 31-12-2026", vm.Rapport?.Titel);

        await vm.AfdrukkenCommand.ExecuteAsync(null);
        Assert.Single(_omgeving.Afdruk.Afgedrukt);

        _omgeving.Dialogen.XlsxDoel = "balans.xlsx";
        await vm.ExporteerExcelCommand.ExecuteAsync(null);
        Assert.Equal("balans.xlsx", Assert.Single(_omgeving.Schrijver.Geschreven).Pad);

        var csv = Path.Combine(Path.GetTempPath(), $"saldi-{Guid.NewGuid():N}.csv");
        try
        {
            vm.Rapportsoort = Rapportsoort.Saldibalans;
            _omgeving.Dialogen.CsvDoel = csv;
            await vm.ExporteerCsvCommand.ExecuteAsync(null);
            Assert.Contains("1100 Bank", await File.ReadAllTextAsync(csv), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(csv);
        }
    }

    [Fact]
    public async Task Grootboekkaart_per_rekening()
    {
        var vm = await _omgeving.OpenIngerichtAsync();

        vm.KaartRekening = vm.Rekeningkeuzes.Single(r => r.Nummer == 1000);

        Assert.Equal("€ 200,50", vm.KaartEindsaldo);
        Assert.Single(vm.KaartMutaties);
    }

    [Fact]
    public async Task Boekjaar_afsluiten_en_heropenen_na_bevestiging()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        var jaar = vm.Jaren.Single(j => j.Jaar == 2026);

        _omgeving.Dialogen.BevestigAntwoord = false;
        await vm.SluitBoekjaarCommand.ExecuteAsync(jaar);
        Assert.True(vm.Jaren.Single(j => j.Jaar == 2026).IsOpen);

        _omgeving.Dialogen.BevestigAntwoord = true;
        await vm.SluitBoekjaarCommand.ExecuteAsync(jaar);
        Assert.False(vm.Jaren.Single(j => j.Jaar == 2026).IsOpen);
        Assert.Contains(vm.Jaren, j => j.Jaar == 2027 && j.IsOpen);
        Assert.Equal(2, _omgeving.Sessie.Backups.Count); // import en afsluiting

        await vm.HeropenBoekjaarCommand.ExecuteAsync(vm.Jaren.Single(j => j.Jaar == 2026));
        Assert.True(vm.Jaren.Single(j => j.Jaar == 2026).IsOpen);
    }

    [Fact]
    public async Task Stichting_heeft_eigen_lege_boekhouding()
    {
        await _omgeving.OpenIngerichtAsync();
        var stichting = new ViewModels.AdministratieViewModel(new Administratie(AdministratieSoort.Stichting, "Stichting", Administratie.Standaardkleur(AdministratieSoort.Stichting)));

        var vm = _omgeving.Maak(stichting);
        await vm.VernieuwCommand.ExecuteAsync(null);

        Assert.False(vm.IsIngericht);
        Assert.Equal("Boekhouding · Stichting", vm.Titel);
    }
}
