using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Tests.Bank;
using BtwBoeker.Domain.Financien.Bank;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.Tests.Boekhouding;

/// <summary>
/// UX-review stap 4 (besluit 40): alleen de acties die nu kunnen; kan iets nog niet, dan zegt de app waarom en wat eerst
/// moet, met één knop erheen.
/// </summary>
public sealed class ActiesVolgensStatusTests : IDisposable
{
    private readonly BoekhoudingTestomgeving _omgeving = new();

    public void Dispose() => _omgeving.Dispose();

    private static JaarRegel Jaar(BoekhoudingViewModel vm, int jaar) => vm.Jaren.Single(j => j.Jaar == jaar);

    private static void VulRegel(PostEditor editor, int index, int rekening, string debet, string credit)
    {
        while (editor.Regels.Count <= index)
        {
            editor.NieuweRegelCommand.Execute(null);
        }

        editor.Regels[index].GekozenRekening = editor.Rekeningen.Single(r => r.Nummer == rekening);
        editor.Regels[index].Debettekst = debet;
        editor.Regels[index].Credittekst = credit;
    }

    private static async Task MaakConceptAsync(BoekhoudingViewModel vm)
    {
        vm.NieuwePostCommand.Execute(null);
        vm.Editor!.Omschrijving = "Energie juni";
        VulRegel(vm.Editor, 0, 4115, "30,00", "");
        VulRegel(vm.Editor, 1, 1100, "", "30,00");
        Assert.True(await vm.BewaarAsync());
    }

    // ---- Boekjaren: lijst en detail ---------------------------------------------------------------

    [Fact]
    public async Task Boekjaren_is_een_lijst_met_detail()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        vm.Subtab = BoekhoudingSubtab.Boekjaren;
        Assert.False(vm.IsDetailOpen);

        vm.GeselecteerdJaar = Jaar(vm, 2026);
        Assert.True(vm.IsDetailOpen);

        await vm.SluitDetailCommand.ExecuteAsync(null);
        Assert.Null(vm.GeselecteerdJaar);
    }

    [Fact]
    public async Task Het_oudste_open_jaar_zonder_concepten_kan_worden_afgesloten()
    {
        var vm = await _omgeving.OpenIngerichtAsync();

        var jaar = Jaar(vm, 2026);

        Assert.True(jaar.KanAfsluiten);
        Assert.False(jaar.KanHeropenen);
        Assert.Equal(Vervolgactie.Geen, jaar.Vervolg);
    }

    [Fact]
    public async Task Concepten_moeten_eerst_worden_geboekt_met_een_knop_naar_het_journaal()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        await MaakConceptAsync(vm);
        vm.Subtab = BoekhoudingSubtab.Boekjaren;
        var jaar = Jaar(vm, 2026);

        Assert.False(jaar.KanAfsluiten);
        Assert.Contains("1 conceptpost", jaar.Toelichting, StringComparison.Ordinal);
        Assert.Equal(Vervolgactie.NaarJournaal, jaar.Vervolg);
        Assert.Equal("Naar het journaal", jaar.Vervolgtekst);

        vm.VervolgCommand.Execute(jaar);

        Assert.Equal(BoekhoudingSubtab.Journaal, vm.Subtab);
        Assert.Equal(2026, vm.Jaar);
    }

    [Fact]
    public async Task Een_later_jaar_wacht_op_het_eerdere_met_een_knop_naar_dat_jaar()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        await new BoekhoudingDienst(_omgeving.Sessie, _omgeving.Tijd).OpenBoekjaarAsync(Domain.Administraties.AdministratieSoort.Standaard, 2027);
        await vm.VernieuwCommand.ExecuteAsync(null);
        vm.Subtab = BoekhoudingSubtab.Boekjaren;
        var later = Jaar(vm, 2027);

        Assert.False(later.KanAfsluiten);
        Assert.Contains("2026", later.Toelichting, StringComparison.Ordinal);
        Assert.Equal(Vervolgactie.NaarEerderJaar, later.Vervolg);
        Assert.Equal("Naar 2026", later.Vervolgtekst);

        vm.VervolgCommand.Execute(later);

        Assert.Equal(2026, vm.GeselecteerdJaar?.Jaar);
    }

    [Fact]
    public async Task Zonder_reserverekening_eerst_naar_het_rekeningschema()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        vm.Rekeningen.Single(r => r.Rol == RekeningRol.Reserve).Rol = RekeningRol.Geen;
        Assert.True(await vm.BewaarAsync());
        var jaar = Jaar(vm, 2026);

        Assert.False(jaar.KanAfsluiten);
        Assert.Equal(Vervolgactie.NaarRekeningschema, jaar.Vervolg);

        vm.VervolgCommand.Execute(jaar);

        Assert.Equal(BoekhoudingSubtab.Rekeningschema, vm.Subtab);
    }

    [Fact]
    public async Task Alleen_het_nieuwste_afgesloten_jaar_kan_worden_heropend()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        await vm.SluitBoekjaarCommand.ExecuteAsync(Jaar(vm, 2026));
        Assert.True(Jaar(vm, 2026).KanHeropenen);

        await vm.SluitBoekjaarCommand.ExecuteAsync(Jaar(vm, 2027));

        Assert.False(Jaar(vm, 2026).KanHeropenen);
        Assert.Contains("2027", Jaar(vm, 2026).Toelichting, StringComparison.Ordinal);
        Assert.True(Jaar(vm, 2027).KanHeropenen);
        Assert.False(Jaar(vm, 2027).KanAfsluiten);
    }

    [Fact]
    public async Task Volgend_boekjaar_openen_pas_in_december()
    {
        var vm = await _omgeving.OpenIngerichtAsync(); // 15 juni 2026
        Assert.False(vm.IsVolgendBoekjaarZichtbaar);

        using var december = new BoekhoudingTestomgeving(new DateTimeOffset(2026, 12, 5, 10, 0, 0, TimeSpan.Zero));
        var inDecember = await december.OpenIngerichtAsync();

        Assert.True(inDecember.IsVolgendBoekjaarZichtbaar);
        Assert.Equal("Boekjaar 2027 openen", inDecember.VolgendBoekjaarTekst);
    }

    // ---- Bank, Rapporten, Grootboekkaart ----------------------------------------------------------

    private async Task<BoekhoudingViewModel> OpenMetImportAsync()
    {
        var vm = await _omgeving.OpenMetBankrekeningenAsync();
        _omgeving.Dialogen.CamtBestanden =
        [
            _omgeving.CamtBestand(CamtVoorbeeld.Betaal, new VbAfschrift(new DateOnly(2026, 2, 2), 79950,
                new VbTransactie(-4500, "ICDT", "ESCT", "520", Ref: "E1", Naam: "Energie BV"))),
        ];
        _omgeving.Dialogen.BankImport = async wizard =>
        {
            await wizard.VolgendeCommand.ExecuteAsync(null);
            wizard.Rekeningen.Single(r => r.Iban == CamtVoorbeeld.Betaal).GekozenRekening = wizard.Balansrekeningen.Single(r => r.Nummer == 1100);
            await wizard.VolgendeCommand.ExecuteAsync(null);
            return wizard.IsKlaar;
        };
        await vm.BankInlezenCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Toewijzen_verschijnt_pas_met_een_gekozen_rekening()
    {
        var vm = await OpenMetImportAsync();
        vm.GeselecteerdeMutatie = vm.Mutaties.Single();
        vm.ToewijsRekening = null;

        Assert.False(vm.IsToewijzenZichtbaar);

        vm.ToewijsRekening = vm.Toewijsrekeningen.Single(r => r.Nummer == 4115);

        Assert.True(vm.IsToewijzenZichtbaar);
    }

    [Fact]
    public async Task Een_verwerkte_transactie_gaat_met_een_knop_naar_de_post()
    {
        var vm = await OpenMetImportAsync();
        vm.GeselecteerdeMutatie = vm.Mutaties.Single();
        Assert.False(vm.IsNaarPostZichtbaar);
        vm.ToewijsRekening = vm.Toewijsrekeningen.Single(r => r.Nummer == 4115);
        await vm.ToewijzenCommand.ExecuteAsync(null);
        vm.Bankfilter = MutatieFilter.Alle;
        vm.GeselecteerdeMutatie = vm.Mutaties.Single();
        Assert.True(vm.IsNaarPostZichtbaar);
        var post = vm.GeselecteerdeMutatie.Mutatie.PostId;

        vm.NaarPostCommand.Execute(null);

        Assert.Equal(BoekhoudingSubtab.Journaal, vm.Subtab);
        Assert.Equal(post, vm.Editor?.Id);
    }

    [Fact]
    public async Task Bij_concepten_gaat_een_knop_van_de_rapporten_naar_het_journaal()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        vm.Subtab = BoekhoudingSubtab.Rapporten;

        vm.NaarJournaalCommand.Execute(null);

        Assert.Equal(BoekhoudingSubtab.Journaal, vm.Subtab);
    }

    [Fact]
    public async Task De_grootboekkaart_toont_zonder_rekening_alleen_de_keuze()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        vm.Subtab = BoekhoudingSubtab.Grootboekkaart;
        Assert.False(vm.IsKaartRekeningGekozen);

        vm.KaartRekening = vm.Rekeningkeuzes[0];

        Assert.True(vm.IsKaartRekeningGekozen);
    }
}
