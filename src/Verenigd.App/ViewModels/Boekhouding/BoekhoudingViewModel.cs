using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Verenigd.App.Diensten;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Application.Modules;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.App.ViewModels.Boekhouding;

/// <summary>
/// Tabblad Boekhouding van één administratie (stap 4a) met de subtabbladen Journaal, Rekeningschema, Grootboekkaart,
/// Rapporten en Boekjaren. Geboekte posten zijn onveranderlijk; corrigeren gaat via tegenboeken (besluit 71).
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

    /// <summary>De woorden van de gekozen weergave (besluit 127); alleen visueel.</summary>
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

    public ObservableCollection<PostRegel> Posten { get; } = [];

    public ObservableCollection<RekeningRegel> Rekeningen { get; } = [];

    public ObservableCollection<KaartRegel> KaartMutaties { get; } = [];

    public ObservableCollection<JaarRegel> Jaren { get; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Rubriek> Rubrieken { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<Grootboekrekening> Rekeningkeuzes { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<int> Boekjaren { get; private set; } = [];

    public IReadOnlyList<RekeningSoort> Soorten { get; } = Enum.GetValues<RekeningSoort>();

    public IReadOnlyList<RekeningRol> Rollen { get; } = Enum.GetValues<RekeningRol>();

    public IReadOnlyList<Rapportkeuze> Rapportkeuzes =>
        [new(Rapportsoort.Saldibalans, Woorden["Saldibalans"]), new(Rapportsoort.Balans, Woorden["Balans"]), new(Rapportsoort.WinstEnVerlies, Woorden["WinstEnVerlies"]),
            new(Rapportsoort.Begroting, Woorden["Rapport.Begroting"])];

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

    [ObservableProperty]
    public partial Rapportsoort Rapportsoort { get; set; }

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

    /// <summary>Bij een historisch jaar: het saldo volgens het Voorblad, met een ⚠ als het afwijkt (besluit 126).</summary>
    [ObservableProperty]
    public partial string? KaartVoorbladtekst { get; private set; }

    /// <summary>Kolomkoppen van de kaart per soort potje (besluit 133).</summary>
    [ObservableProperty]
    public partial string KaartDebetkop { get; private set; } = "Debet";

    [ObservableProperty]
    public partial string KaartCreditkop { get; private set; } = "Credit";

    /// <summary>Uitleg bij debet en credit als tooltip, alleen in Eenvoudig.</summary>
    [ObservableProperty]
    public partial string? KaartKoptip { get; private set; }

    [ObservableProperty]
    public partial bool KaartWijktAf { get; private set; }

    /// <summary>Het gekozen boekjaar is historisch: alleen-lezen, overgenomen uit Excel (besluit 122).</summary>
    public bool IsHistorischJaar => _stand?.IsHistorisch(Jaar) == true;

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    [ObservableProperty]
    public partial string? Melding { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KanOpslaan))]
    public partial bool IsBezig { get; private set; }

    public string Aantaltekst => string.Create(CultureInfo.CurrentCulture,
        $"{Posten.Count} {(Posten.Count == 1 ? "post" : "posten")} · {Posten.Count(p => p.IsConcept)} concept");

    public override bool HeeftOnopgeslagenWijzigingen =>
        Editor?.IsGewijzigd == true || Rekeningen.Any(r => r.IsGewijzigd) || Boekingsregels.Any(r => r.IsGewijzigd) || FacturenGewijzigd || BegrotingGewijzigd || MeerjarenplanGewijzigd;

    public override bool KanOpslaan =>
        HeeftOnopgeslagenWijzigingen && !IsBezig
        && (Editor is not { IsGewijzigd: true } || Editor.Fouten.Count == 0)
        && Rekeningen.Where(r => r.IsGewijzigd).All(r => r.Fout is null)
        && Boekingsregels.Where(r => r.IsGewijzigd).All(r => r.Fout is null)
        && FacturenGeldig
        && BegrotingGeldig
        && MeerjarenplanGeldig;

    /// <summary>Wat Ctrl+P afdrukt en de export wegschrijft: de grootboekkaart of het gekozen rapport.</summary>
    public RapportTabel? Afdruktabel => Subtab switch
    {
        BoekhoudingSubtab.Grootboekkaart when KaartTabel is not null => KaartTabel,
        // De begroting voor de ALV (besluit 155), altijd in Expert-termen (besluit 131).
        BoekhoudingSubtab.Begroting when _stand is not null && HeeftBegroting => Rapporten.BegrotingAlv(_stand, Begrotingsjaar, Administratienaam),
        BoekhoudingSubtab.Meerjarenplan when HeeftMeerjarenplan => MeerjarenAfdruk(),
        _ => Rapport,
    };

    private DateOnly Vandaag => DateOnly.FromDateTime(_tijd.GetLocalNow().DateTime);

    private Domain.Administraties.AdministratieSoort Soort => Administratie!.Soort;

    private string Administratienaam => Administratie!.Naam;

    /// <summary>Ctrl+S: het concept en gewijzigde rekeningen opslaan.</summary>
    public override async Task<bool> BewaarAsync()
    {
        if (!KanOpslaan)
        {
            return !HeeftOnopgeslagenWijzigingen;
        }

        IsBezig = true;
        try
        {
            foreach (var rekening in Rekeningen.Where(r => r.IsGewijzigd).ToList())
            {
                await _dienst.BewaarRekeningAsync(Soort, rekening.Invoer);
            }

            await BewaarRegelsAsync();
            await BewaarBegrotingAsync();
            await BewaarMeerjarenplanAsync();
            var factuur = await BewaarFacturenAsync();
            Guid? selectie = Editor?.Id;
            if (Editor is { IsGewijzigd: true } editor)
            {
                selectie = await _dienst.BewaarConceptAsync(Soort, editor.Invoer);
            }

            Foutmelding = null;
            await LaadAsync(selectie);
            if (factuur is not null)
            {
                PasFactuurfilterToe(factuur);
            }

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Foutmelding = ex.Message;
            return false;
        }
        finally
        {
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
        Rekeningen.Clear();
        foreach (var rekening in Rekeningkeuzes)
        {
            Rekeningen.Add(new RekeningRegel(rekening, Rubrieken.FirstOrDefault(r => r.Id == rekening.Rubriek?.Id), Grootboek.Saldo(_stand, rekening, Jaar)));
        }

        KoppelRekeningen(true);

        Jaren.Clear();
        foreach (var boekjaar in _stand.Boekjaren.OrderByDescending(b => b.Jaar))
        {
            var posten = _stand.Posten.Where(p => p.Datum.Year == boekjaar.Jaar).ToList();
            Jaren.Add(new JaarRegel(
                boekjaar.Jaar, boekjaar.IsOpen, boekjaar.IsHistorisch ? "Historisch" : boekjaar.IsOpen ? "Open" : "Afgesloten",
                posten.Count(p => !p.IsGeboekt), posten.Count(p => p.IsGeboekt), boekjaar.IsHistorisch));
        }

        _allePosten = MaakPostregels();
        BerekenZonderBon();
        OnPropertyChanged(nameof(IsHistorischJaar));
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
        LaadFacturen(null);
        LaadBijlagen();
        LaadBegroting(GekozenBegrotingsversie?.Id);
        LaadMeerjarenplan(GekozenMeerjarenversie?.Id);
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
            Posten.Clear();
            foreach (var post in zichtbaar)
            {
                Posten.Add(post);
            }

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
    }

    // ---- Inrichten --------------------------------------------------------------------------

    [RelayCommand]
    private Task LeegBeginnenAsync() =>
        VoerUitAsync(async () =>
        {
            await _dienst.RichtInAsync(Soort);
            await LaadAsync(null);
        }, "De boekhouding kon niet worden ingericht.");

    /// <summary>Bestand › Importeren › Boekhouding uit Excel (besluit 62).</summary>
    [RelayCommand]
    private async Task OvernemenUitExcelAsync()
    {
        if (IsIngericht)
        {
            _dialogen.ToonInformatie("Boekhouding uit Excel", $"De boekhouding van {Administratienaam} is al ingericht; de overname uit Excel kan maar één keer.");
            return;
        }

        if (_dialogen.KiesExcelBestand() is not { } pad)
        {
            return;
        }

        var wizard = new BoekhoudingImportViewModel(_dienst, _hulpmiddelen.Lezer, Soort, pad);
        _dialogen.ToonBoekhoudingImport(wizard);
        if (!wizard.IsKlaar)
        {
            return;
        }

        ShellLog.BoekhoudingOvergenomen(_logger, Soort.ToString(), wizard.AantalOvergenomen);
        if (wizard.Verschilrekening is { } rekening)
        {
            ShellLog.BoekhoudingVerschilGeboekt(_logger, Soort.ToString(), wizard.Verschiltekst, rekening);
        }

        if (wizard.OokHistorie)
        {
            ToonHistorie(pad);
        }

        await VoerUitAsync(() => LaadAsync(null), "De boekhouding kon niet worden geladen.");
    }

    /// <summary>Bestand › Importeren › Historie uit Excel: het historische boekjaar (besluiten 122–125).</summary>
    [RelayCommand]
    private async Task HistorieInlezenAsync()
    {
        if (!IsIngericht)
        {
            _dialogen.ToonInformatie("Historie uit Excel", "Neem eerst het rekeningschema over (Boekhouding uit Excel); daarna kan de historie worden toegevoegd.");
            return;
        }

        var historisch = _stand?.Boekjaren.FirstOrDefault(b => b.IsHistorisch);
        if (historisch is not null)
        {
            _dialogen.ToonInformatie("Historie uit Excel", $"Het historische boekjaar {historisch.Jaar} bestaat al; de historie kan maar één keer worden overgenomen.");
            return;
        }

        if (_dialogen.KiesExcelBestand() is not { } pad || !ToonHistorie(pad))
        {
            return;
        }

        await VoerUitAsync(() => LaadAsync(null), "De boekhouding kon niet worden geladen.");
        Melding = "De historie is overgenomen als alleen-lezen boekjaar.";
    }

    private bool ToonHistorie(string pad)
    {
        var wizard = new HistorieImportViewModel(new HistorieDienst(_sessie, _tijd), _hulpmiddelen.Lezer, Soort, pad);
        return _dialogen.ToonHistorieImport(wizard) && wizard.IsKlaar;
    }

    /// <summary>Inrichten met het ingebouwde standaardschema van deze administratie (besluiten 114 en 118).</summary>
    [RelayCommand]
    private Task StandaardInrichtenAsync() => RichtInMetSjabloonAsync(() => Rekeningsjablonen.Standaard(Soort));

    /// <summary>Bestand › Importeren › Rekeningschema-sjabloon (besluit 116).</summary>
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

        StandaardinrichtingViewModel wizard;
        try
        {
            wizard = new StandaardinrichtingViewModel(_dienst, Soort, sjabloon(), Vandaag.Year);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Foutmelding = ex is InvalidOperationException ? ex.Message : "Het sjabloonbestand kan niet worden gelezen.";
            return;
        }

        Foutmelding = null;
        if (!_dialogen.ToonStandaardinrichting(wizard) || !wizard.IsKlaar)
        {
            return;
        }

        ShellLog.BoekhoudingOvergenomen(_logger, Soort.ToString(), wizard.AantalIngericht);
        await VoerUitAsync(() => LaadAsync(null), "De boekhouding kon niet worden geladen.");
    }

    /// <summary>Bestand › Exporteren › Rekeningschema als sjabloon: alleen nummers, namen, rubrieken en rollen (besluit 116).</summary>
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

    // ---- Journaal ---------------------------------------------------------------------------

    /// <summary>Ctrl+N binnen het tabblad (besluit 72).</summary>
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
                $"Wilt u post {post.Nummer} tegenboeken op {Contributie.Weergave.Datum(datum)}? Er wordt een geboekte post met omgekeerde bedragen gemaakt."))
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
        OnPropertyChanged(nameof(IsHistorischJaar));
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

    partial void OnSubtabChanged(BoekhoudingSubtab value)
    {
        OnPropertyChanged(nameof(Afdruktabel));
        OnPropertyChanged(nameof(Onderdeel));
        OnPropertyChanged(nameof(IsOpslaanHoofdactie));
    }

    /// <summary>Besluit 168: op de begroting en het meerjarenplan is opslaan de hoofdactie (blauwe knop); elders een gewone knop.</summary>
    public bool IsOpslaanHoofdactie =>
        (Subtab == BoekhoudingSubtab.Begroting && HeeftBegroting) || (Subtab == BoekhoudingSubtab.Meerjarenplan && HeeftMeerjarenplan);

    /// <summary>Het onderdeel in de navigatiebalk (besluit 178).</summary>
    public override int? Onderdeel
    {
        get => (int)Subtab;
        set => Subtab = value is { } i ? (BoekhoudingSubtab)i : BoekhoudingSubtab.Journaal;
    }

    private void BerekenKaart()
    {
        KaartMutaties.Clear();
        KaartVoorbladtekst = null;
        ZetKaartkoppen();
        KaartWijktAf = false;
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
        if (kaart.IsHistorisch)
        {
            KaartWijktAf = kaart.TotaalVolgensVoorblad is { } totaal && totaal != kaart.Eindsaldo;
            KaartVoorbladtekst = kaart.TotaalVolgensVoorblad is { } voorblad
                ? $"Totaal volgens Voorblad: {new Bedrag(voorblad)}" + (KaartWijktAf ? " ⚠ wijkt af van de regels; zoek het uit in Excel." : " ✓")
                : "Deze rekening staat niet op het Voorblad.";
        }
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
        OnPropertyChanged(nameof(Rapportkeuzes));
        if (_stand is not null)
        {
            _allePosten = MaakPostregels();
            PasPostfilterToe(GeselecteerdePost?.Id);
        }

        ZetKaartkoppen();
        if (_stand is not null)
        {
            BerekenBegrotingstotalen();
        }
    }

    private void BerekenRapport()
    {
        Rapport = _stand is null || !IsIngericht ? null : Rapportsoort switch
        {
            Rapportsoort.Balans => Rapporten.Balans(Grootboek.Balans(_stand, Jaar), Administratienaam),
            Rapportsoort.WinstEnVerlies => Rapporten.WinstEnVerlies(Grootboek.WinstEnVerlies(_stand, Jaar), Administratienaam),
            Rapportsoort.Begroting => Rapporten.Begroting(Application.Begrotingen.Begrotingsrekenaar.Overzicht(_stand, Jaar, Vandaag), Administratienaam),
            _ => Rapporten.Saldibalans(Grootboek.Saldibalans(_stand, Jaar), Administratienaam),
        };
        OnPropertyChanged(nameof(Afdruktabel));
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
        OpslaanCommand.NotifyCanExecuteChanged();
        BoekenCommand.NotifyCanExecuteChanged();
        VerwijderConceptCommand.NotifyCanExecuteChanged();
        TegenboekenCommand.NotifyCanExecuteChanged();
        SluitBoekjaarCommand.NotifyCanExecuteChanged();
        HeropenBoekjaarCommand.NotifyCanExecuteChanged();
        BankCommandsVernieuwen();
        FactuurCommandsVernieuwen();
        BijlageCommandsVernieuwen();
        BegrotingCommandsVernieuwen();
        MeerjarenCommandsVernieuwen();
    }
}
