using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.Application.Bank;
using BtwBoeker.Application.Tests.Bank;
using BtwBoeker.Domain.Financien.Bank;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.Tests.Boekhouding;

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

        // De knop boekt alle bankrekeningen en toont dus ook dat aantal, ongeacht de gekozen rekening.
        Assert.Equal(3, vm.AantalConceptAlleRekeningen);
        Assert.EndsWith("(3)", vm.ConceptenBoekenTekst, StringComparison.Ordinal);
        vm.GekozenBankrekening = vm.Bankrekeningen.Single(d => d.Code != "RABO");
        Assert.Equal(1, vm.AantalConcept);
        Assert.EndsWith("(3)", vm.ConceptenBoekenTekst, StringComparison.Ordinal);
        vm.GekozenBankrekening = vm.Bankrekeningen.Single(d => d.Code == "RABO");

        _omgeving.Dialogen.BevestigAntwoord = true;
        await vm.ConceptenBoekenCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.AantalConcept);
        Assert.Equal(0, vm.AantalConceptAlleRekeningen);
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
    public async Task Per_bankrekening_het_aantal_te_verwerken_ook_van_de_andere_rekening()
    {
        var vm = await OpenMetImportAsync();
        var betaal = vm.Bankrekeningen.Single(d => d.Code == "RABO");
        var spaar = vm.Bankrekeningen.Single(d => d.Code != "RABO");
        Assert.Equal(2, vm.TeVerwerkenPerRekening[betaal.Id]);
        Assert.False(vm.TeVerwerkenPerRekening.ContainsKey(spaar.Id));

        vm.GekozenBankrekening = spaar; // De keuze verandert de aantallen niet
        Assert.Equal(2, vm.TeVerwerkenPerRekening[betaal.Id]);

        vm.GekozenBankrekening = betaal;
        vm.ToewijsRekening = vm.Toewijsrekeningen.Single(r => r.Nummer == 4115);
        await vm.ToewijzenCommand.ExecuteAsync(null);
        Assert.Equal(1, vm.TeVerwerkenPerRekening[betaal.Id]);
    }

    private static RekeningRegel VoegRekeningToe(BoekhoudingViewModel vm)
    {
        vm.NieuweRekeningCommand.Execute(null);
        var rekening = vm.Rekeningen.Single(r => r.IsNieuw);
        rekening.Nummertekst = "4220";
        rekening.Naam = "Huur Terrein";
        rekening.Soort = RekeningSoort.Resultaat;
        return rekening;
    }

    [Fact]
    public async Task Halve_regel_blokkeert_de_nieuwe_rekening_niet_en_blijft_staan()
    {
        var vm = await _omgeving.OpenMetBankrekeningenAsync();
        vm.NieuweRegelCommand.Execute(null);
        var halve = vm.Boekingsregels.Single();
        halve.Naam = "Gemeente";
        halve.NaamBevat = "gemeente";
        VoegRekeningToe(vm);
        Assert.True(vm.KanOpslaan); // Was de vicieuze cirkel

        Assert.False(await vm.BewaarAsync()); // de regel is nog niet opgeslagen, dus sluiten mag nog niet zomaar

        var rekening = vm.Toewijsrekeningen.Single(r => r.Nummer == 4220);
        var behouden = vm.Boekingsregels.Single();
        Assert.True(behouden.IsNieuw);
        Assert.Equal("gemeente", behouden.NaamBevat);
        Assert.Equal("Gemeente", behouden.Naam);
        Assert.Same(behouden, vm.GeselecteerdeRegel);
        Assert.Contains("1 boekingsregel nog niet", vm.Waarschuwing, StringComparison.Ordinal);
        Assert.False(vm.KanOpslaan); // alleen de halve regel over: niets meer te bewaren

        behouden.GekozenRekening = rekening;
        Assert.True(vm.KanOpslaan);
        Assert.True(await vm.BewaarAsync());
        Assert.Null(vm.Waarschuwing);
        Assert.Equal(4220, Assert.Single(vm.Boekingsregels, r => !r.IsNieuw).GekozenRekening!.Nummer);
    }

    [Fact]
    public async Task Goede_regels_worden_opgeslagen_en_alleen_de_halve_blijft_over()
    {
        var vm = await _omgeving.OpenMetBankrekeningenAsync();
        vm.NieuweRegelCommand.Execute(null);
        var goede = vm.Boekingsregels.Single();
        goede.NaamBevat = "energie";
        goede.GekozenRekening = vm.Toewijsrekeningen.Single(r => r.Nummer == 4115);
        vm.NieuweRegelCommand.Execute(null);
        vm.Boekingsregels.Single(r => r.NaamBevat is null).NaamBevat = "gemeente";
        vm.NieuweRegelCommand.Execute(null);
        vm.Boekingsregels.Single(r => r.NaamBevat is null).NaamBevat = "welpen";

        Assert.False(await vm.BewaarAsync());

        Assert.Equal(["energie", "gemeente", "welpen"], vm.Boekingsregels.Select(r => r.NaamBevat));
        Assert.Equal([false, true, true], vm.Boekingsregels.Select(r => r.IsNieuw));
        Assert.Contains("2 boekingsregels nog niet", vm.Waarschuwing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ongeldige_wijziging_van_een_bestaande_regel_blijft_staan_en_de_rekening_blijft_gekozen()
    {
        var vm = await _omgeving.OpenMetBankrekeningenAsync();
        vm.NieuweRegelCommand.Execute(null);
        var regel = vm.Boekingsregels.Single();
        regel.NaamBevat = "energie";
        regel.GekozenRekening = vm.Toewijsrekeningen.Single(r => r.Nummer == 4115);
        Assert.True(await vm.BewaarAsync());

        var bestaand = vm.Boekingsregels.Single();
        bestaand.NaamBevat = null; // geen voorwaarde meer: ongeldig
        VoegRekeningToe(vm);
        Assert.False(await vm.BewaarAsync());

        var behouden = vm.Boekingsregels.Single();
        Assert.False(behouden.IsNieuw);
        Assert.Null(behouden.NaamBevat);
        Assert.True(behouden.IsGewijzigd);
        Assert.Contains(behouden.GekozenRekening!, vm.Toewijsrekeningen); // zelfde object als in de keuzelijst, anders blijft de keuze leeg
        Assert.Contains(vm.Toewijsrekeningen, r => r.Nummer == 4220);
    }

    [Fact]
    public async Task Bij_boekingsregels_staat_een_melding_zolang_een_nieuwe_rekening_niet_is_opgeslagen()
    {
        var vm = await _omgeving.OpenMetBankrekeningenAsync();
        Assert.False(vm.HeeftOnopgeslagenNieuweRekeningen);

        vm.NieuweRekeningCommand.Execute(null); // nog leeg: niets om op te slaan
        Assert.False(vm.HeeftOnopgeslagenNieuweRekeningen);

        var gemeld = false;
        vm.PropertyChanged += (_, e) => gemeld |= e.PropertyName == nameof(BoekhoudingViewModel.HeeftOnopgeslagenNieuweRekeningen);
        var rekening = vm.Rekeningen.Single(r => r.IsNieuw);
        rekening.Nummertekst = "4220";
        rekening.Naam = "Huur Terrein";
        Assert.True(gemeld);
        Assert.True(vm.HeeftOnopgeslagenNieuweRekeningen);

        Assert.True(await vm.BewaarAsync());
        Assert.False(vm.HeeftOnopgeslagenNieuweRekeningen);
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
