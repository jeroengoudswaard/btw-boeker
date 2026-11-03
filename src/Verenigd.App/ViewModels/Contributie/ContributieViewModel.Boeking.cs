using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Debiteuren;
using Verenigd.Application.Email;
using Verenigd.App.ViewModels.Email;
using Verenigd.Domain.Communicatie;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.App.ViewModels.Contributie;

/// <summary>Stap 4c-1: opbrengstrekeningen, boeken van runs en het subtabblad Openstaand (besluiten 63 en 89–103).</summary>
public sealed partial class ContributieViewModel
{
    private BoekhoudingStand? _boekhouding;

    /// <summary>Per jeugdlidcategorie de opbrengstrekening (Contributie › Tarieven).</summary>
    public ObservableCollection<OpbrengstRegel> Opbrengstrekeningen { get; } = [];

    public Verzameling<VorderingRegel> Vorderingen { get; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Grootboekrekening> Opbrengstkeuzes { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Grootboekrekening> Kostenrekeningen { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsBoekhoudingIngericht { get; private set; }

    [ObservableProperty]
    public partial bool AlleenOpenstaand { get; set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(HerincassoCommand), nameof(OninbaarCommand), nameof(MailStorneringCommand))]
    public partial VorderingRegel? GeselecteerdeVordering { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OninbaarCommand))]
    public partial Grootboekrekening? Oninbaarrekening { get; set; }

    [ObservableProperty]
    public partial string TotaalOpenstaand { get; private set; } = Bedrag.Nul.ToString();

    private async Task LaadBoekhoudingAsync()
    {
        _boekhouding = await _sessie.Boekhouding.LaadAsync(AdministratieSoort.Vereniging);
        IsBoekhoudingIngericht = _boekhouding.Dagboeken.Any(d => d.Soort == DagboekSoort.Memoriaal);
        var actief = _boekhouding.Rekeningen.Where(r => r.IsActief && r.Soort == RekeningSoort.Resultaat).OrderBy(r => r.Nummer).ToList();
        Opbrengstkeuzes = actief;
        Kostenrekeningen = actief;

        foreach (var regel in Opbrengstrekeningen)
        {
            regel.PropertyChanged -= OpbrengstGewijzigd;
        }

        Opbrengstrekeningen.Clear();
        foreach (var categorie in Categorieen.Where(c => c.HeeftTarief))
        {
            var regel = new OpbrengstRegel(categorie, _boekhouding.Rekeningen.FirstOrDefault(r => r.Id == categorie.OpbrengstrekeningId));
            regel.PropertyChanged += OpbrengstGewijzigd;
            Opbrengstrekeningen.Add(regel);
        }

        Runs.Clear();
        var effectief = Contributievorderingen.Effectief(_boekhouding).ToList();
        foreach (var run in _stand!.Runs)
        {
            var postId = effectief.FirstOrDefault(m => m.IncassorunId == run.Id && m.Soort == Domain.Financien.Debiteuren.DebiteurmutatieSoort.Vordering)?.PostId;
            var nummer = postId is { } id ? _boekhouding.Posten.FirstOrDefault(p => p.Id == id)?.Nummer : null;
            Runs.Add(new RunRegel(run, nummer, Contributievorderingen.IsOntvangen(_boekhouding, run.Id)));
        }

        ToonVorderingen();
    }

    private void ToonVorderingen()
    {
        if (_boekhouding is null)
        {
            return;
        }

        var gekozen = GeselecteerdeVordering?.Vordering;
        var alle = Contributievorderingen.Bereken(_boekhouding).Select(v => new VorderingRegel(v)).ToList();
        TotaalOpenstaand = new Bedrag(alle.Where(v => v.IsOpen).Sum(v => v.Stand.Saldo)).ToString();
        Vorderingen.Vervang(alle.Where(v => !AlleenOpenstaand || v.IsOpen));

        GeselecteerdeVordering = Vorderingen.FirstOrDefault(v => v.Vordering == gekozen);
    }

    partial void OnAlleenOpenstaandChanged(bool value) => ToonVorderingen();

    private void OpbrengstGewijzigd(object? sender, PropertyChangedEventArgs e) => WijzigingenGewijzigd();

    private bool KanBoekRun(RunRegel? regel) => regel is { KanBoeken: true } && IsBoekhoudingIngericht && !IsBezig;

    /// <summary>Een definitieve run die nog niet is geboekt alsnog boeken (besluit 99).</summary>
    [RelayCommand(CanExecute = nameof(KanBoekRun))]
    private async Task BoekRunAsync(RunRegel? regel)
    {
        if (regel is null)
        {
            return;
        }

        try
        {
            var nummer = await _boeking.BoekRunAsync(regel.Run.Id);
            Foutmelding = null;
            await LaadAsync(Betaler?.Id);
            _dialogen.ToonInformatie("Run boeken", nummer is null ? "Er was niets te boeken." : $"De run van {regel.Maand} is geboekt als {nummer}.");
        }
        catch (InvalidOperationException ex)
        {
            Foutmelding = ex.Message;
        }
    }

    private bool KanHerincasso() => GeselecteerdeVordering is { Stand.Status: Vorderingstatus.Gestorneerd, HerincassoGepland: false };

    /// <summary>Besluit 92: de gestorneerde post gaat mee in de volgende run.</summary>
    [RelayCommand(CanExecute = nameof(KanHerincasso))]
    private async Task HerincassoAsync()
    {
        var regel = GeselecteerdeVordering!;
        try
        {
            await _boeking.VraagHerincassoAanAsync(regel.Vordering);
            Foutmelding = null;
            await LaadAsync(Betaler?.Id);
            GeselecteerdeVordering = Vorderingen.FirstOrDefault(v => v.Vordering == regel.Vordering);
        }
        catch (InvalidOperationException ex)
        {
            Foutmelding = ex.Message;
        }
    }

    private bool KanOninbaar() => GeselecteerdeVordering is { IsOpen: true } && Oninbaarrekening is not null;

    /// <summary>Besluit 101: afboeken als oninbaar op de gekozen kostenrekening.</summary>
    [RelayCommand(CanExecute = nameof(KanOninbaar))]
    private async Task OninbaarAsync()
    {
        var regel = GeselecteerdeVordering!;
        var rekening = Oninbaarrekening!;
        if (!_dialogen.Bevestig("Afboeken als oninbaar",
                $"Wilt u {regel.Saldo} van {regel.Betaler} ({regel.Run}) afboeken op {rekening}? Er wordt een geboekte memoriaalpost gemaakt."))
        {
            return;
        }

        try
        {
            await _boeking.BoekOninbaarAsync(regel.Vordering, rekening.Id);
            Foutmelding = null;
            await LaadAsync(Betaler?.Id);
        }
        catch (InvalidOperationException ex)
        {
            Foutmelding = ex.Message;
        }
    }

    private bool KanStorneringMailen() => GeselecteerdeVordering is { Stand.Status: Vorderingstatus.Gestorneerd } && _stand is not null;

    /// <summary>Besluit 204: een bericht over de stornering aan de leden en voogden onder de betaler, via E-mail › Opstellen.</summary>
    [RelayCommand(CanExecute = nameof(KanStorneringMailen))]
    private Task MailStorneringAsync()
    {
        var regel = GeselecteerdeVordering!;
        var ontvangers = Berichten.Stornering(regel.Stand, _stand!, Vandaag, Administratie?.Naam ?? string.Empty);
        return BiedEmailAanAsync(new EmailVoorstel(Emailsoort.Stornering, ontvangers, $"Stornering {regel.Run} van {regel.Betaler}"));
    }
}
