using Verenigd.App.ViewModels.Boekhouding;
using Verenigd.Application.Facturen;
using Verenigd.Application.Tests.Bank;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Relaties;

namespace Verenigd.App.Tests.Boekhouding;

/// <summary>Subtabbladen Relaties en Facturen (stap 4c-2, besluiten 105–112). Het IBAN is een openbaar voorbeeldnummer.</summary>
public sealed class FacturenViewModelTests : IDisposable
{
    private const string Iban = "NL91ABNA0417164300";
    private readonly BoekhoudingTestomgeving _omgeving = new();

    public void Dispose() => _omgeving.Dispose();

    private FactuurDienst Dienst => new(_omgeving.Sessie, _omgeving.Tijd);

    private async Task<Guid> RelatieAsync(string naam = "Stichting De Kerk", string? iban = null) =>
        await Dienst.BewaarRelatieAsync(new RelatieInvoer(null, new RelatieGegevens(naam, null, "Kerkstraat 1", "1234 AB", "Dorp", null, null, iban, true, false, null), true));

    private static void VulFactuur(BoekhoudingViewModel vm, string aantal = "2", string prijs = "62,50")
    {
        var editor = vm.FactuurEditor!;
        editor.Relatie = editor.Relatiekeuzes.Single();
        var regel = editor.Regels.Single();
        regel.Omschrijving = "Verhuur clubhuis";
        regel.Aantal = aantal;
        regel.Prijs = prijs;
        regel.Rekening = editor.Rekeningkeuzes.Single(r => r.Nummer == 8020);
    }

    [Fact]
    public async Task Relatie_toevoegen_controleren_en_opslaan_met_ctrl_s()
    {
        var vm = await _omgeving.OpenIngerichtAsync();

        vm.NieuweRelatieCommand.Execute(null);
        Assert.Equal(BoekhoudingSubtab.Relaties, vm.Subtab);
        var regel = Assert.Single(vm.Relaties);
        regel.Naam = "Stichting De Kerk";
        regel.Iban = "NL91ABNA0417164301";
        Assert.NotNull(regel.Fout);
        Assert.False(vm.KanOpslaan);
        regel.Iban = "nl91 abna 0417 1643 00";
        Assert.Null(regel.Fout);
        Assert.True(vm.KanOpslaan);

        Assert.True(await vm.BewaarAsync());

        var opgeslagen = Assert.Single(vm.Relaties);
        Assert.False(opgeslagen.IsGewijzigd);
        Assert.Equal(Iban, (await Dienst.LaadAsync(AdministratieSoort.Vereniging)).Relaties.Single().Iban);
    }

    [Fact]
    public void Kolom_soort_van_een_relatie_werkt_direct_bij_na_aanvinken()
    {
        var regel = new RelatieRegel(null);
        var gemeld = new List<string?>();
        regel.PropertyChanged += (_, e) => gemeld.Add(e.PropertyName);

        regel.IsCrediteur = true;

        Assert.Equal("Debiteur en crediteur", regel.Rol);
        Assert.Contains(nameof(RelatieRegel.Rol), gemeld);
    }

    [Fact]
    public async Task Factuur_maken_definitief_maken_en_als_pdf_opslaan()
    {
        await RelatieAsync();
        var vm = await _omgeving.OpenIngerichtAsync();

        vm.NieuweFactuurCommand.Execute(null);
        Assert.Equal(BoekhoudingSubtab.Facturen, vm.Subtab);
        Assert.Equal("14", vm.FactuurEditor!.Termijn);
        VulFactuur(vm);
        Assert.Equal("€ 125,00", vm.FactuurEditor.Totaaltekst);
        Assert.True(vm.KanOpslaan);
        Assert.True(await vm.BewaarAsync());
        Assert.Equal(Factuurfilter.Concept, vm.Factuurfilter);
        Assert.Equal("Concept", Assert.Single(vm.Facturen).Status);

        await vm.MaakFactuurDefinitiefCommand.ExecuteAsync(null);

        Assert.Contains("V2026-001", vm.Melding, StringComparison.Ordinal);
        Assert.Equal(Factuurfilter.Open, vm.Factuurfilter);
        var factuur = Assert.Single(vm.Facturen);
        Assert.Equal("V2026-001", factuur.Nummer);
        Assert.Equal("Open", factuur.Status);
        Assert.Null(vm.FactuurEditor);
        vm.Factuurfilter = Factuurfilter.Vervallen;
        Assert.Empty(vm.Facturen);

        vm.Factuurfilter = Factuurfilter.Alle;
        vm.GeselecteerdeFactuur = vm.Facturen.Single();
        var doel = Path.Combine(Path.GetTempPath(), $"factuur-{Guid.NewGuid():N}.pdf");
        _omgeving.Dialogen.PdfDoel = doel;
        try
        {
            await vm.FactuurPdfCommand.ExecuteAsync(null);
            Assert.Equal(FakeFactuurPdfMaker.Inhoud, await File.ReadAllBytesAsync(doel));
            Assert.Equal("V2026-001", Assert.Single(_omgeving.Pdf.Gemaakt).Nummer);
            Assert.Equal("V2026-001.pdf", _omgeving.Dialogen.PdfVoorstel);
        }
        finally
        {
            File.Delete(doel);
        }
    }

    [Fact]
    public async Task Creditfactuur_wordt_een_concept_en_crediteert_na_definitief_maken()
    {
        await RelatieAsync();
        var vm = await _omgeving.OpenIngerichtAsync();
        vm.NieuweFactuurCommand.Execute(null);
        VulFactuur(vm);
        await vm.MaakFactuurDefinitiefCommand.ExecuteAsync(null);
        vm.GeselecteerdeFactuur = vm.Facturen.Single();

        await vm.CreditfactuurCommand.ExecuteAsync(null);

        Assert.Equal(Factuurfilter.Concept, vm.Factuurfilter);
        var credit = vm.GeselecteerdeFactuur;
        Assert.NotNull(credit);
        Assert.Equal("-€ 125,00", vm.FactuurEditor!.Totaaltekst);
        await vm.MaakFactuurDefinitiefCommand.ExecuteAsync(null);

        vm.Factuurfilter = Factuurfilter.Alle;
        Assert.Equal(["Creditfactuur", "Gecrediteerd"], vm.Facturen.Select(f => f.Status).Order(StringComparer.Ordinal));
        vm.Factuurfilter = Factuurfilter.Open;
        Assert.Empty(vm.Facturen);
    }

    [Fact]
    public async Task Concept_verwijderen_en_ctrl_n_blijft_nieuwe_post()
    {
        await RelatieAsync();
        var vm = await _omgeving.OpenIngerichtAsync();
        vm.NieuweFactuurCommand.Execute(null);
        VulFactuur(vm);
        await vm.BewaarAsync();
        vm.GeselecteerdeFactuur = vm.Facturen.Single();

        await vm.VerwijderFactuurconceptCommand.ExecuteAsync(null);
        Assert.Empty(vm.Facturen);

        vm.Subtab = BoekhoudingSubtab.Facturen;
        vm.NieuwePostCommand.Execute(null);
        Assert.Equal(BoekhoudingSubtab.Journaal, vm.Subtab);
        Assert.NotNull(vm.Editor);
    }

    [Fact]
    public async Task Ontvangst_afletteren_met_twee_open_facturen()
    {
        var vm = await _omgeving.OpenMetBankrekeningenAsync();
        var relatie = await RelatieAsync();
        var stand = await Dienst.LaadAsync(AdministratieSoort.Vereniging);
        var opbrengst = stand.Rekeningen.Single(r => r.Nummer == 8020).Id;
        foreach (var bedrag in new[] { 1000L, 1500L })
        {
            var id = await Dienst.BewaarConceptAsync(AdministratieSoort.Vereniging, new FactuurInvoer(null, relatie, new DateOnly(2026, 1, 10), 14,
                [new FactuurregelInvoerId("Verhuur", 1, new Bedrag(bedrag), opbrengst)]));
            await Dienst.MaakDefinitiefAsync(AdministratieSoort.Vereniging, id);
        }

        _omgeving.Dialogen.CamtBestanden =
        [
            _omgeving.CamtBestand(CamtVoorbeeld.Betaal, new VbAfschrift(new DateOnly(2026, 2, 2), 79950,
                new VbTransactie(2500, Ref: "W1", Naam: "Kerk", NtryRef: "3"))),
        ];
        _omgeving.Dialogen.BankImport = async wizard =>
        {
            await wizard.VolgendeCommand.ExecuteAsync(null);
            wizard.Rekeningen.Single().GekozenRekening = wizard.Balansrekeningen.Single(r => r.Nummer == 1100);
            await wizard.VolgendeCommand.ExecuteAsync(null);
            return wizard.IsKlaar;
        };
        await vm.BankInlezenCommand.ExecuteAsync(null);
        vm.GeselecteerdeMutatie = vm.Mutaties.Single();

        Assert.Equal(2, vm.Afletterkeuzes.Count);
        Assert.All(vm.Afletterkeuzes, k => Assert.StartsWith("V2026-00", k.Tekst, StringComparison.Ordinal));
        vm.Afletterkeuzes[0].IsGekozen = true;
        Assert.False(vm.AfletterenCommand.CanExecute(null));
        vm.Afletterkeuzes[1].IsGekozen = true;
        Assert.True(vm.AfletterenCommand.CanExecute(null));
        await vm.AfletterenCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.AantalTeVerwerken);
        vm.Factuurfilter = Factuurfilter.Alle;
        Assert.All(vm.Facturen, f => Assert.Equal("Betaald", f.Status));
    }
}
