using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.Application.Bank;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Bank;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.App.ViewModels.Boekhouding;

/// <summary>Subtabbladen Bank en Boekingsregels (stap 4b, besluiten 74–82).</summary>
public sealed partial class BoekhoudingViewModel
{
    private BankDienst? _bankdienst;
    private bool _mutatieWordtGezet;

    private BankDienst Bankdienst => _bankdienst ??= new BankDienst(_sessie, _tijd);

    public ObservableCollection<MutatieRegel> Mutaties { get; } = [];

    public ObservableCollection<BoekingsregelRegel> Boekingsregels { get; } = [];

    /// <summary>Open contributieposten voor de geselecteerde ontvangst (stap 4c-1).</summary>
    public ObservableCollection<AfletterKeuze> Afletterkeuzes { get; } = [];

    [ObservableProperty]
    public partial string Afletterentotaal { get; private set; } = Bedrag.Nul.ToString();

    public bool KanAfletteren => Afletterkeuzes.Count > 0;

    public IReadOnlyList<MutatieFilterKeuze> Bankfilters { get; } =
    [
        new(MutatieFilter.TeVerwerken, "Te verwerken"), new(MutatieFilter.Concept, "Concept"), new(MutatieFilter.Geboekt, "Geboekt"), new(MutatieFilter.Alle, "Alle"),
    ];

    public IReadOnlyList<Richting> Richtingen { get; } = Enum.GetValues<Richting>();

    [ObservableProperty]
    public partial IReadOnlyList<Dagboek> Bankrekeningen { get; private set; } = [];

    [ObservableProperty]
    public partial Dagboek? GekozenBankrekening { get; set; }

    [ObservableProperty]
    public partial MutatieFilter Bankfilter { get; set; }

    [ObservableProperty]
    public partial MutatieRegel? GeselecteerdeMutatie { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Grootboekrekening> Toewijsrekeningen { get; private set; } = [];

    [ObservableProperty]
    public partial Grootboekrekening? ToewijsRekening { get; set; }

    [ObservableProperty]
    public partial string ToewijsOmschrijving { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int AantalTeVerwerken { get; private set; }

    [ObservableProperty]
    public partial int AantalConcept { get; private set; }

    [ObservableProperty]
    public partial string Banksaldotekst { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBankAangesloten { get; private set; } = true;

    [ObservableProperty]
    public partial bool HeeftBankrekeningen { get; private set; }

    [ObservableProperty]
    public partial BoekingsregelRegel? GeselecteerdeRegel { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<TransactiecodeKeuze> Transactiecodekeuzes { get; private set; } = [];

    // ---- Inlezen -----------------------------------------------------------------------------

    /// <summary>Bestand › Importeren › Bankafschrift (CAMT.053)…</summary>
    [RelayCommand]
    private async Task BankInlezenAsync()
    {
        if (_stand is null || !IsIngericht)
        {
            _dialogen.ToonInformatie("Bankafschrift inlezen", "Richt eerst de boekhouding in (overnemen uit Excel of leeg beginnen).");
            return;
        }

        if (_dialogen.KiesCamtBestanden() is not { Count: > 0 } paden)
        {
            return;
        }

        var wizard = new BankImportViewModel(Bankdienst, _stand, Soort, paden);
        _dialogen.ToonBankImport(wizard);
        if (!wizard.IsKlaar || wizard.Resultaat is not { } resultaat)
        {
            return;
        }

        ShellLog.BankafschriftIngelezen(_logger, Soort.ToString(), resultaat.Nieuw, resultaat.Herkend, resultaat.Werkvoorraad);
        await VoerUitAsync(async () =>
        {
            await LaadAsync(Editor?.Id);
            Subtab = BoekhoudingSubtab.Bank;
            Melding = wizard.Samenvatting;
        }, "De boekhouding kon niet worden geladen.");
    }

    // ---- Werkvoorraad -------------------------------------------------------------------------

    private bool KanToewijzen() => !IsBezig && GeselecteerdeMutatie is { IsTeVerwerken: true } && ToewijsRekening is not null;

    [RelayCommand(CanExecute = nameof(KanToewijzen))]
    private Task ToewijzenAsync()
    {
        var mutatie = GeselecteerdeMutatie!;
        var rekening = ToewijsRekening!;
        var volgende = Mutaties.SkipWhile(m => m.Id != mutatie.Id).Skip(1).FirstOrDefault()?.Id;
        return VoerUitAsync(async () =>
        {
            await Bankdienst.WijsToeAsync(Soort, mutatie.Id, rekening.Id, string.IsNullOrWhiteSpace(ToewijsOmschrijving) ? null : ToewijsOmschrijving);
            await LaadAsync(Editor?.Id);
            KiesMutatie(volgende);
        }, null);
    }

    /// <summary>Ctrl+B (besluit 82): alle concepten van de bankdagboeken boeken na controle.</summary>
    [RelayCommand]
    private async Task ConceptenBoekenAsync()
    {
        var aantal = _stand?.Bankmutaties.Count(m => Bankoverzicht.Status(_stand, m) == MutatieStatus.Concept) ?? 0;
        if (aantal == 0)
        {
            Melding = Woorden["Melding.GeenBankconcepten"];
            return;
        }

        if (!_dialogen.Bevestig("Concepten boeken",
                string.Create(CultureInfo.CurrentCulture, $"Wilt u {aantal} {(aantal == 1 ? "concept" : "concepten")} van de bank boeken? Een geboekte post kan alleen nog worden tegengeboekt.")))
        {
            return;
        }

        await VoerUitAsync(async () =>
        {
            var uitkomst = await Bankdienst.BoekConceptenAsync(Soort, null);
            await LaadAsync(Editor?.Id);
            Melding = $"{Woorden.Aantal(uitkomst.Aantal, "Post", "Posten")} {Woorden["Melding.Geboekt"]}.";
            if (uitkomst.Fouten.Count > 0)
            {
                Foutmelding = string.Create(CultureInfo.CurrentCulture, $"{uitkomst.Fouten.Count} niet geboekt; bijvoorbeeld {uitkomst.Fouten[0]}");
            }
        }, null);
    }

    [RelayCommand]
    private Task PasRegelsToeAsync() =>
        VoerUitAsync(async () =>
        {
            var aantal = await Bankdienst.PasRegelsToeAsync(Soort);
            await LaadAsync(Editor?.Id);
            Melding = string.Create(CultureInfo.CurrentCulture, $"{aantal} {(aantal == 1 ? "transactie" : "transacties")} uit de werkvoorraad herkend.");
        }, null);

    /// <summary>"Regel maken van deze boeking": een nieuwe boekingsregel met tegen-IBAN en richting van de transactie.</summary>
    [RelayCommand]
    private void RegelMaken()
    {
        if (GeselecteerdeMutatie is not { } regel)
        {
            return;
        }

        var mutatie = regel.Mutatie;
        var rekening = ToewijsRekening ?? Rekeningvan(mutatie);
        var nieuw = new BoekingsregelRegel(null, rekening, BoekingsregelVoorwaarden.Van(mutatie))
        {
            Naam = mutatie.Tegennaam ?? mutatie.Code.Omschrijving,
        };
        VoegRegelToe(nieuw);
    }

    private Grootboekrekening? Rekeningvan(Bankmutatie mutatie)
    {
        var bank = mutatie.Dagboek.Rekening;
        return mutatie.PostId is { } id && _stand?.Posten.FirstOrDefault(p => p.Id == id) is { } post
            ? post.Regels.Select(r => r.Rekening).FirstOrDefault(r => bank is null || r.Id != bank.Id)
            : null;
    }

    partial void OnGekozenBankrekeningChanged(Dagboek? value) => ToonMutaties(GeselecteerdeMutatie?.Id);

    partial void OnBankfilterChanged(MutatieFilter value) => ToonMutaties(GeselecteerdeMutatie?.Id);

    partial void OnGeselecteerdeMutatieChanged(MutatieRegel? value)
    {
        ToewijzenCommand.NotifyCanExecuteChanged();
        VulAfletterkeuzes(value);
        if (_mutatieWordtGezet)
        {
            return;
        }

        ToewijsRekening = value is null ? null : Rekeningvan(value.Mutatie);
        ToewijsOmschrijving = string.Empty;
    }

    partial void OnToewijsRekeningChanged(Grootboekrekening? value) => ToewijzenCommand.NotifyCanExecuteChanged();

    private void LaadBank()
    {
        if (_stand is null)
        {
            return;
        }

        var gekozen = GekozenBankrekening?.Id;
        Bankrekeningen = [.. _stand.Dagboeken.Where(d => d.Soort == DagboekSoort.Bank).OrderBy(d => d.Code, StringComparer.Ordinal)];
        HeeftBankrekeningen = Bankrekeningen.Count > 0;
        Toewijsrekeningen = [.. _stand.Rekeningen.Where(r => r.IsActief).OrderBy(r => r.Nummer)];
        Transactiecodekeuzes = [.. _stand.Bankmutaties
            .SelectMany(m => new[] { (Code: m.Code.Iso, m.Code.Omschrijving), (Code: m.Code.EigenSleutel, m.Code.Omschrijving) })
            .Where(c => c.Code is not null)
            .DistinctBy(c => c.Code)
            .OrderBy(c => c.Code, StringComparer.Ordinal)
            .Select(c => new TransactiecodeKeuze(c.Code!, c.Omschrijving))];
        _mutatieWordtGezet = true;
        try
        {
            GekozenBankrekening = Bankrekeningen.FirstOrDefault(d => d.Id == gekozen) ?? (Bankrekeningen.Count > 0 ? Bankrekeningen[0] : null);
        }
        finally
        {
            _mutatieWordtGezet = false;
        }

        ToonMutaties(GeselecteerdeMutatie?.Id);
        LaadRegels();
    }

    private void ToonMutaties(Guid? selectie)
    {
        if (_stand is null)
        {
            return;
        }

        var dagboek = GekozenBankrekening;
        var alle = _stand.Bankmutaties
            .Where(m => dagboek is not null && (m.Dagboek.Id == dagboek.Id || ReferenceEquals(m.Dagboek, dagboek)))
            .Select(m =>
            {
                var status = Bankoverzicht.Status(_stand, m);
                var post = m.PostId is { } id ? _stand.Posten.FirstOrDefault(p => p.Id == id) : null;
                return new MutatieRegel(m, status, post?.IsGeboekt == true ? post.Nummer : null);
            })
            .ToList();
        AantalTeVerwerken = alle.Count(m => m.Statuswaarde == MutatieStatus.TeVerwerken);
        AantalConcept = alle.Count(m => m.Statuswaarde == MutatieStatus.Concept);
        var zichtbaar = alle
            .Where(m => Bankfilter switch
            {
                MutatieFilter.TeVerwerken => m.Statuswaarde == MutatieStatus.TeVerwerken,
                MutatieFilter.Concept => m.Statuswaarde == MutatieStatus.Concept,
                MutatieFilter.Geboekt => m.Statuswaarde == MutatieStatus.Geboekt,
                _ => true,
            })
            .OrderBy(m => m.DatumWaarde).ThenBy(m => m.Mutatie.Sleutel, StringComparer.Ordinal);
        Mutaties.Clear();
        foreach (var regel in zichtbaar)
        {
            Mutaties.Add(regel);
        }

        KiesMutatie(selectie);
        if (dagboek is null)
        {
            Banksaldotekst = string.Empty;
            IsBankAangesloten = true;
            return;
        }

        var saldi = Bankoverzicht.Saldi(_stand, dagboek);
        IsBankAangesloten = saldi.IsAangesloten;
        Banksaldotekst = saldi.Bank is { } bank
            ? $"Saldo bank {Contributie.Weergave.Datum(saldi.Datum)} {new Bedrag(bank)} · grootboek (met concepten) {new Bedrag(saldi.MetConcepten)}"
                + (saldi.IsAangesloten ? " ✓" : " — verschil " + new Bedrag(bank - saldi.MetConcepten))
            : "Nog geen afschriften ingelezen.";
    }

    private void KiesMutatie(Guid? id)
    {
        GeselecteerdeMutatie = Mutaties.FirstOrDefault(m => m.Id == id) ?? Mutaties.FirstOrDefault();
    }

    // ---- Boekingsregels ----------------------------------------------------------------------

    [RelayCommand]
    private void NieuweRegel() => VoegRegelToe(new BoekingsregelRegel(null, null));

    private void VoegRegelToe(BoekingsregelRegel regel)
    {
        regel.PropertyChanged += RegelGewijzigd;
        Boekingsregels.Add(regel);
        GeselecteerdeRegel = regel;
        Subtab = BoekhoudingSubtab.Boekingsregels;
        CommandsVernieuwen();
    }

    private bool KanRegelVerplaatsen() => !IsBezig && GeselecteerdeRegel is { IsNieuw: false } && !Boekingsregels.Any(r => r.IsGewijzigd);

    /// <summary>Alt+↑.</summary>
    [RelayCommand(CanExecute = nameof(KanRegelVerplaatsen))]
    private Task RegelOmhoogAsync() => VerplaatsAsync(-1);

    /// <summary>Alt+↓.</summary>
    [RelayCommand(CanExecute = nameof(KanRegelVerplaatsen))]
    private Task RegelOmlaagAsync() => VerplaatsAsync(1);

    private Task VerplaatsAsync(int stap)
    {
        var id = GeselecteerdeRegel!.Id!.Value;
        return VoerUitAsync(async () =>
        {
            await Bankdienst.VerplaatsRegelAsync(Soort, id, stap);
            await LaadAsync(Editor?.Id);
            GeselecteerdeRegel = Boekingsregels.FirstOrDefault(r => r.Id == id);
        }, null);
    }

    [RelayCommand]
    private async Task VerwijderRegelAsync()
    {
        if (GeselecteerdeRegel is not { } regel)
        {
            return;
        }

        if (regel.IsNieuw)
        {
            regel.PropertyChanged -= RegelGewijzigd;
            Boekingsregels.Remove(regel);
            CommandsVernieuwen();
            return;
        }

        if (!_dialogen.Bevestig("Boekingsregel verwijderen", $"Wilt u de regel \"{regel.Naam}\" verwijderen? Al gemaakte posten blijven bestaan."))
        {
            return;
        }

        await VoerUitAsync(async () =>
        {
            await Bankdienst.VerwijderRegelAsync(Soort, regel.Id!.Value);
            await LaadAsync(Editor?.Id);
        }, null);
    }

    private void LaadRegels()
    {
        foreach (var regel in Boekingsregels)
        {
            regel.PropertyChanged -= RegelGewijzigd;
        }

        var gekozen = GeselecteerdeRegel?.Id;
        Boekingsregels.Clear();
        foreach (var regel in _stand!.Boekingsregels.OrderBy(r => r.Volgorde).ThenBy(r => r.Id))
        {
            var rij = new BoekingsregelRegel(regel, Toewijsrekeningen.FirstOrDefault(r => r.Id == regel.Rekening.Id) ?? regel.Rekening);
            rij.PropertyChanged += RegelGewijzigd;
            Boekingsregels.Add(rij);
        }

        GeselecteerdeRegel = Boekingsregels.FirstOrDefault(r => r.Id == gekozen);
    }

    private void RegelGewijzigd(object? sender, PropertyChangedEventArgs e) => CommandsVernieuwen();

    partial void OnGeselecteerdeRegelChanged(BoekingsregelRegel? value) => CommandsVernieuwen();

    private async Task BewaarRegelsAsync()
    {
        foreach (var regel in Boekingsregels.Where(r => r.IsGewijzigd).ToList())
        {
            await Bankdienst.BewaarRegelAsync(Soort, regel.Invoer);
        }
    }

    private void VulAfletterkeuzes(MutatieRegel? regel)
    {
        foreach (var keuze in Afletterkeuzes)
        {
            keuze.PropertyChanged -= AfletterkeuzeGewijzigd;
        }

        Afletterkeuzes.Clear();
        if (_stand is not null && regel is { IsTeVerwerken: true, Mutatie.IsBij: true })
        {
            foreach (var stand in Application.Debiteuren.Contributievorderingen.Bereken(_stand).Where(v => v.Saldo > 0))
            {
                var keuze = new AfletterKeuze(stand);
                keuze.PropertyChanged += AfletterkeuzeGewijzigd;
                Afletterkeuzes.Add(keuze);
            }

            foreach (var factuur in Application.Facturen.Factuuroverzicht.Bereken(_stand, Vandaag).Where(f => f.IsOpen).OrderBy(f => f.Factuur.Nummer, StringComparer.Ordinal))
            {
                var keuze = new AfletterKeuze(factuur);
                keuze.PropertyChanged += AfletterkeuzeGewijzigd;
                Afletterkeuzes.Add(keuze);
            }
        }

        OnPropertyChanged(nameof(KanAfletteren));
        AfletterkeuzeGewijzigd(null, new PropertyChangedEventArgs(null));
    }

    private void AfletterkeuzeGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        Afletterentotaal = new Bedrag(Afletterkeuzes.Where(k => k.IsGekozen).Sum(k => k.Saldo)).ToString();
        AfletterenCommand.NotifyCanExecuteChanged();
    }

    private bool KanAfletterenUitvoeren() =>
        !IsBezig && GeselecteerdeMutatie is { IsTeVerwerken: true } mutatie
        && Afletterkeuzes.Any(k => k.IsGekozen)
        && Afletterkeuzes.Where(k => k.IsGekozen).Select(k => k.FactuurId is null).Distinct().Count() == 1 // contributie en facturen niet samen
        && Afletterkeuzes.Where(k => k.IsGekozen).Sum(k => k.Saldo) == mutatie.Mutatie.Bedrag.Centen;

    /// <summary>Een ontvangst afletteren met de gekozen open contributieposten (besluit 102) of verkoopfacturen.</summary>
    [RelayCommand(CanExecute = nameof(KanAfletterenUitvoeren))]
    private Task AfletterenAsync()
    {
        var mutatie = GeselecteerdeMutatie!;
        var gekozen = Afletterkeuzes.Where(k => k.IsGekozen).ToList();
        var volgende = Mutaties.SkipWhile(m => m.Id != mutatie.Id).Skip(1).FirstOrDefault()?.Id;
        return VoerUitAsync(async () =>
        {
            if (gekozen[0].FactuurId is null)
            {
                await new Application.Debiteuren.ContributieboekingDienst(_sessie, _tijd).LetterAfAsync(mutatie.Id, [.. gekozen.Select(k => k.Vordering)]);
            }
            else
            {
                await Factuurdienst.LetterAfAsync(Soort, mutatie.Id, [.. gekozen.Select(k => k.FactuurId!.Value)]);
            }
            await LaadAsync(Editor?.Id);
            KiesMutatie(volgende);
        }, null);
    }

    private void BankCommandsVernieuwen()
    {
        ToewijzenCommand.NotifyCanExecuteChanged();
        AfletterenCommand.NotifyCanExecuteChanged();
        RegelOmhoogCommand.NotifyCanExecuteChanged();
        RegelOmlaagCommand.NotifyCanExecuteChanged();
    }
}
