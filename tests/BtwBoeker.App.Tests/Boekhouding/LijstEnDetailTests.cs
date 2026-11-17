using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Tests.Bank;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Bank;

namespace BtwBoeker.App.Tests.Boekhouding;

/// <summary>
/// UX-review stap 3 (besluit 38): één patroon voor lijst en detail. De lijst neemt de volle breedte; het detail opent pas bij
/// een selectie of <i>Nieuw</i> en sluit met ✕ of Esc. Opslaan blijft één opslag per pagina.
/// </summary>
public sealed class LijstEnDetailTests : IDisposable
{
    private readonly BoekhoudingTestomgeving _omgeving = new();

    public void Dispose() => _omgeving.Dispose();

    private async Task<BoekhoudingViewModel> OpenMetImportAsync()
    {
        var vm = await _omgeving.OpenMetBankrekeningenAsync();
        _omgeving.Dialogen.CamtBestanden =
        [
            _omgeving.CamtBestand(CamtVoorbeeld.Betaal, new VbAfschrift(new DateOnly(2026, 2, 2), 79950,
                new VbTransactie(-4500, "ICDT", "ESCT", "520", Ref: "E1", Naam: "Energie BV"),
                new VbTransactie(2500, Ref: "W1", Naam: "Huurder Jansen", NtryRef: "3"))),
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

    // ---- Het detail opent pas bij een keuze ------------------------------------------------------

    [Fact]
    public async Task Na_laden_is_er_geen_detail_open()
    {
        var vm = await OpenMetImportAsync();

        Assert.NotEmpty(vm.Mutaties);
        Assert.Null(vm.GeselecteerdeMutatie); // niet meer vanzelf de eerste regel
        Assert.Null(vm.Editor);
        Assert.Null(vm.GeselecteerdeBon);
        Assert.Null(vm.GeselecteerdeRegel);
        Assert.False(vm.IsDetailOpen);
    }

    [Theory]
    [InlineData(BoekhoudingSubtab.Journaal)]
    [InlineData(BoekhoudingSubtab.Bank)]
    [InlineData(BoekhoudingSubtab.Boekingsregels)]
    [InlineData(BoekhoudingSubtab.Rekeningschema)]
    public async Task Het_detail_opent_bij_een_keuze_en_sluit_met_esc(BoekhoudingSubtab onderdeel)
    {
        var vm = await OpenMetImportAsync();
        vm.Subtab = onderdeel;
        Assert.False(vm.IsDetailOpen);

        switch (onderdeel)
        {
            case BoekhoudingSubtab.Journaal:
                vm.NieuwePostCommand.Execute(null);
                break;
            case BoekhoudingSubtab.Bank:
                vm.GeselecteerdeMutatie = vm.Mutaties[0];
                break;
            case BoekhoudingSubtab.Boekingsregels:
                vm.NieuweRegelCommand.Execute(null);
                break;
            default:
                vm.GeselecteerdeRekening = vm.Rekeningen[0];
                break;
        }

        Assert.True(vm.IsDetailOpen);
        Assert.Equal(onderdeel, vm.Subtab);

        await vm.SluitDetailCommand.ExecuteAsync(null);

        Assert.False(vm.IsDetailOpen);
    }

    [Fact]
    public async Task Sluiten_met_een_gewijzigde_post_vraagt_eerst_om_te_verwerpen()
    {
        var vm = await OpenMetImportAsync();
        vm.NieuwePostCommand.Execute(null);
        vm.Editor!.Omschrijving = "Half af";
        _omgeving.Dialogen.BevestigAntwoord = false;

        await vm.SluitDetailCommand.ExecuteAsync(null);

        Assert.True(vm.IsDetailOpen);
        Assert.NotEmpty(_omgeving.Dialogen.Bevestigingsvragen);

        _omgeving.Dialogen.BevestigAntwoord = true;
        await vm.SluitDetailCommand.ExecuteAsync(null);
        Assert.False(vm.IsDetailOpen);
    }

    [Fact]
    public async Task Een_nieuwe_regel_die_gesloten_wordt_verdwijnt()
    {
        var vm = await OpenMetImportAsync();
        vm.NieuweRegelCommand.Execute(null);
        var aantal = vm.Boekingsregels.Count;

        await vm.SluitDetailCommand.ExecuteAsync(null);

        Assert.Equal(aantal - 1, vm.Boekingsregels.Count);
        Assert.False(vm.HeeftOnopgeslagenWijzigingen);
    }

    // ---- Opslaan ---------------------------------------------------------------------------------

    [Fact]
    public async Task Opslaan_staat_in_het_detail_en_alleen_zonder_detail_onderaan()
    {
        var vm = await OpenMetImportAsync();
        vm.Subtab = BoekhoudingSubtab.Rekeningschema;
        vm.GeselecteerdeRekening = vm.Rekeningen.Single(r => r.Nummertekst == "4115");
        vm.GeselecteerdeRekening.Naam = "Energie en water";

        Assert.True(vm.HeeftOnopgeslagenWijzigingen);
        Assert.True(vm.HeeftOnopgeslagenWijzigingenMetDetail);
        Assert.False(vm.HeeftOnopgeslagenWijzigingenZonderDetail);

        vm.GeselecteerdeRekening = null; // de wijziging blijft staan tot Opslaan (één opslag per pagina)

        Assert.True(vm.HeeftOnopgeslagenWijzigingen);
        Assert.False(vm.HeeftOnopgeslagenWijzigingenMetDetail);
        Assert.True(vm.HeeftOnopgeslagenWijzigingenZonderDetail);
    }

    // ---- Jaarkeuze -------------------------------------------------------------------------------

    [Fact]
    public async Task De_jaarkeuze_staat_er_alleen_bij_meer_jaren_en_waar_het_jaar_iets_doet()
    {
        var vm = await OpenMetImportAsync();
        Assert.False(vm.IsJaarkeuzeZichtbaar);

        await new BoekhoudingDienst(_omgeving.Sessie, _omgeving.Tijd).OpenBoekjaarAsync(AdministratieSoort.Standaard, 2027);
        await vm.VernieuwCommand.ExecuteAsync(null);

        foreach (var onderdeel in Enum.GetValues<BoekhoudingSubtab>())
        {
            vm.Subtab = onderdeel;
            var verwacht = onderdeel is BoekhoudingSubtab.Journaal or BoekhoudingSubtab.Bank or BoekhoudingSubtab.Rapporten or BoekhoudingSubtab.Grootboekkaart;
            Assert.True(verwacht == vm.IsJaarkeuzeZichtbaar, onderdeel.ToString());
        }
    }

    // ---- Bank ------------------------------------------------------------------------------------

    [Fact]
    public async Task Het_saldo_is_een_getal_en_een_verschil_krijgt_een_uitleg()
    {
        var vm = await OpenMetImportAsync();

        Assert.StartsWith("Saldo ", vm.Banksaldotekst, StringComparison.Ordinal);
        Assert.DoesNotContain("grootboek", vm.Banksaldotekst, StringComparison.Ordinal);
        if (vm.IsBankAangesloten)
        {
            Assert.Null(vm.Bankverschiltekst);
        }
        else
        {
            Assert.StartsWith("⚠", vm.Bankverschiltekst, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Een_opgeslagen_boekingsregel_wordt_vanzelf_toegepast()
    {
        var vm = await OpenMetImportAsync();
        Assert.Equal(2, vm.AantalTeVerwerken);

        vm.NieuweRegelCommand.Execute(null);
        var regel = vm.GeselecteerdeRegel!;
        regel.Naam = "Energie";
        regel.NaamBevat = "energie";
        regel.GekozenRekening = regel.Rekeningen.Single(r => r.Nummer == 4115);
        Assert.True(await vm.BewaarAsync());

        Assert.Equal(1, vm.AantalTeVerwerken); // zonder op Regels toepassen te klikken
    }

    // ---- Boekingsregels --------------------------------------------------------------------------

    [Fact]
    public async Task Een_boekingsregel_toont_in_de_lijst_wanneer_hij_geldt_en_waarheen()
    {
        var vm = await OpenMetImportAsync();
        vm.NieuweRegelCommand.Execute(null);
        var regel = vm.GeselecteerdeRegel!;
        regel.Naam = "Energie";
        regel.NaamBevat = "energ";
        regel.Richting = Richting.Af;
        regel.GekozenRekening = regel.Rekeningen.Single(r => r.Nummer == 4115);

        Assert.Equal("naam bevat \"energ\", af", regel.Wanneer);
        Assert.Contains("4115", regel.Waarheen, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Alleen_de_ingevulde_voorwaarden_staan_er_en_een_andere_kan_erbij()
    {
        var vm = await OpenMetImportAsync();
        vm.NieuweRegelCommand.Execute(null);
        var regel = vm.GeselecteerdeRegel!;
        regel.NaamBevat = "energ";

        Assert.True(regel.IsNaamBevatZichtbaar);
        Assert.False(regel.IsTegenIbanZichtbaar);
        Assert.False(regel.IsBedragZichtbaar);
        Assert.Contains(Voorwaarde.TegenIban, regel.ToeTeVoegenVoorwaarden);
        Assert.DoesNotContain(Voorwaarde.NaamBevat, regel.ToeTeVoegenVoorwaarden);

        regel.VoorwaardeToevoegenCommand.Execute(Voorwaarde.Bedrag);

        Assert.True(regel.IsBedragZichtbaar);
        Assert.DoesNotContain(Voorwaarde.Bedrag, regel.ToeTeVoegenVoorwaarden);
    }

    // ---- Relaties --------------------------------------------------------------------------------

    [Fact]
    public void Een_relatie_is_klant_of_leverancier()
    {
        var relatie = new RelatieRegel(null) { IsDebiteur = false, IsCrediteur = true };
        Assert.Equal("Leverancier", relatie.Rol);
        relatie.IsDebiteur = true;
        Assert.Equal("Klant en leverancier", relatie.Rol);
        relatie.IsCrediteur = false;
        Assert.Equal("Klant", relatie.Rol);
    }
}
