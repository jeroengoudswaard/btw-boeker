using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;
using BtwBoeker.Domain.Financien.Btw;

namespace BtwBoeker.App.Tests.Boekhouding;

/// <summary>
/// Besluit 50 (fase B2): btw invoeren in de posteditor. Per regel een tarief, incl of excl; de app maakt de btw-regel en toont
/// die als onderregel. De soort volgt uit de kant van de rekening; het btw-bedrag is aan te passen, met een ⚠.
/// </summary>
public sealed class BtwInvoerTests : IDisposable
{
    private readonly BoekhoudingTestomgeving _omgeving = new();

    public void Dispose() => _omgeving.Dispose();

    /// <summary>Een ingerichte boekhouding met de rekeningen 1520 Voorbelasting en 1530 Te betalen btw.</summary>
    private async Task<BoekhoudingViewModel> OpenMetBtwrekeningenAsync(bool metVoorbelasting = true)
    {
        var dienst = new BoekhoudingDienst(_omgeving.Sessie, _omgeving.Tijd);
        await dienst.ImporteerAsync(AdministratieSoort.Standaard, Application.Tests.Boekhouding.Testvoorstel.Standaard());
        if (metVoorbelasting)
        {
            await dienst.BewaarRekeningAsync(AdministratieSoort.Standaard, new RekeningInvoer(null, 1520, "Voorbelasting", null, RekeningSoort.Balans, RekeningRol.Voorbelasting, true));
        }

        await dienst.BewaarRekeningAsync(AdministratieSoort.Standaard, new RekeningInvoer(null, 1530, "Te betalen btw", null, RekeningSoort.Balans, RekeningRol.TeBetalenBtw, true));
        var vm = _omgeving.Maak();
        await vm.VernieuwCommand.ExecuteAsync(null);
        return vm;
    }

    private static PostRegelEditor Regel(PostEditor editor, int index, int rekening, string debet, string credit, string? btw = null)
    {
        while (editor.Regels.Count <= index)
        {
            editor.NieuweRegelCommand.Execute(null);
        }

        var regel = editor.Regels[index];
        regel.GekozenRekening = editor.Rekeningen.Single(r => r.Nummer == rekening);
        regel.Debettekst = debet;
        regel.Credittekst = credit;
        if (btw is not null)
        {
            regel.GekozenBtw = regel.Btwkeuzes.Single(k => k.Tekst == btw);
        }

        return regel;
    }

    private static async Task<PostEditor> NieuwAsync(BoekhoudingViewModel vm)
    {
        vm.NieuwePostCommand.Execute(null);
        vm.Editor!.Omschrijving = "Energie november";
        await Task.CompletedTask;
        return vm.Editor;
    }

    [Fact]
    public async Task De_keuzes_volgen_de_kant_van_de_rekening()
    {
        var editor = await NieuwAsync(await OpenMetBtwrekeningenAsync());

        var kosten = Regel(editor, 0, 4115, "10,00", string.Empty);
        var omzet = Regel(editor, 1, 8020, string.Empty, "10,00");

        Assert.Equal(["Geen btw", "21 % incl.", "21 % excl.", "9 % incl.", "9 % excl.", "0 %"], kosten.Btwkeuzes.Select(k => k.Tekst));
        Assert.Contains(omzet.Btwkeuzes, k => k.Tekst == "Privégebruik 21 %");
        Assert.Equal("Geen btw", kosten.GekozenBtw.Tekst); // bij kosten stelt de app geen tarief voor (besluit 12)
        Assert.Equal(BtwSoort.Voorbelasting, kosten.Btwkeuzes.Single(k => k.Tekst == "21 % incl.").Soort);
        Assert.Equal(BtwSoort.Omzet, omzet.Btwkeuzes.Single(k => k.Tekst == "21 % incl.").Soort);
    }

    [Fact]
    public async Task Inclusief_21_procent_splitst_in_grondslag_en_btw()
    {
        var editor = await NieuwAsync(await OpenMetBtwrekeningenAsync());

        var regel = Regel(editor, 0, 4115, "10,47", string.Empty, "21 % incl.");
        Regel(editor, 1, 1100, string.Empty, "10,47");

        Assert.Equal(new Bedrag(865), regel.Grondslag);
        Assert.Equal(new Bedrag(182), regel.BtwBedrag);
        Assert.Equal("grondslag € 8,65 · btw € 1,82 → 1520 Voorbelasting", regel.Onderregel);
        Assert.True(editor.IsInEvenwicht);
        Assert.True(editor.KanBoeken);
    }

    [Fact]
    public async Task Exclusief_telt_de_btw_bij_het_bedrag()
    {
        var editor = await NieuwAsync(await OpenMetBtwrekeningenAsync());

        var regel = Regel(editor, 0, 4115, "8,65", string.Empty, "21 % excl.");
        Regel(editor, 1, 1100, string.Empty, "10,47");

        Assert.Equal(new Bedrag(865), regel.Grondslag);
        Assert.Equal(new Bedrag(182), regel.BtwBedrag);
        Assert.Equal(new Bedrag(1047), regel.Debet);
        Assert.Equal("€ 10,47", editor.TotaalDebettekst);
        Assert.True(editor.IsInEvenwicht);
    }

    [Fact]
    public async Task Opslaan_zet_de_btw_regel_direct_na_de_grondslag_en_herladen_voegt_ze_weer_samen()
    {
        var vm = await OpenMetBtwrekeningenAsync();
        var editor = await NieuwAsync(vm);
        Regel(editor, 0, 4115, "10,47", string.Empty, "21 % incl.");
        Regel(editor, 1, 1100, string.Empty, "10,47");

        Assert.True(await vm.BewaarAsync());

        var post = (await new BoekhoudingDienst(_omgeving.Sessie, _omgeving.Tijd).LaadAsync(AdministratieSoort.Standaard))
            .Posten.Single(p => p.Omschrijving == "Energie november");
        Assert.Equal([4115, 1520, 1100], post.Regels.Select(r => r.Rekening.Nummer));
        Assert.Equal([865L, 182L, -1047L], post.Regels.Select(r => r.Saldo));
        Assert.Equal(new BtwCode(BtwSoort.Voorbelasting, 21), post.Regels[0].Btw);
        Assert.Equal(new BtwCode(BtwSoort.Voorbelasting, 21), post.Regels[1].Btw);

        vm.GeselecteerdePost = vm.Posten.Single(p => p.Id == post.Id);
        var herladen = vm.Editor!;
        Assert.Equal(2, herladen.Regels.Count);
        Assert.Equal("21 % incl.", herladen.Regels[0].GekozenBtw.Tekst);
        Assert.Equal(new Bedrag(182), herladen.Regels[0].BtwBedrag);
        Assert.Null(herladen.Regels[0].Btwwaarschuwing);
        Assert.False(herladen.IsGewijzigd);
    }

    [Fact]
    public async Task Een_aangepast_btw_bedrag_geeft_een_waarschuwing_maar_boeken_mag()
    {
        var editor = await NieuwAsync(await OpenMetBtwrekeningenAsync());
        var regel = Regel(editor, 0, 4115, "10,47", string.Empty, "21 % incl.");
        Regel(editor, 1, 1100, string.Empty, "10,47");

        regel.BtwAanpassenCommand.Execute(null);
        Assert.True(regel.IsBtwAanpassen);
        Assert.Equal("1,82", regel.Btwtekst); // begint bij het berekende bedrag

        regel.Btwtekst = "1,83";

        Assert.Equal(new Bedrag(183), regel.BtwBedrag);
        Assert.Equal(new Bedrag(864), regel.Grondslag);
        Assert.Contains("wijkt € 0,01 af", regel.Btwwaarschuwing, StringComparison.Ordinal);
        Assert.True(editor.KanBoeken);
    }

    [Fact]
    public async Task Omzet_gaat_naar_te_betalen_btw()
    {
        var vm = await OpenMetBtwrekeningenAsync();
        var editor = await NieuwAsync(vm);
        Regel(editor, 0, 1100, "109,00", string.Empty);
        var omzet = Regel(editor, 1, 8020, string.Empty, "109,00", "9 % incl.");

        Assert.Equal("grondslag € 100,00 · btw € 9,00 → 1530 Te betalen btw", omzet.Onderregel);
        Assert.True(await vm.BewaarAsync());

        var post = (await new BoekhoudingDienst(_omgeving.Sessie, _omgeving.Tijd).LaadAsync(AdministratieSoort.Standaard))
            .Posten.Single(p => p.Omschrijving == "Energie november");
        Assert.Equal([1100, 8020, 1530], post.Regels.Select(r => r.Rekening.Nummer));
        Assert.Equal(-900, post.Regels[2].Saldo);
        Assert.Equal(new BtwCode(BtwSoort.Omzet, 9), post.Regels[2].Btw);
    }

    /// <summary>Besluit 50: een boekingsregel voor de bank krijgt een tarief; zonder btw-rekening zegt de regel wat eerst moet.</summary>
    [Fact]
    public async Task Een_boekingsregel_krijgt_een_tarief()
    {
        var vm = await OpenMetBtwrekeningenAsync(metVoorbelasting: false);
        vm.NieuweRegelCommand.Execute(null);
        var regel = vm.GeselecteerdeRegel!;
        regel.NaamBevat = "energie";
        regel.GekozenRekening = vm.Toewijsrekeningen.Single(r => r.Nummer == 4115);

        regel.GekozenBtwtarief = BoekingsregelRegel.Btwtarieven.Single(t => t.Tekst == "21 %");
        Assert.Contains("Voorbelasting", regel.Fout, StringComparison.Ordinal);

        regel.GekozenRekening = vm.Toewijsrekeningen.Single(r => r.Nummer == 8020); // omzet: te betalen btw is er wel
        Assert.Null(regel.Fout);
        Assert.Equal("→ 8020 Verhuur huisje · 21 % btw", regel.Waarheen);
        Assert.True(await vm.BewaarAsync());

        var bewaard = (await new BoekhoudingDienst(_omgeving.Sessie, _omgeving.Tijd).LaadAsync(AdministratieSoort.Standaard)).Boekingsregels.Single();
        Assert.Equal(21, bewaard.BtwPercentage);
    }

    [Fact]
    public async Task Zonder_btw_rekening_meldt_de_editor_wat_eerst_moet()
    {
        var vm = await OpenMetBtwrekeningenAsync(metVoorbelasting: false);
        var editor = await NieuwAsync(vm);

        Regel(editor, 0, 4115, "10,47", string.Empty, "21 % incl.");
        Regel(editor, 1, 1100, string.Empty, "10,47");

        Assert.Equal("Voorbelasting", editor.OntbrekendeBtwrekening);
        Assert.Contains(editor.Fouten, f => f.Contains("Voorbelasting", StringComparison.Ordinal));
        Assert.False(editor.KanBoeken);

        vm.NaarRekeningschemaCommand.Execute(null);
        Assert.Equal(BoekhoudingSubtab.Rekeningschema, vm.Subtab);
    }
}
