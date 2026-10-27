using Verenigd.App.ViewModels.Boekhouding;
using Verenigd.Application.Bank;
using Verenigd.Application.Tests.Bank;
using Verenigd.Domain.Financien.Bank;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.App.Tests.Boekhouding;

public sealed class BankViewModelTests : IDisposable
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
                new VbTransactie(-10000, "ICDT", "ESCT", "544", Ref: "S1", TegenIban: CamtVoorbeeld.Spaar, Naam: "Eigen spaar", NtryRef: "2"),
                new VbTransactie(2500, Ref: "W1", Naam: "Welpen ouder", NtryRef: "3"))),
            _omgeving.CamtBestand(CamtVoorbeeld.Spaar, new VbAfschrift(new DateOnly(2026, 2, 2), 0,
                new VbTransactie(10000, Ref: "S2", TegenIban: CamtVoorbeeld.Betaal, Naam: "Eigen betaal"))),
        ];
        _omgeving.Dialogen.BankImport = async wizard =>
        {
            await wizard.VolgendeCommand.ExecuteAsync(null);
            wizard.Rekeningen.Single(r => r.Iban == CamtVoorbeeld.Betaal).GekozenRekening = wizard.Balansrekeningen.Single(r => r.Nummer == 1100);
            wizard.Rekeningen.Single(r => r.Iban == CamtVoorbeeld.Spaar).GekozenRekening = wizard.Balansrekeningen.Single(r => r.Nummer == 1150);
            await wizard.VolgendeCommand.ExecuteAsync(null);
            return wizard.IsKlaar;
        };
        await vm.BankInlezenCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Inlezen_toont_werkvoorraad_en_kruisposten_als_concept()
    {
        var vm = await OpenMetImportAsync();

        Assert.Equal(BoekhoudingSubtab.Bank, vm.Subtab);
        Assert.Equal(2, vm.Bankrekeningen.Count);
        Assert.Equal("RABO", vm.GekozenBankrekening?.Code);
        Assert.Equal(MutatieFilter.TeVerwerken, vm.Bankfilter);
        Assert.Equal(2, vm.AantalTeVerwerken); // energie en welpen; de overboeking naar spaar is herkend
        Assert.Equal(2, vm.Mutaties.Count);
        Assert.Equal(1, vm.AantalConcept);

        vm.Bankfilter = MutatieFilter.Alle;
        Assert.Equal(3, vm.Mutaties.Count);
        Assert.Contains(vm.Mutaties, m => m.Bedragtekst == "-€ 45,00" && m.Tegenpartij == "Energie BV" && m.Status == "Te verwerken");
    }

    [Fact]
    public async Task Toewijzen_en_concepten_boeken_met_ctrl_b()
    {
        var vm = await OpenMetImportAsync();
        vm.GeselecteerdeMutatie = vm.Mutaties.Single(m => m.Tegenpartij == "Energie BV");
        Assert.False(vm.ToewijzenCommand.CanExecute(null));

        vm.ToewijsRekening = vm.Toewijsrekeningen.Single(r => r.Nummer == 4115);
        vm.ToewijsOmschrijving = "Energie februari";
        await vm.ToewijzenCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.AantalTeVerwerken);
        Assert.Equal(2, vm.AantalConcept);
        Assert.Equal("Welpen ouder", vm.GeselecteerdeMutatie?.Tegenpartij); // door naar de volgende

        _omgeving.Dialogen.BevestigAntwoord = true;
        await vm.ConceptenBoekenCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.AantalConcept);
        Assert.Equal("3 posten geboekt.", vm.Melding); // ook de overboeking aan de kant van de spaarrekening
        Assert.Contains(vm.Posten, p => p.Nummer == "RABO-2026-0001");
    }

    [Fact]
    public async Task Regel_maken_van_een_boeking_en_toepassen()
    {
        var vm = await OpenMetImportAsync();
        vm.GeselecteerdeMutatie = vm.Mutaties.Single(m => m.Tegenpartij == "Welpen ouder");
        vm.ToewijsRekening = vm.Toewijsrekeningen.Single(r => r.Nummer == 8020);

        vm.RegelMakenCommand.Execute(null);

        Assert.Equal(BoekhoudingSubtab.Boekingsregels, vm.Subtab);
        var regel = Assert.Single(vm.Boekingsregels);
        Assert.True(regel.IsNieuw);
        Assert.Equal(CamtVoorbeeld.Tegen, regel.TegenIban);
        Assert.Equal(Richting.Bij, regel.Richting);
        Assert.Equal(8020, regel.GekozenRekening?.Nummer);
        regel.Naam = "Contributie welpen";
        Assert.True(vm.HeeftOnopgeslagenWijzigingen);

        Assert.True(await vm.BewaarAsync());
        Assert.False(Assert.Single(vm.Boekingsregels).IsNieuw);

        await vm.PasRegelsToeCommand.ExecuteAsync(null);
        Assert.Equal(1, vm.AantalTeVerwerken); // alleen energie nog
    }

    [Fact]
    public async Task Regel_zonder_voorwaarde_geeft_fout_en_volgorde_wijzigen()
    {
        var vm = await OpenMetImportAsync();
        vm.NieuweRegelCommand.Execute(null);
        var leeg = vm.Boekingsregels.Single();
        leeg.GekozenRekening = vm.Toewijsrekeningen.Single(r => r.Nummer == 4115);
        Assert.NotNull(leeg.Fout);
        Assert.False(await vm.BewaarAsync());

        leeg.NaamBevat = "energie";
        vm.NieuweRegelCommand.Execute(null);
        var tweede = vm.Boekingsregels.Single(r => r.IsNieuw && r.NaamBevat is null);
        tweede.NaamBevat = "welpen";
        tweede.GekozenRekening = vm.Toewijsrekeningen.Single(r => r.Nummer == 8020);
        Assert.True(await vm.BewaarAsync());
        Assert.Equal(["energie", "welpen"], vm.Boekingsregels.Select(r => r.NaamBevat));

        vm.GeselecteerdeRegel = vm.Boekingsregels[1];
        await vm.RegelOmhoogCommand.ExecuteAsync(null);
        Assert.Equal(["welpen", "energie"], vm.Boekingsregels.Select(r => r.NaamBevat));
    }

    [Fact]
    public async Task Nieuwe_post_biedt_geen_bankdagboek_aan()
    {
        var vm = await OpenMetImportAsync();

        vm.NieuwePostCommand.Execute(null);

        Assert.DoesNotContain(vm.Editor!.Dagboeken, d => d.Soort == DagboekSoort.Bank);
    }

    [Fact]
    public async Task Banksaldo_sluit_aan_na_boeken()
    {
        var vm = await OpenMetImportAsync();
        vm.Bankfilter = MutatieFilter.TeVerwerken;
        foreach (var (naam, nummer) in new[] { ("Energie BV", 4115), ("Welpen ouder", 8020) })
        {
            vm.GeselecteerdeMutatie = vm.Mutaties.Single(m => m.Tegenpartij == naam);
            vm.ToewijsRekening = vm.Toewijsrekeningen.Single(r => r.Nummer == nummer);
            await vm.ToewijzenCommand.ExecuteAsync(null);
        }

        _omgeving.Dialogen.BevestigAntwoord = true;
        await vm.ConceptenBoekenCommand.ExecuteAsync(null);

        Assert.True(vm.IsBankAangesloten);
        Assert.Contains("€ 679,50", vm.Banksaldotekst, StringComparison.Ordinal);
    }
}
