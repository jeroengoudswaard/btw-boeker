using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using BtwBoeker.App.Diensten;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Database;
using BtwBoeker.Application.Modules;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.ViewModels.Boekhouding;

/// <summary>
/// Tabblad Boekhouding van één administratie (stap 4a) met de subtabbladen Journaal, Rekeningschema, Grootboekkaart,
/// Rapporten en Boekjaren. Geboekte posten zijn onveranderlijk; corrigeren gaat via tegenboeken.
/// </summary>
public sealed partial class BoekhoudingViewModel : TabbladViewModel, IPaginameldingen
{
    private readonly IDatabaseSessie _sessie;
    private readonly IDialoogService _dialogen;
    private readonly TimeProvider _tijd;
    private readonly BoekhoudingHulpmiddelen _hulpmiddelen;
    private readonly ILogger _logger;
    private readonly BoekhoudingDienst _dienst;
    private BoekhoudingStand? _stand;
    private List<PostRegel> _allePosten = [];
    private bool _selectieWordtGezet;

    public BoekhoudingViewModel(
        IDatabaseSessie sessie, IDialoogService dialogen, TimeProvider tijd, AdministratieViewModel administratie,
        BoekhoudingHulpmiddelen hulpmiddelen, ILogger logger)
        : base(ModuleCatalogus.Geef(ModuleSoort.Boekhouding), administratie)
    {
        _sessie = sessie;
        _dialogen = dialogen;
        _tijd = tijd;
        _hulpmiddelen = hulpmiddelen;
        _logger = logger;
        _dienst = new BoekhoudingDienst(sessie, tijd);
        VernieuwCommand = new AsyncRelayCommand(VernieuwAsync);
        OpslaanCommand = new AsyncRelayCommand(BewaarAsync, () => KanOpslaan);
        BoekenCommand = new AsyncRelayCommand(BoekAsync, () => !IsBezig && Editor?.KanBoeken == true);
        VerwijderConceptCommand = new AsyncRelayCommand(VerwijderConceptAsync, () => !IsBezig && GeselecteerdePost?.IsConcept == true);
        TegenboekenCommand = new AsyncRelayCommand(TegenboekAsync, () => !IsBezig && GeselecteerdePost?.KanTegenboeken == true);
        SluitBoekjaarCommand = new AsyncRelayCommand<JaarRegel>(SluitBoekjaarAsync, j => !IsBezig && j?.IsOpen == true);
        HeropenBoekjaarCommand = new AsyncRelayCommand<JaarRegel>(HeropenBoekjaarAsync, j => !IsBezig && j?.IsAfgesloten == true);
        Jaar = Vandaag.Year;
        Woorden.PropertyChanged += WoordenGewijzigd;
    }

    /// <summary>De woorden van de gekozen weergave; alleen visueel.</summary>
    public Woordenboek Woorden => _hulpmiddelen.Woorden;

    protected override string Moduletitel => Woorden["Boekhouding"];

    public IAsyncRelayCommand VernieuwCommand { get; }

    public IAsyncRelayCommand OpslaanCommand { get; }

    /// <summary>Ctrl+Enter.</summary>
    public IAsyncRelayCommand BoekenCommand { get; }

    public IAsyncRelayCommand VerwijderConceptCommand { get; }

    public IAsyncRelayCommand TegenboekenCommand { get; }

    public IAsyncRelayCommand<JaarRegel> SluitBoekjaarCommand { get; }

    public IAsyncRelayCommand<JaarRegel> HeropenBoekjaarCommand { get; }

    public Verzameling<PostRegel> Posten { get; } = [];

    public Verzameling<RekeningRegel> Rekeningen { get; } = [];

    public ObservableCollection<KaartRegel> KaartMutaties { get; } = [];

    public ObservableCollection<JaarRegel> Jaren { get; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Rubriek> Rubrieken { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Grootboekrekening> Rekeningkeuzes { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeeftMeerdereBoekjaren))]
    public partial IReadOnlyList<int> Boekjaren { get; private set; } = [];

    /// <summary>De jaarkeuze alleen bij meer dan één boekjaar.</summary>
    public bool HeeftMeerdereBoekjaren => Boekjaren.Count > 1;

    /// <summary>Afdrukken en exporteren alleen waar er iets af te drukken is.</summary>
    public bool HeeftUitvoer => Subtab is BoekhoudingSubtab.Rapporten or BoekhoudingSubtab.Grootboekkaart;

    /// <summary>Het aantal posten zonder bon in het gekozen jaar; 0 = het filter is niet zichtbaar.</summary>
    public int AantalZonderBon => _allePosten.Count(p => p.DatumWaarde.Year == Jaar && _zonderBon.Contains(p.Id));

    public string ZonderBonTekst => $"{AantalZonderBon} zonder bon";

    /// <summary>Het filter blijft zichtbaar zolang het aan staat, ook als de lijst leeg raakt.</summary>
    public bool IsZonderBonZichtbaar => AantalZonderBon > 0 || AlleenZonderBon;

    public IReadOnlyList<RekeningSoort> Soorten { get; } = Enum.GetValues<RekeningSoort>();

    public IReadOnlyList<RekeningRol> Rollen { get; } = Enum.GetValues<RekeningRol>();

    public IReadOnlyList<Rapportkeuze> Rapportkeuzes =>
        [new(Rapportsoort.Saldibalans, Woorden["Saldibalans"]), new(Rapportsoort.Balans, Woorden["Balans"]), new(Rapportsoort.WinstEnVerlies, Woorden["WinstEnVerlies"]),
            new(Rapportsoort.JaarrekeningBalans, Woorden["Rapport.JaarrekeningBalans"]),
            new(Rapportsoort.JaarrekeningBatenLasten, Woorden["Rapport.JaarrekeningBatenLasten"])];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLeeg))]
    public partial bool IsIngericht { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLeeg))]
    public partial bool IsGeladen { get; private set; }

    public bool IsLeeg => IsGeladen && !IsIngericht;

    [ObservableProperty]
    public partial BoekhoudingSubtab Subtab { get; set; }

    [ObservableProperty]
    public partial int Jaar { get; set; }

    [ObservableProperty]
    public partial string Zoektekst { get; set; } = string.Empty;

    [ObservableProperty]
    public partial PostRegel? GeselecteerdePost { get; set; }

    [ObservableProperty]
    public partial PostEditor? Editor { get; private set; }

    [ObservableProperty]
    public partial RekeningRegel? GeselecteerdeRekening { get; set; }

    [ObservableProperty]
    public partial string NieuweRubriekNaam { get; set; } = string.Empty;

    /// <summary>Het invoerveld voor een rubriek pas na een klik op <i>+ Rubriek</i>.</summary>
    [ObservableProperty]
    public partial bool IsRubriekinvoerOpen { get; set; }

    /// <summary>Alleen als er concepten zijn een melding dat die niet meetellen.</summary>
    [ObservableProperty]
    public partial string? Conceptmelding { get; private set; }

    [RelayCommand]
    private void RubriekinvoerOpenen() => IsRubriekinvoerOpen = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJaarrekening))]
    public partial Rapportsoort Rapportsoort { get; set; }

    /// <summary>De jaarrekening volgens de eigen indeling: dan zijn <i>Rekeningen tonen</i> en <i>Indeling bewerken</i> zichtbaar.</summary>
    public bool IsJaarrekening => Rapportsoort is Rapportsoort.JaarrekeningBalans or Rapportsoort.JaarrekeningBatenLasten;

    /// <summary>De rekeningen onder elke regel van de jaarrekening tonen; standaard aan.</summary>
    [ObservableProperty]
    public partial bool RekeningenTonen { get; set; } = true;

    /// <summary>⚠ als er rekeningen met bedragen in geen regel van de indeling vallen.</summary>
    [ObservableProperty]
    public partial string? JaarrekeningMelding { get; private set; }

    [ObservableProperty]
    public partial RapportTabel? Rapport { get; private set; }

    [ObservableProperty]
    public partial Grootboekrekening? KaartRekening { get; set; }

    [ObservableProperty]
    public partial DateTime? KaartVan { get; set; }

    [ObservableProperty]
    public partial DateTime? KaartTot { get; set; }

    [ObservableProperty]
    public partial string KaartBeginsaldo { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string KaartEindsaldo { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial RapportTabel? KaartTabel { get; private set; }

    /// <summary>Kolomkoppen van de kaart per soort potje.</summary>
    [ObservableProperty]
    public partial string KaartDebetkop { get; private set; } = "Debet";

    [ObservableProperty]
    public partial string KaartCreditkop { get; private set; } = "Credit";

    /// <summary>Uitleg bij debet en credit als tooltip, alleen in Eenvoudig.</summary>
    [ObservableProperty]
    public partial string? KaartKoptip { get; private set; }

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    [ObservableProperty]
    public partial string? Melding { get; private set; }

    [ObservableProperty]
    public partial string? Waarschuwing { get; private set; }

    /// <summary>Alleen opgeslagen rekeningen staan in de keuzelijsten; de pagina Boekingsregels meldt dat.</summary>
    public bool HeeftOnopgeslagenNieuweRekeningen => Rekeningen.Any(r => r.IsNieuw && r.IsGewijzigd);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KanOpslaan))]
    public partial bool IsBezig { get; private set; }

    public string Aantaltekst => string.Create(CultureInfo.CurrentCulture,
        $"{Posten.Count} {(Posten.Count == 1 ? "post" : "posten")} · {Posten.Count(p => p.IsConcept)} concept");

    public override bool HeeftOnopgeslagenWijzigingen =>
        Editor?.IsGewijzigd == true || Rekeningen.Any(r => r.IsGewijzigd) || Boekingsregels.Any(r => r.IsGewijzigd) || RelatiesGewijzigd;

    /// <summary>Een halve boekingsregel blokkeert de rest niet; hij blijft staan tot hij klopt.</summary>
    private bool HeeftBewaarbareWijzigingen =>
        Editor?.IsGewijzigd == true || Rekeningen.Any(r => r.IsGewijzigd) || Boekingsregels.Any(r => r.IsGewijzigd && r.Fout is null) || RelatiesGewijzigd;

    public override bool KanOpslaan =>
        HeeftBewaarbareWijzigingen && !IsBezig
        && (Editor is not { IsGewijzigd: true } || Editor.Fouten.Count == 0)
        && Rekeningen.Where(r => r.IsGewijzigd).All(r => r.Fout is null)
        && RelatiesGeldig;

    /// <summary>Wat Ctrl+P afdrukt en de export wegschrijft: de grootboekkaart of het gekozen rapport.</summary>
    public RapportTabel? Afdruktabel => Subtab switch
    {
        BoekhoudingSubtab.Grootboekkaart when KaartTabel is not null => KaartTabel,
        _ => Rapport,
    };

    private DateOnly Vandaag => DateOnly.FromDateTime(_tijd.GetLocalNow().DateTime);

    private Domain.Administraties.AdministratieSoort Soort => Administratie!.Soort;

    private string Administratienaam => Administratie!.Naam;

    /// <summary>
    /// Ctrl+S: het concept en gewijzigde rekeningen opslaan. Onwaar als er iets niet is opgeslagen, zoals een halve boekingsregel
    ///: zo kan de pagina niet ongemerkt worden gesloten.
    /// </summary>
    public override async Task<bool> BewaarAsync()
    {
        if (!KanOpslaan)
        {
            return !HeeftOnopgeslagenWijzigingen;
        }

        Waarschuwing = null;
        IsBezig = true;
        try
        {
            foreach (var rekening in Rekeningen.Where(r => r.IsGewijzigd).ToList())
            {
                await _dienst.BewaarRekeningAsync(Soort, rekening.Invoer);
            }

            await BewaarRegelsAsync();
            await BewaarRelatiesAsync();
            Guid? selectie = Editor?.Id;
            if (Editor is { IsGewijzigd: true } editor)
            {
                selectie = await _dienst.BewaarConceptAsync(Soort, editor.Invoer);
            }

            Foutmelding = null;
            _halveRegels = [.. Boekingsregels.Where(r => r.IsGewijzigd && r.Fout is not null)];
            var aantalHalf = _halveRegels.Count;
            await LaadAsync(selectie);
            if (aantalHalf == 0)
            {
                return true;
            }

            Waarschuwing = string.Format(CultureInfo.CurrentCulture, Woorden["Melding.RegelsNietOpgeslagen"],
                Woorden.Aantal(aantalHalf, "Boekingsregel.Enkelvoud", "Boekingsregel.Meervoud"));
            return false;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Foutmelding = ex.Message;
            return false;
        }
        finally
        {
            _halveRegels = [];
            IsBezig = false;
            CommandsVernieuwen();
        }
    }

    public override void Opruimen()
    {
        KoppelEditor(null);
        KoppelRekeningen(false);
        KoppelRelaties(false);
        Woorden.PropertyChanged -= WoordenGewijzigd;
        base.Opruimen();
    }

    private async Task VernieuwAsync()
    {
        if (HeeftOnopgeslagenWijzigingen
            && !_dialogen.Bevestig("Vernieuwen", "Er zijn niet-opgeslagen wijzigingen. Wilt u die verwerpen en de gegevens opnieuw laden?"))
        {
            return;
        }

        await VoerUitAsync(() => LaadAsync(Editor?.Id), "De boekhouding kon niet worden geladen.");
    }

    private async Task LaadAsync(Guid? postSelectie)
    {
        using var meting = Meetpunt.Start(_logger, "Boekhouding laden " + Soort);
        _stand = await _dienst.LaadAsync(Soort);
        IsIngericht = _stand.IsIngericht || _stand.Dagboeken.Count > 0;
        IsGeladen = true;
        Rubrieken = [.. _stand.Rubrieken.OrderBy(r => r.Volgorde).ThenBy(r => r.Naam, StringComparer.CurrentCulture)];
        Rekeningkeuzes = [.. _stand.Rekeningen.OrderBy(r => r.Nummer)];
        Boekjaren = [.. _stand.Boekjaren.Select(b => b.Jaar).Order()];
        if (Boekjaren.Count > 0 && !Boekjaren.Contains(Jaar))
        {
            Jaar = Boekjaren.Contains(Vandaag.Year) ? Vandaag.Year : Boekjaren[^1];
        }

        KoppelRekeningen(false);
        Rekeningen.Vervang((Rekeningkeuzes).Select(rekening => new RekeningRegel(rekening, Rubrieken.FirstOrDefault(r => r.Id == rekening.Rubriek?.Id), Grootboek.Saldo(_stand, rekening, Jaar))));

        KoppelRekeningen(true);

        Jaren.Clear();
        foreach (var boekjaar in _stand.Boekjaren.OrderByDescending(b => b.Jaar))
        {
            var posten = _stand.Posten.Where(p => p.Datum.Year == boekjaar.Jaar).ToList();
            Jaren.Add(new JaarRegel(
                boekjaar.Jaar, boekjaar.IsOpen, boekjaar.IsOpen ? "Open" : "Afgesloten",
                posten.Count(p => !p.IsGeboekt), posten.Count(p => p.IsGeboekt)));
        }

        _allePosten = MaakPostregels();
        BerekenZonderBon();
        PasPostfilterToe(postSelectie);
        if (postSelectie is null || GeselecteerdePost is null)
        {
            ToonEditor(null, nieuw: false);
        }

        if (KaartRekening is { } kaart)
        {
            KaartRekening = Rekeningkeuzes.FirstOrDefault(r => r.Id == kaart.Id);
        }

        StelKaartperiodeIn();
        BerekenRapport();
        BerekenKaart();
        LaadBank();
        LaadRelaties();
        LaadBijlagen();
    }

    private void PasPostfilterToe(Guid? selectie)
    {
        var zoek = Zoektekst.Trim();
        var zichtbaar = _allePosten
            .Where(p => p.DatumWaarde.Year == Jaar)
            .Where(p => !AlleenZonderBon || _zonderBon.Contains(p.Id))
            .Where(p => zoek.Length == 0
                || p.Nummer.Contains(zoek, StringComparison.CurrentCultureIgnoreCase)
                || p.Omschrijving.Contains(zoek, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(p => p.IsGeboekt ? 0 : 1).ThenBy(p => p.DatumWaarde).ThenBy(p => p.Nummer, StringComparer.Ordinal);
        _selectieWordtGezet = true;
        try
        {
            Posten.Vervang(zichtbaar);

            GeselecteerdePost = Posten.FirstOrDefault(p => p.Id == selectie);
        }
        finally
        {
            _selectieWordtGezet = false;
        }

        if (GeselecteerdePost is { } gekozen)
        {
            ToonEditor(gekozen.Post, nieuw: false);
        }

        OnPropertyChanged(nameof(Aantaltekst));
        OnPropertyChanged(nameof(AantalZonderBon));
        OnPropertyChanged(nameof(ZonderBonTekst));
        OnPropertyChanged(nameof(IsZonderBonZichtbaar));
    }

    // ---- Inrichten --------------------------------------------------------------------------

    [RelayCommand]
    private Task LeegBeginnenAsync() =>
        VoerUitAsync(async () =>
        {
            await _dienst.RichtInAsync(Soort);
            await LaadAsync(null);
        }, "De boekhouding kon niet worden ingericht.");

    /// <summary>Bestand › Importeren › Rekeningschema-sjabloon.</summary>
    [RelayCommand]
    private Task SjabloonInlezenAsync()
    {
        if (IsIngericht)
        {
            _dialogen.ToonInformatie("Rekeningschema-sjabloon", $"De boekhouding van {Administratienaam} is al ingericht; een sjabloon kan alleen in een lege administratie.");
            return Task.CompletedTask;
        }

        return _dialogen.KiesSjabloonbestand() is { } pad
            ? RichtInMetSjabloonAsync(() => Rekeningsjablonen.Lees(File.ReadAllText(pad)))
            : Task.CompletedTask;
    }

    private async Task RichtInMetSjabloonAsync(Func<Rekeningsjabloon> sjabloon)
    {
        if (IsIngericht)
        {
            return;
        }

        SjablooninrichtingViewModel wizard;
        Rekeningsjabloon gelezen;
        try
        {
            gelezen = sjabloon();
            wizard = new SjablooninrichtingViewModel(_dienst, Soort, gelezen, Vandaag.Year) { Bevestiging = _dialogen.Bevestig };
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Foutmelding = ex is InvalidOperationException ? ex.Message : "Het sjabloonbestand kan niet worden gelezen.";
            return;
        }

        Foutmelding = null;
        if (!_dialogen.ToonSjablooninrichting(wizard) || !wizard.IsKlaar)
        {
            return;
        }

        ShellLog.BoekhoudingOvergenomen(_logger, Soort.ToString(), wizard.AantalIngericht);
        if (gelezen.Jaarrekening.Count > 0)
        {
            // De eigen jaarrekeningindeling uit het sjabloon, op naam van de rubrieken.
            await VoerUitAsync(() => new JaarrekeningDienst(_sessie).PasSjabloonToeAsync(Soort, gelezen.Jaarrekening), "De jaarrekeningindeling uit het sjabloon kon niet worden overgenomen.");
        }

        await VoerUitAsync(() => LaadAsync(null), "De boekhouding kon niet worden geladen.");
    }

    /// <summary>Bestand › Exporteren › Rekeningschema als sjabloon: alleen nummers, namen, rubrieken en rollen.</summary>
    [RelayCommand]
    private Task ExporteerSjabloonAsync()
    {
        if (_stand is not { IsIngericht: true } stand
            || _dialogen.KiesSjabloonDoel($"rekeningschema-{Administratienaam.ToLowerInvariant().Replace(' ', '-')}{Rekeningsjablonen.Extensie}") is not { } doel)
        {
            return Task.CompletedTask;
        }

        return VoerUitAsync(async () =>
        {
            await File.WriteAllTextAsync(doel, Rekeningsjablonen.Schrijf(Rekeningsjablonen.UitStand(stand, $"Rekeningschema {Administratienaam}")));
            Melding = $"Rekeningschema opgeslagen als {Path.GetFileName(doel)}. Het bevat geen bedragen of persoonsgegevens.";
        }, null);
    }

    /// <summary>
    /// Bestand › Exporteren › Auditfile (XAF) en de knop bij Boekjaren: het gekozen boekjaar van deze administratie als
    /// XAF 3.2, voor een accountant of een overstap naar een ander pakket.
    /// </summary>
    [RelayCommand]
    private Task ExporteerXafAsync(JaarRegel? regel)
    {
        if (_stand is not { IsIngericht: true } || Boekjaren.Count == 0)
        {
            return Task.CompletedTask;
        }

        var jaar = regel?.Jaar ?? Jaar;
        if (_dialogen.KiesXafDoel(XafDienst.Bestandsnaam(Administratienaam, jaar)) is not { } doel)
        {
            return Task.CompletedTask;
        }

        return VoerUitAsync(async () =>
        {
            var versie = typeof(BoekhoudingViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            var resultaat = await new XafDienst(_sessie, _tijd).MaakAsync(Soort, jaar, versie);
            await File.WriteAllBytesAsync(doel, resultaat.Inhoud);
            Melding = resultaat.OvergeslagenConcepten == 0
                ? $"✓ Auditfile {jaar} opgeslagen als {Path.GetFileName(doel)} ({resultaat.AantalPosten} geboekte posten)."
                : $"⚠ Auditfile {jaar} opgeslagen als {Path.GetFileName(doel)} ({resultaat.AantalPosten} geboekte posten); "
                    + $"{resultaat.OvergeslagenConcepten} conceptpost(en) niet meegenomen. Boek ze eerst als ze erbij horen.";
        }, null);
    }

    // ---- Journaal ---------------------------------------------------------------------------

    /// <summary>Ctrl+N binnen het tabblad.</summary>
    [RelayCommand]
    private void NieuwePost()
    {
        if (_stand is null || !BevestigVerwerpen())
        {
            return;
        }

        Subtab = BoekhoudingSubtab.Journaal;
        _selectieWordtGezet = true;
        GeselecteerdePost = null;
        _selectieWordtGezet = false;
        ToonEditor(null, nieuw: true);
    }

    private async Task BoekAsync()
    {
        if (Editor is not { KanBoeken: true } editor)
        {
            return;
        }

        IsBezig = true;
        try
        {
            var nummer = await _dienst.BoekAsync(Soort, editor.Invoer);
            Foutmelding = null;
            Melding = string.Format(CultureInfo.CurrentCulture, Woorden["Melding.PostGeboekt"], nummer);
            await LaadAsync(null);
            if (_allePosten.FirstOrDefault(p => p.Nummer == nummer) is { } geboekt)
            {
                Jaar = geboekt.DatumWaarde.Year;
                PasPostfilterToe(geboekt.Id);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Foutmelding = ex.Message;
        }
        finally
        {
            IsBezig = false;
            CommandsVernieuwen();
        }
    }

    private async Task VerwijderConceptAsync()
    {
        if (GeselecteerdePost is not { IsConcept: true } post
            || !_dialogen.Bevestig("Concept verwijderen", $"Wilt u het concept \"{post.Omschrijving}\" verwijderen?"))
        {
            return;
        }

        await VoerUitAsync(async () =>
        {
            await _dienst.VerwijderConceptAsync(Soort, post.Id);
            KoppelEditor(null);
            Editor = null;
            await LaadAsync(null);
        }, "Het concept kon niet worden verwijderd.");
    }

    private async Task TegenboekAsync()
    {
        if (GeselecteerdePost is not { KanTegenboeken: true } post)
        {
            return;
        }

        var datum = Vandaag.Year == post.DatumWaarde.Year ? Vandaag : post.DatumWaarde;
        if (!_dialogen.Bevestig("Tegenboeken",
                $"Wilt u post {post.Nummer} tegenboeken op {Weergave.Datum(datum)}? Er wordt een geboekte post met omgekeerde bedragen gemaakt."))
        {
            return;
        }

        await VoerUitAsync(async () =>
        {
            var nummer = await _dienst.TegenboekAsync(Soort, post.Id, datum);
            Melding = string.Format(CultureInfo.CurrentCulture, Woorden["Melding.Tegenboeking"], nummer);
            await LaadAsync(null);
            PasPostfilterToe(_allePosten.FirstOrDefault(p => p.Nummer == nummer)?.Id);
        }, null);
    }

    partial void OnGeselecteerdePostChanged(PostRegel? value)
    {
        CommandsVernieuwen();
        if (_selectieWordtGezet || value is null)
        {
            return;
        }

        if (Editor?.Id != value.Id && BevestigVerwerpen())
        {
            ToonEditor(value.Post, nieuw: false);
        }
    }

    private bool BevestigVerwerpen() =>
        Editor is not { IsGewijzigd: true }
        || _dialogen.Bevestig("Post wijzigen", "De huidige post is niet opgeslagen. Wilt u de wijzigingen verwerpen?");

    private void ToonEditor(Journaalpost? post, bool nieuw)
    {
        if (_stand is null || (post is null && !nieuw))
        {
            KoppelEditor(null);
            Editor = null;
            return;
        }

        var actieveRekeningen = _stand.Rekeningen.OrderBy(r => r.Nummer).ToList();
        // Posten in een bankdagboek ontstaan alleen uit het afschrift (subtabblad Bank).
        var dagboeken = _stand.Dagboeken
            .Where(d => d.Soort != DagboekSoort.Bank || (post is not null && d.Id == post.Dagboek.Id))
            .OrderBy(d => d.Soort).ThenBy(d => d.Code, StringComparer.Ordinal).ToList();
        var editor = new PostEditor(post, dagboeken, actieveRekeningen, Vandaag);
        KoppelEditor(editor);
        Editor = editor;
    }

    private void KoppelEditor(PostEditor? editor)
    {
        if (Editor is not null)
        {
            Editor.PropertyChanged -= EditorGewijzigd;
        }

        if (editor is not null)
        {
            editor.PropertyChanged += EditorGewijzigd;
        }
    }

    private void EditorGewijzigd(object? sender, PropertyChangedEventArgs e) => CommandsVernieuwen();

    partial void OnEditorChanged(PostEditor? value)
    {
        LaadPostbijlagen();
        CommandsVernieuwen();
    }

    partial void OnZoektekstChanged(string value) => PasPostfilterToe(GeselecteerdePost?.Id);

    partial void OnJaarChanged(int value)
    {
        if (_stand is null)
        {
            return;
        }

        PasPostfilterToe(GeselecteerdePost?.Id);
        StelKaartperiodeIn();
        BerekenRapport();
        BerekenKaart();
    }

    // ---- Rekeningschema ---------------------------------------------------------------------

    [RelayCommand]
    private void NieuweRekening()
    {
        if (!IsIngericht)
        {
            return;
        }

        var regel = new RekeningRegel(null, null, 0);
        regel.PropertyChanged += RekeningGewijzigd;
        Rekeningen.Add(regel);
        GeselecteerdeRekening = regel;
        Subtab = BoekhoudingSubtab.Rekeningschema;
        CommandsVernieuwen();
    }

    [RelayCommand]
    private Task NieuweRubriekAsync()
    {
        var naam = NieuweRubriekNaam.Trim();
        if (naam.Length == 0)
        {
            return Task.CompletedTask;
        }

        return VoerUitAsync(async () =>
        {
            await _dienst.BewaarRubriekAsync(Soort, new RubriekInvoer(null, naam, Rubrieken.Select(r => r.Volgorde).DefaultIfEmpty(0).Max() + 10));
            NieuweRubriekNaam = string.Empty;
            IsRubriekinvoerOpen = false;
            await LaadAsync(Editor?.Id);
        }, null);
    }

    private void KoppelRekeningen(bool koppel)
    {
        foreach (var regel in Rekeningen)
        {
            regel.PropertyChanged -= RekeningGewijzigd;
            if (koppel)
            {
                regel.PropertyChanged += RekeningGewijzigd;
            }
        }
    }

    private void RekeningGewijzigd(object? sender, PropertyChangedEventArgs e) => CommandsVernieuwen();

    // ---- Grootboekkaart en rapporten --------------------------------------------------------

    private void StelKaartperiodeIn()
    {
        KaartVan = new DateTime(Jaar, 1, 1);
        KaartTot = new DateTime(Jaar, 12, 31);
    }

    partial void OnKaartRekeningChanged(Grootboekrekening? value) => BerekenKaart();

    partial void OnKaartVanChanged(DateTime? value) => BerekenKaart();

    partial void OnKaartTotChanged(DateTime? value) => BerekenKaart();

    partial void OnRapportsoortChanged(Rapportsoort value) => BerekenRapport();

    partial void OnRekeningenTonenChanged(bool value) => BerekenRapport();

    partial void OnSubtabChanged(BoekhoudingSubtab value)
    {
        OnPropertyChanged(nameof(HeeftUitvoer));
        OnPropertyChanged(nameof(Afdruktabel));
        OnPropertyChanged(nameof(Onderdeel));
    }

    /// <summary>Het onderdeel in de navigatiebalk.</summary>
    public override int? Onderdeel
    {
        get => (int)Subtab;
        // Een onbekend nummer (bijvoorbeeld van een vervallen onderdeel, bewaard als laatste pagina) opent het Journaal.
        set => Subtab = value is { } i && Enum.IsDefined((BoekhoudingSubtab)i) ? (BoekhoudingSubtab)i : BoekhoudingSubtab.Journaal;
    }

    private void BerekenKaart()
    {
        KaartMutaties.Clear();
        ZetKaartkoppen();
        if (_stand is null || KaartRekening is null || KaartVan is null || KaartTot is null || KaartTot < KaartVan)
        {
            KaartBeginsaldo = KaartEindsaldo = string.Empty;
            KaartTabel = null;
            OnPropertyChanged(nameof(Afdruktabel));
            return;
        }

        var kaart = Grootboek.Kaart(_stand, KaartRekening, Jaar, DateOnly.FromDateTime(KaartVan.Value), DateOnly.FromDateTime(KaartTot.Value));
        foreach (var mutatie in kaart.Mutaties)
        {
            KaartMutaties.Add(new KaartRegel(mutatie));
        }

        KaartBeginsaldo = new Bedrag(kaart.Beginsaldo).ToString();
        KaartEindsaldo = new Bedrag(kaart.Eindsaldo).ToString();
        KaartTabel = Rapporten.Kaart(kaart, Administratienaam);
        OnPropertyChanged(nameof(Afdruktabel));
    }

    private void ZetKaartkoppen()
    {
        var rekening = KaartRekening;
        var geldpotje = rekening is not null && (rekening.Rol == RekeningRol.Kas || _stand?.Dagboeken.Any(d =>
            d.Soort is DagboekSoort.Bank or DagboekSoort.Kas && d.Rekening is { } r && (ReferenceEquals(r, rekening) || (r.Id != 0 && r.Id == rekening.Id))) == true);
        (KaartDebetkop, KaartCreditkop) = rekening is null ? (Woorden["Debet"], Woorden["Credit"]) : Woorden.Kolomkoppen(rekening, geldpotje);
        KaartKoptip = KaartDebetkop == Woorden["Debet"] ? Woorden.Uitleg("Debet") : null;
    }

    private void WoordenGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Titel));
        OnPropertyChanged(nameof(Paginatitel));
        OnPropertyChanged(nameof(Rapportkeuzes));
        if (_stand is not null)
        {
            _allePosten = MaakPostregels();
            PasPostfilterToe(GeselecteerdePost?.Id);
        }

        ZetKaartkoppen();
    }

    private void BerekenRapport()
    {
        using var meting = Meetpunt.Start(_logger, "Overzicht maken " + Rapportsoort);
        JaarrekeningMelding = null;
        var concepten = _stand?.Posten.Count(p => !p.IsGeboekt && p.Datum.Year == Jaar) ?? 0;
        Conceptmelding = concepten == 0 ? null
            : $"⚠ {concepten} {(concepten == 1 ? "conceptpost telt" : "conceptposten tellen")} niet mee. Boek ze in het Journaal om ze in de rapporten te zien.";
        Rapport = _stand is null || !IsIngericht ? null : Rapportsoort switch
        {
            Rapportsoort.JaarrekeningBalans => Jaarrekeningtabel(_stand, JaarrekeningRapport.Balans),
            Rapportsoort.JaarrekeningBatenLasten => Jaarrekeningtabel(_stand, JaarrekeningRapport.BatenEnLasten),
            Rapportsoort.Balans => Rapporten.Balans(Grootboek.Balans(_stand, Jaar), Administratienaam),
            Rapportsoort.WinstEnVerlies => Rapporten.WinstEnVerlies(Grootboek.WinstEnVerlies(_stand, Jaar), Administratienaam),
            _ => Rapporten.Saldibalans(Grootboek.Saldibalans(_stand, Jaar), Administratienaam),
        };
        OnPropertyChanged(nameof(Afdruktabel));
    }

    private RapportTabel Jaarrekeningtabel(BoekhoudingStand stand, JaarrekeningRapport rapport)
    {
        var jaarrekening = Jaarrekeningrekenaar.Bereken(stand, rapport, Jaar);
        if (jaarrekening.NietIngedeeld.Count > 0)
        {
            var aantal = jaarrekening.NietIngedeeld.Count;
            JaarrekeningMelding = $"⚠ {aantal} {(aantal == 1 ? "rekening valt" : "rekeningen vallen")} in geen regel van de indeling: "
                + string.Join(", ", jaarrekening.NietIngedeeld.Select(r => r.Nummer.ToString(CultureInfo.InvariantCulture)))
                + ". Ze staan onder \"Niet ingedeeld\" en tellen mee in de totalen. Kies Indeling bewerken… om ze een plaats te geven.";
        }

        return Rapporten.Jaarrekening(jaarrekening, Administratienaam, RekeningenTonen);
    }

    /// <summary>Venster <i>Indeling bewerken</i> voor het gekozen jaarstuk.</summary>
    [RelayCommand]
    private async Task IndelingBewerkenAsync()
    {
        if (_stand is not { IsIngericht: true } stand || !IsJaarrekening)
        {
            return;
        }

        var rapport = Rapportsoort == Rapportsoort.JaarrekeningBalans ? JaarrekeningRapport.Balans : JaarrekeningRapport.BatenEnLasten;
        var editor = new JaarrekeningIndelingViewModel(new JaarrekeningDienst(_sessie), stand, rapport);
        if (!_dialogen.ToonJaarrekeningIndeling(editor) || !editor.IsOpgeslagen)
        {
            return;
        }

        await VoerUitAsync(() => LaadAsync(null), "De boekhouding kon niet worden geladen.");
        Melding = "✓ De indeling is opgeslagen. De jaarrekening hieronder gebruikt hem al; afdrukken kan met Ctrl+P.";
    }

    /// <summary>Ctrl+P, via het standaard Windows-afdrukvenster (ook naar PDF).</summary>
    [RelayCommand]
    private Task AfdrukkenAsync()
    {
        if (Afdruktabel is { } tabel)
        {
            _hulpmiddelen.Afdruk.Druk(tabel);
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ExporteerExcelAsync()
    {
        if (Afdruktabel is not { } tabel || _dialogen.KiesXlsxDoel(Bestandsnaam(tabel) + ".xlsx") is not { } doel)
        {
            return Task.CompletedTask;
        }

        return VoerUitAsync(() =>
        {
            _hulpmiddelen.Schrijver.Schrijf(tabel, doel);
            Melding = $"Opgeslagen als {Path.GetFileName(doel)}.";
            return Task.CompletedTask;
        }, null);
    }

    [RelayCommand]
    private Task ExporteerCsvAsync()
    {
        if (Afdruktabel is not { } tabel || _dialogen.KiesCsvDoel(Bestandsnaam(tabel) + ".csv") is not { } doel)
        {
            return Task.CompletedTask;
        }

        return VoerUitAsync(async () =>
        {
            await File.WriteAllTextAsync(doel, CsvExport.Schrijf(tabel));
            Melding = $"Opgeslagen als {Path.GetFileName(doel)}.";
        }, null);
    }

    private string Bestandsnaam(RapportTabel tabel)
    {
        var naam = $"{tabel.Titel} {Administratienaam}".ToLowerInvariant();
        foreach (var teken in Path.GetInvalidFileNameChars().Append(' '))
        {
            naam = naam.Replace(teken, '-');
        }

        return naam;
    }

    // ---- Boekjaren --------------------------------------------------------------------------

    [RelayCommand]
    private Task OpenVolgendBoekjaarAsync()
    {
        var jaar = Boekjaren.Count == 0 ? Vandaag.Year : Boekjaren[^1] + 1;
        return VoerUitAsync(async () =>
        {
            await _dienst.OpenBoekjaarAsync(Soort, jaar);
            await LaadAsync(Editor?.Id);
        }, null);
    }

    private async Task SluitBoekjaarAsync(JaarRegel? regel)
    {
        if (regel is not { IsOpen: true }
            || !_dialogen.Bevestig("Boekjaar afsluiten",
                $"Wilt u boekjaar {regel.Jaartekst} afsluiten? Het resultaat wordt naar de reserve geboekt, het jaar wordt vergrendeld en "
                + "de openingsbalans van het volgende jaar wordt gemaakt. Vooraf wordt een back-up gemaakt."))
        {
            return;
        }

        await VoerUitAsync(async () =>
        {
            var resultaat = await _dienst.SluitBoekjaarAsync(Soort, regel.Jaar);
            ShellLog.BoekjaarGewijzigd(_logger, regel.Jaar, Soort.ToString(), "afgesloten");
            Melding = $"Boekjaar {regel.Jaartekst} is afgesloten met een {(resultaat.Centen >= 0 ? "overschot" : "tekort")} van {new Bedrag(Math.Abs(resultaat.Centen))}.";
            await LaadAsync(null);
        }, null);
    }

    private async Task HeropenBoekjaarAsync(JaarRegel? regel)
    {
        if (regel is not { IsAfgesloten: true }
            || !_dialogen.Bevestig("Boekjaar heropenen",
                $"Wilt u boekjaar {regel.Jaartekst} heropenen? De afsluitpost en de openingsbalans van het volgende jaar worden verwijderd."))
        {
            return;
        }

        await VoerUitAsync(async () =>
        {
            await _dienst.HeropenBoekjaarAsync(Soort, regel.Jaar);
            ShellLog.BoekjaarGewijzigd(_logger, regel.Jaar, Soort.ToString(), "heropend");
            Melding = $"Boekjaar {regel.Jaartekst} is heropend.";
            await LaadAsync(null);
        }, null);
    }

    // ---- Hulp -------------------------------------------------------------------------------

    /// <param name="standaardfout">Melding bij een onverwachte fout; <c>null</c> toont de fout zelf.</param>
    private async Task VoerUitAsync(Func<Task> actie, string? standaardfout)
    {
        IsBezig = true;
        Foutmelding = null;
        CommandsVernieuwen();
        try
        {
            await actie();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Foutmelding = standaardfout ?? ex.Message;
            ShellLog.Mislukt(_logger, ex, "Boekhouding");
        }
        finally
        {
            IsBezig = false;
            CommandsVernieuwen();
        }
    }

    private void CommandsVernieuwen()
    {
        OnPropertyChanged(nameof(HeeftOnopgeslagenWijzigingen));
        OnPropertyChanged(nameof(KanOpslaan));
        OnPropertyChanged(nameof(HeeftOnopgeslagenNieuweRekeningen));
        OpslaanCommand.NotifyCanExecuteChanged();
        BoekenCommand.NotifyCanExecuteChanged();
        VerwijderConceptCommand.NotifyCanExecuteChanged();
        TegenboekenCommand.NotifyCanExecuteChanged();
        SluitBoekjaarCommand.NotifyCanExecuteChanged();
        HeropenBoekjaarCommand.NotifyCanExecuteChanged();
        BankCommandsVernieuwen();
        BijlageCommandsVernieuwen();
    }
}
