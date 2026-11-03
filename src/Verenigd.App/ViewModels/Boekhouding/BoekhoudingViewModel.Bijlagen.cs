using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.Application.Bijlagen;
using Verenigd.Domain.Financien.Bijlagen;

namespace Verenigd.App.ViewModels.Boekhouding;

/// <summary>
/// Bijlagen bij een post, het subtabblad <i>Postvak bonnen</i> en het filter <i>Zonder bon</i> (stap 4c-3, besluiten
/// 83–88, 96, 97 en 135–140). Toevoegen en verwijderen mag ook bij een geboekte post; de boeking verandert niet.
/// </summary>
public sealed partial class BoekhoudingViewModel
{
    private HashSet<Guid> _zonderBon = [];

    private BijlageDienst Bijlagedienst => field ??= new BijlageDienst(_sessie, _hulpmiddelen.PdfTekst, _tijd);

    /// <summary>De bijlagen van de post in de editor.</summary>
    public ObservableCollection<BijlageRegel> Postbijlagen { get; } = [];

    public Verzameling<BonRegel> Postvak { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BonKoppelenCommand), nameof(BonVerwijderenCommand), nameof(BonBekijkenCommand), nameof(BonGegevensOpslaanCommand))]
    public partial BonRegel? GeselecteerdeBon { get; set; }

    /// <summary>Filter in het Journaal: uitgaven en ontvangsten van bank of kas boven de drempel zonder bijlage (besluiten 145 en 214).</summary>
    [ObservableProperty]
    public partial bool AlleenZonderBon { get; set; }

    /// <summary>Een nieuwe post krijgt pas bijlagen nadat hij is opgeslagen.</summary>
    public bool KanBijlagenToevoegen => !IsBezig && Editor?.Id is not null;

    public string Bijlagenuitleg => KanBijlagenToevoegen
        ? "Sleep bestanden hierheen (PDF, JPG of PNG, maximaal 10 MB)."
        : "Sla de post eerst op om een bijlage toe te voegen.";

    public string Postvaktekst => Postvak.Count == 1 ? "1 bon in het postvak" : $"{Postvak.Count} bonnen in het postvak";

    partial void OnAlleenZonderBonChanged(bool value) => PasPostfilterToe(GeselecteerdePost?.Id);

    private List<PostRegel> MaakPostregels() =>
        [.. _stand!.Posten.Select(p => new PostRegel(p, Woorden, _stand.BijlagenVan(p.Id).Count()))];

    private void LaadBijlagen()
    {
        BerekenZonderBon();
        LaadPostbijlagen();
        var selectie = GeselecteerdeBon?.Id;
        Postvak.Vervang((_stand!.Bijlagen.Where(b => b.IsInPostvak).OrderBy(b => b.ToegevoegdUtc)).Select(bijlage => new BonRegel(bijlage, Postkeuzes)));

        GeselecteerdeBon = Postvak.FirstOrDefault(b => b.Id == selectie) ?? Postvak.FirstOrDefault();
        OnPropertyChanged(nameof(Postvaktekst));
    }

    /// <summary>Besluit 214: de drempel van de eigen administratie.</summary>
    private void BerekenZonderBon() =>
        _zonderBon = [.. ZonderBon.Posten(_stand!, new Domain.Financien.Bedrag(Administratie?.Model.ZonderBonDrempel ?? Domain.Administraties.Administratie.StandaardZonderBonDrempel)).Select(p => p.Id)];

    private void LaadPostbijlagen()
    {
        Postbijlagen.Clear();
        if (_stand is not null && Editor?.Id is { } post)
        {
            foreach (var bijlage in _stand.BijlagenVan(post).OrderBy(b => b.ToegevoegdUtc))
            {
                Postbijlagen.Add(new BijlageRegel(bijlage));
            }
        }

        OnPropertyChanged(nameof(KanBijlagenToevoegen));
        OnPropertyChanged(nameof(Bijlagenuitleg));
    }

    /// <summary>Voorstellen eerst, daarna de andere posten van dat jaar (nieuwste eerst).</summary>
    private IReadOnlyList<PostKeuze> Postkeuzes(DateOnly? datum, Domain.Financien.Bedrag? bedrag)
    {
        if (_stand is null)
        {
            return [];
        }

        var voorstellen = KoppelVoorsteller.Voorstellen(_stand, datum, bedrag);
        var jaar = datum?.Year ?? Jaar;
        return
        [
            .. voorstellen.Select(p => new PostKeuze(p, IsVoorstel: true)),
            .. _stand.Posten.Where(p => p.Datum.Year == jaar && !voorstellen.Contains(p))
                .OrderByDescending(p => p.Datum).Select(p => new PostKeuze(p, IsVoorstel: false)),
        ];
    }

    /// <summary>Na een wijziging in de bijlagen: alleen de stand en de bijlagen verversen, de post in de editor blijft staan.</summary>
    private async Task VernieuwBijlagenAsync()
    {
        _stand = await _dienst.LaadAsync(Soort);
        _allePosten = MaakPostregels();
        LaadBijlagen();
        PasPostfilterToe(GeselecteerdePost?.Id ?? Editor?.Id);
    }

    // ---- Bij een post -----------------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(KanBijlagenToevoegen))]
    private async Task BijlageToevoegenAsync()
    {
        var paden = _dialogen.KiesBijlagen();
        if (paden.Count > 0)
        {
            await VoegBijlagenToeAsync(paden, Editor?.Id);
        }
    }

    /// <summary>Bestanden die op het blok <i>Bijlagen</i> worden gesleept.</summary>
    [RelayCommand(CanExecute = nameof(KanBijlagenToevoegen))]
    private Task BijlagenSlepenAsync(string[]? paden) => paden is { Length: > 0 } ? VoegBijlagenToeAsync(paden, Editor?.Id) : Task.CompletedTask;

    [RelayCommand]
    private Task BijlageBekijkenAsync(BijlageRegel? regel) => regel is null ? Task.CompletedTask : BekijkAsync(regel.Bijlage);

    [RelayCommand]
    private Task BijlageOpslaanAsync(BijlageRegel? regel) => regel is null ? Task.CompletedTask : OpslaanAlsAsync(regel.Bijlage);

    /// <summary>Ook bij een geboekte post (besluit 136), na bevestiging.</summary>
    [RelayCommand]
    private Task BijlageVerwijderenAsync(BijlageRegel? regel) => regel is null ? Task.CompletedTask : VerwijderBijlageAsync(regel.Bijlage);

    // ---- Postvak bonnen ---------------------------------------------------------------------

    [RelayCommand]
    private async Task BonnenToevoegenAsync()
    {
        var paden = _dialogen.KiesBijlagen();
        if (paden.Count > 0)
        {
            await VoegBijlagenToeAsync(paden, postId: null);
        }
    }

    [RelayCommand]
    private Task BonnenSlepenAsync(string[]? paden) => paden is { Length: > 0 } ? VoegBijlagenToeAsync(paden, postId: null) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(KanBonKoppelen))]
    private Task BonKoppelenAsync() =>
        VoerUitAsync(async () =>
        {
            var bon = GeselecteerdeBon!;
            var post = bon.GekozenPost!.Post;
            await BewaarBongegevensAsync(bon);
            await Bijlagedienst.KoppelAsync(Soort, bon.Id, post.Id);
            await VernieuwBijlagenAsync();
            Melding = $"{bon.Bestandsnaam} is gekoppeld aan {post.Nummer}.";
        }, null);

    [RelayCommand(CanExecute = nameof(IsBonGekozen))]
    private Task BonGegevensOpslaanAsync() =>
        VoerUitAsync(async () =>
        {
            await BewaarBongegevensAsync(GeselecteerdeBon!);
            await VernieuwBijlagenAsync();
        }, null);

    [RelayCommand(CanExecute = nameof(IsBonGekozen))]
    private Task BonBekijkenAsync() => BekijkAsync(GeselecteerdeBon!.Bijlage);

    [RelayCommand(CanExecute = nameof(IsBonGekozen))]
    private Task BonVerwijderenAsync() => VerwijderBijlageAsync(GeselecteerdeBon!.Bijlage);

    private bool IsBonGekozen() => !IsBezig && GeselecteerdeBon is not null;

    private bool KanBonKoppelen() => IsBonGekozen() && GeselecteerdeBon is { GekozenPost: not null, Fout: null };

    private async Task BewaarBongegevensAsync(BonRegel bon)
    {
        if (bon.IsGewijzigd)
        {
            await Bijlagedienst.WijzigGegevensAsync(Soort, bon.Id, Contributie.Weergave.AlsDatum(bon.Datum), bon.Bedrag);
        }
    }

    // ---- Gedeeld ----------------------------------------------------------------------------

    private Task VoegBijlagenToeAsync(IReadOnlyList<string> paden, Guid? postId) =>
        VoerUitAsync(async () =>
        {
            var bestanden = new List<BijlageBestand>();
            var overgeslagen = new List<string>();
            foreach (var pad in paden)
            {
                var naam = Path.GetFileName(pad);
                if (new FileInfo(pad).Length > Bijlage.MaximaleGrootte)
                {
                    overgeslagen.Add($"{naam}: een bijlage is maximaal 10 MB.");
                    continue;
                }

                bestanden.Add(new BijlageBestand(naam, await File.ReadAllBytesAsync(pad)));
            }

            var resultaat = await Bijlagedienst.VoegToeAsync(Soort, bestanden, postId);
            await VernieuwBijlagenAsync();
            overgeslagen.AddRange(resultaat.Overgeslagen.Select(o => $"{o.Bestandsnaam}: {o.Reden}"));
            var toegevoegd = resultaat.Toegevoegd.Count == 1 ? "1 bijlage toegevoegd." : $"{resultaat.Toegevoegd.Count} bijlagen toegevoegd.";
            Melding = overgeslagen.Count == 0 ? toegevoegd : $"{toegevoegd} Overgeslagen: {string.Join(" ", overgeslagen)}";
        }, null);

    private Task BekijkAsync(Bijlage bijlage) =>
        VoerUitAsync(async () =>
        {
            var inhoud = await Bijlagedienst.LaadInhoudAsync(Soort, bijlage.Id);
            _dialogen.ToonBijlage(new BijlageVoorbeeldViewModel(bijlage.Bestandsnaam, bijlage.Soort, inhoud, _dialogen));
        }, "De bijlage kon niet worden geopend.");

    private Task OpslaanAlsAsync(Bijlage bijlage) =>
        VoerUitAsync(async () =>
        {
            if (_dialogen.KiesBijlageDoel(bijlage.Bestandsnaam) is { } doel)
            {
                await File.WriteAllBytesAsync(doel, await Bijlagedienst.LaadInhoudAsync(Soort, bijlage.Id));
                Melding = $"Opgeslagen als {Path.GetFileName(doel)}.";
            }
        }, null);

    private async Task VerwijderBijlageAsync(Bijlage bijlage)
    {
        var waar = bijlage.PostId is null ? "uit het postvak" : "bij deze post";
        if (!_dialogen.Bevestig("Bijlage verwijderen", $"Wilt u '{bijlage.Bestandsnaam}' {waar} verwijderen? Dit kan niet ongedaan worden gemaakt."))
        {
            return;
        }

        await VoerUitAsync(async () =>
        {
            await Bijlagedienst.VerwijderAsync(Soort, bijlage.Id);
            ShellLog.BijlageVerwijderd(_logger, Soort.ToString());
            await VernieuwBijlagenAsync();
            Melding = $"{bijlage.Bestandsnaam} is verwijderd.";
        }, null);
    }

    private void BijlageCommandsVernieuwen()
    {
        OnPropertyChanged(nameof(KanBijlagenToevoegen));
        OnPropertyChanged(nameof(Bijlagenuitleg));
        BijlageToevoegenCommand.NotifyCanExecuteChanged();
        BijlagenSlepenCommand.NotifyCanExecuteChanged();
        BonKoppelenCommand.NotifyCanExecuteChanged();
        BonGegevensOpslaanCommand.NotifyCanExecuteChanged();
        BonBekijkenCommand.NotifyCanExecuteChanged();
        BonVerwijderenCommand.NotifyCanExecuteChanged();
    }
}
