using Verenigd.App.ViewModels;
using Verenigd.App.ViewModels.Boekhouding;
using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.App.Tests.Boekhouding;

/// <summary>Subtabblad Meerjarenplan van de Stichting (besluiten 159–167). Vandaag is 15-06-2026.</summary>
public sealed class MeerjarenplanViewModelTests : IDisposable
{
    private const AdministratieSoort Sti = AdministratieSoort.Stichting;
    private readonly BoekhoudingTestomgeving _omgeving = new();

    private static readonly AdministratieViewModel Stichting = new(
        new Administratie(Sti, "Stichting", Administratie.Standaardkleur(Sti)));

    public void Dispose() => _omgeving.Dispose();

    private async Task<BoekhoudingViewModel> OpenAsync()
    {
        var dienst = new BoekhoudingDienst(_omgeving.Sessie, _omgeving.Tijd);
        await dienst.RichtInAsync(Sti);
        await dienst.BewaarRekeningAsync(Sti, new RekeningInvoer(null, 8100, "Huur van de Vereniging", null, RekeningSoort.Resultaat, RekeningRol.Geen, true));
        await dienst.BewaarRekeningAsync(Sti, new RekeningInvoer(null, 4200, "Onderhoud gebouw", null, RekeningSoort.Resultaat, RekeningRol.Geen, true));
        var vm = _omgeving.Maak(Stichting);
        await vm.VernieuwCommand.ExecuteAsync(null);
        vm.Subtab = BoekhoudingSubtab.Meerjarenplan;
        return vm;
    }

    [Fact]
    public async Task Alleen_de_stichting_heeft_een_meerjarenplan()
    {
        var vereniging = await _omgeving.OpenIngerichtAsync();
        Assert.False(vereniging.HeeftMeerjarenplanTab);

        var vm = await OpenAsync();
        Assert.True(vm.HeeftMeerjarenplanTab);
        Assert.Equal(2027, vm.MeerjarenStartjaar); // standaard het volgende jaar
    }

    [Fact]
    public async Task Plan_maken_toont_een_raster_met_tien_jaren()
    {
        var vm = await OpenAsync();

        await vm.MeerjarenplanMakenCommand.ExecuteAsync(null);

        Assert.True(vm.IsMeerjarenplanBewerkbaar);
        Assert.Equal(["2027", "2028", "2029", "2030", "2031", "2032", "2033", "2034", "2035", "2036"], vm.Meerjarenkoppen);
        var huur = vm.Meerjarenrijen.Single(r => r.Rekeningtekst == "8100 Huur van de Vereniging");
        Assert.Equal("Baten", huur.Kanttekst);
        Assert.Equal(10, huur.Cellen.Count);
        Assert.Equal("Lasten", vm.Meerjarenrijen.Single(r => r.Rekeningtekst == "4200 Onderhoud gebouw").Kanttekst);
    }

    [Fact]
    public async Task Bedragen_invullen_kopieren_en_opslaan_met_ctrl_s()
    {
        var vm = await OpenAsync();
        await vm.MeerjarenplanMakenCommand.ExecuteAsync(null);
        var huur = vm.Meerjarenrijen.Single(r => r.Rekeningtekst == "8100 Huur van de Vereniging");
        huur.Cellen[1].Tekst = "6.000,00";
        vm.GeselecteerdeMeerjarenrij = huur;
        vm.KopieerVanafJaar = 2028;

        vm.NaarVolgendeJarenKopierenCommand.Execute(null);

        Assert.Equal(string.Empty, huur.Cellen[0].Tekst);
        Assert.All(huur.Cellen.Skip(2), c => Assert.Equal("6000,00", c.Tekst));
        vm.Meerjarenrijen.Single(r => r.Rekeningtekst == "4200 Onderhoud gebouw").Cellen[3].Tekst = "3000";
        Assert.Contains("2030: resultaat € 3.000,00", vm.Meerjarentotalen, StringComparison.Ordinal);
        Assert.True(await vm.BewaarAsync());

        var plan = Assert.Single((await _omgeving.Opslag.LaadAsync(Sti)).Meerjarenplannen);
        var rekening = plan.Posten.Single(p => p.Rekening.Nummer == 8100).Rekening;
        Assert.Equal(new Bedrag(600000), plan.Bedrag(rekening, 2036));
        Assert.Equal(Bedrag.Nul, plan.Bedrag(rekening, 2027));
    }

    [Fact]
    public async Task Ongeldig_bedrag_blokkeert_opslaan()
    {
        var vm = await OpenAsync();
        await vm.MeerjarenplanMakenCommand.ExecuteAsync(null);

        vm.Meerjarenrijen[0].Cellen[0].Tekst = "veel";

        Assert.NotNull(vm.Meerjarenrijen[0].Fout);
        Assert.False(vm.KanOpslaan);
    }

    [Fact]
    public async Task Vaststellen_herzien_en_liggend_afdrukken()
    {
        var vm = await OpenAsync();
        await vm.MeerjarenplanMakenCommand.ExecuteAsync(null);
        vm.Meerjarenrijen[0].Cellen[0].Tekst = "100";
        await vm.BewaarAsync();

        Assert.True(vm.Afdruktabel?.IsLiggend);
        Assert.Equal("Meerjarenplan 2027–2036", vm.Afdruktabel?.Titel);

        vm.MeerjarenVaststellenOp = new DateTime(2026, 11, 20);
        await vm.MeerjarenplanVaststellenCommand.ExecuteAsync(null);
        Assert.False(vm.IsMeerjarenplanBewerkbaar);

        await vm.MeerjarenplanHerzienCommand.ExecuteAsync(null);
        Assert.True(vm.IsMeerjarenplanBewerkbaar);
        Assert.Equal("Versie 2 · Herziening 1 · Concept", vm.GekozenMeerjarenversie?.Tekst);
    }
}
