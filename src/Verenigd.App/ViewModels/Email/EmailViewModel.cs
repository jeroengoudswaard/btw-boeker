using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.App.Diensten;
using Verenigd.Application.Database;
using Verenigd.Application.Email;
using Verenigd.Application.Modules;
using Verenigd.Domain.Communicatie;
using Verenigd.Domain.Leden;

namespace Verenigd.App.ViewModels.Email;

/// <summary>De onderdelen van E-mail in de navigatiebalk.</summary>
public enum EmailSubtab
{
    Opstellen,
    Verzonden,
    Sjablonen,
}

/// <summary>Een bericht dat een andere pagina voorbereidt (besluiten 205–207): het soort, de ontvangers en waarom.</summary>
public sealed record EmailVoorstel(Emailsoort Soort, IReadOnlyList<Emailontvanger> Ontvangers, string Aanleiding);

public sealed record OntvangerkeuzeOptie(Ontvangerkeuze Keuze, string Tekst);

/// <summary>Een regel in Verzonden (besluit 195): aan wie, wanneer, onderwerp en sjabloon; niet de tekst.</summary>
public sealed record VerzondenRegel(VerzondenBericht Bericht)
{
    public string Tijdstip => DateTime.SpecifyKind(Bericht.TijdstipUtc, DateTimeKind.Utc).ToLocalTime().ToString("d-M-yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    public string Status => Bericht.Status == Verzendstatus.Verzonden ? "✓ Verzonden" : "✕ Mislukt";
}

/// <summary>Een groep (speltak, bestuur of overig) om leden uit te kiezen; technische groepen staan er niet bij.</summary>
public sealed partial class GroepKeuze(string naam, int aantal) : ObservableObject
{
    public string Naam { get; } = naam;

    public int Aantal { get; } = aantal;

    public string Tekst => $"{Naam} ({Aantal})";

    [ObservableProperty]
    public partial bool IsGekozen { get; set; }
}

/// <summary>
/// Pagina E-mail (fase 6, besluiten 191–208): een bericht opstellen voor groepen of voor een voorbereid bericht uit een
/// andere pagina, het logboek van verzonden berichten (zonder tekst, besluit 195) en het beheer van sjablonen.
/// </summary>
public sealed partial class EmailViewModel : TabbladViewModel, IPaginameldingen
{
    private readonly IDatabaseSessie _sessie;
    private readonly IDialoogService _dialogen;
    private readonly TimeProvider _tijd;
    private readonly Func<string> _vereniging;
    private readonly EmailDienst _dienst;
    private IReadOnlyList<Persoon> _leden = [];
    private EmailVoorstel? _voorstel;
    private int _verzondenVandaag;

    public EmailViewModel(IDatabaseSessie sessie, IDialoogService dialogen, TimeProvider tijd, BoekhoudingHulpmiddelen hulpmiddelen, Func<string> vereniging)
        : base(ModuleCatalogus.Geef(ModuleSoort.Email), administratie: null)
    {
        ArgumentNullException.ThrowIfNull(hulpmiddelen);
        _sessie = sessie;
        _dialogen = dialogen;
        _tijd = tijd;
        _vereniging = vereniging;
        _dienst = new EmailDienst(sessie, hulpmiddelen.Email, tijd);
        VernieuwCommand = new AsyncRelayCommand(VernieuwAsync);
        OpslaanCommand = new AsyncRelayCommand(BewaarAsync, () => KanOpslaan);
        GekozenOntvangerkeuze = Ontvangerkeuzes[0];
        Mislukt.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HeeftMislukt));
    }

    public IAsyncRelayCommand VernieuwCommand { get; }

    public IAsyncRelayCommand OpslaanCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Onderdeel))]
    public partial EmailSubtab Subtab { get; set; }

    public override int? Onderdeel
    {
        get => (int)Subtab;
        set => Subtab = value is { } i ? (EmailSubtab)i : EmailSubtab.Opstellen;
    }

    public ObservableCollection<Emailsjabloon> Sjablonen { get; } = [];

    public ObservableCollection<VerzondenRegel> Logboek { get; } = [];

    public ObservableCollection<GroepKeuze> Groepen { get; } = [];

    public ObservableCollection<Emailontvanger> Ontvangers { get; } = [];

    public ObservableCollection<Mislukking> Mislukt { get; } = [];

    /// <summary>Besluit 203: per bericht te kiezen; standaard voogden bij jeugdleden, anders het lid zelf.</summary>
    public static IReadOnlyList<OntvangerkeuzeOptie> Ontvangerkeuzes { get; } =
    [
        new(Ontvangerkeuze.Standaard, "Standaard (voogden van jeugdleden, anders het lid)"),
        new(Ontvangerkeuze.AlleenVoogden, "Alleen voogden"),
        new(Ontvangerkeuze.AlleenLeden, "Alleen de leden zelf"),
        new(Ontvangerkeuze.VoogdenEnLeden, "Voogden én leden"),
    ];

    [ObservableProperty]
    public partial OntvangerkeuzeOptie GekozenOntvangerkeuze { get; set; }

    [ObservableProperty]
    public partial Emailsjabloon? GekozenSjabloon { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VoorbeeldOnderwerp), nameof(Waarschuwing))]
    [NotifyCanExecuteChangedFor(nameof(VerstuurCommand), nameof(ProefCommand))]
    public partial string Onderwerp { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VoorbeeldTekst), nameof(Waarschuwing))]
    [NotifyCanExecuteChangedFor(nameof(VerstuurCommand), nameof(ProefCommand))]
    public partial string Tekst { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VoorbeeldOnderwerp), nameof(VoorbeeldTekst), nameof(VoorbeeldBijlagen), nameof(Waarschuwing))]
    public partial Emailontvanger? GekozenOntvanger { get; set; }

    [ObservableProperty]
    public partial string? ZonderAdres { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerstuurCommand), nameof(ProefCommand))]
    public partial bool IsGekoppeld { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KanOpslaan))]
    [NotifyCanExecuteChangedFor(nameof(VerstuurCommand), nameof(ProefCommand))]
    public partial bool IsBezig { get; private set; }

    [ObservableProperty]
    public partial string? Voortgang { get; private set; }

    [ObservableProperty]
    public partial string? Melding { get; private set; }

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    /// <summary>Het sjabloon dat op het onderdeel Sjablonen gekozen is.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerwijderSjabloonCommand))]
    public partial Emailsjabloon? Beheersjabloon { get; set; }

    [ObservableProperty]
    public partial SjabloonBewerking? Bewerking { get; private set; }

    public bool IsVoorbereid => _voorstel is not null;

    public bool HeeftMislukt => Mislukt.Count > 0;

    public string? Aanleiding => _voorstel?.Aanleiding;

    public string Aantaltekst => Ontvangers.Count == 1 ? "1 ontvanger" : $"{Ontvangers.Count} ontvangers";

    public string Daglimiettekst => $"Vandaag verstuurd: {_verzondenVandaag} van {EmailDienst.Daglimiet}";

    public string VoorbeeldOnderwerp => GekozenOntvanger is { } o ? Sjablooninvuller.Vul(Onderwerp, o.Velden) : Onderwerp;

    public string VoorbeeldTekst => GekozenOntvanger is { } o ? Sjablooninvuller.Vul(Tekst, o.Velden) : Tekst;

    public string? VoorbeeldBijlagen => GekozenOntvanger is { Bijlagen.Count: > 0 } o ? string.Join(", ", o.Bijlagen.Select(b => b.Bestandsnaam)) : null;

    /// <summary>Invulvelden die niet bestaan blijven letterlijk in de e-mail staan; dat meldt de pagina vooraf.</summary>
    public string? Waarschuwing
    {
        get
        {
            var bekend = Invulvelden.Voor(Soort).Select(v => v.Naam).Concat(GekozenOntvanger?.Velden.Keys ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var onbekend = Sjablooninvuller.OnbekendeVelden(Onderwerp + "\n" + Tekst, bekend);
            return onbekend.Count == 0
                ? null
                : $"Onbekend invulveld: {string.Join(", ", onbekend.Select(v => "{" + v + "}"))}. Dit blijft letterlijk in de e-mail staan.";
        }
    }

    public override bool HeeftOnopgeslagenWijzigingen => Bewerking?.IsGewijzigd == true;

    public override bool KanOpslaan => HeeftOnopgeslagenWijzigingen && Bewerking!.Fout is null && !IsBezig;

    private Emailsoort Soort => GekozenSjabloon?.Soort ?? _voorstel?.Soort ?? Emailsoort.Vrij;

    private DateOnly Vandaag => DateOnly.FromDateTime(_tijd.GetLocalNow().DateTime);

    private async Task VernieuwAsync()
    {
        var stand = await _dienst.LaadAsync();
        _verzondenVandaag = _dienst.VerzondenVandaag(stand);
        IsGekoppeld = stand.Instellingen is { IsGekoppeld: true, Afzenderadres: not null };
        VulSjablonen(stand.Sjablonen);
        Logboek.Clear();
        foreach (var bericht in stand.Logboek.OrderByDescending(b => b.TijdstipUtc))
        {
            Logboek.Add(new VerzondenRegel(bericht));
        }

        _leden = await _sessie.Leden.HaalAlleAsync();
        VulGroepen();
        BepaalOntvangers();
        OnPropertyChanged(nameof(Daglimiettekst));
    }

    private void VulSjablonen(IReadOnlyList<Emailsjabloon> sjablonen)
    {
        var gekozen = GekozenSjabloon?.Id;
        var beheer = Beheersjabloon?.Id;
        Sjablonen.Clear();
        foreach (var sjabloon in sjablonen.OrderBy(s => s.Soort).ThenBy(s => s.Naam, StringComparer.CurrentCulture))
        {
            Sjablonen.Add(sjabloon);
        }

        // Opnieuw kiezen zonder onderwerp en tekst te overschrijven.
        _sjabloonStil = true;
        GekozenSjabloon = Sjablonen.FirstOrDefault(s => s.Id == gekozen);
        _sjabloonStil = false;
        if (beheer is not null && !HeeftOnopgeslagenWijzigingen)
        {
            Beheersjabloon = Sjablonen.FirstOrDefault(s => s.Id == beheer);
        }
    }

    private bool _sjabloonStil;

    private void VulGroepen()
    {
        var gekozen = Groepen.Where(g => g.IsGekozen).Select(g => g.Naam).ToHashSet(StringComparer.Ordinal);
        foreach (var groep in Groepen)
        {
            groep.PropertyChanged -= GroepGewijzigd;
        }

        Groepen.Clear();
        var vandaag = Vandaag;
        foreach (var groep in _leden
                     .Where(l => !l.IsUitgeschreven)
                     .SelectMany(l => l.Functies.Where(f => f.IsActief(vandaag) && f.Groep.Soort != GroepSoort.Technisch).Select(f => (f.Groep.Naam, l.Id)))
                     .Distinct()
                     .GroupBy(f => f.Naam, StringComparer.Ordinal)
                     .OrderBy(g => g.Key, StringComparer.CurrentCulture))
        {
            var keuze = new GroepKeuze(groep.Key, groep.Count()) { IsGekozen = gekozen.Contains(groep.Key) };
            keuze.PropertyChanged += GroepGewijzigd;
            Groepen.Add(keuze);
        }
    }

    private void GroepGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GroepKeuze.IsGekozen))
        {
            BepaalOntvangers();
        }
    }

    partial void OnGekozenOntvangerkeuzeChanged(OntvangerkeuzeOptie value) => BepaalOntvangers();

    partial void OnGekozenSjabloonChanged(Emailsjabloon? value)
    {
        if (value is not null && !_sjabloonStil)
        {
            Onderwerp = value.Onderwerp;
            Tekst = value.Tekst;
        }

        OnPropertyChanged(nameof(Waarschuwing));
    }

    partial void OnBeheersjabloonChanged(Emailsjabloon? value) =>
        Bewerking = value is null ? null : new SjabloonBewerking(value, value.Soort);

    partial void OnBewerkingChanged(SjabloonBewerking? oldValue, SjabloonBewerking? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= BewerkingGewijzigd;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += BewerkingGewijzigd;
        }

        BewerkingGewijzigd(this, new PropertyChangedEventArgs(null));
    }

    private void BewerkingGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HeeftOnopgeslagenWijzigingen));
        OnPropertyChanged(nameof(KanOpslaan));
        OpslaanCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Opent Opstellen met de ontvangers en het sjabloon van een bericht uit een andere pagina.</summary>
    public void BereidVoor(EmailVoorstel voorstel)
    {
        ArgumentNullException.ThrowIfNull(voorstel);
        _voorstel = voorstel;
        Subtab = EmailSubtab.Opstellen;
        Mislukt.Clear();
        Melding = null;
        Foutmelding = voorstel.Ontvangers.Count == 0 ? "Er is geen geldig e-mailadres gevonden voor dit bericht." : null;
        GekozenSjabloon = Sjablonen.FirstOrDefault(s => s.Soort == voorstel.Soort && s.IsStandaard) ?? Sjablonen.FirstOrDefault(s => s.Soort == voorstel.Soort);
        BepaalOntvangers();
        OnPropertyChanged(nameof(IsVoorbereid));
        OnPropertyChanged(nameof(Aanleiding));
    }

    /// <summary>Terug naar een eigen bericht aan groepen.</summary>
    [RelayCommand]
    private void WisVoorstel()
    {
        _voorstel = null;
        Foutmelding = null;
        BepaalOntvangers();
        OnPropertyChanged(nameof(IsVoorbereid));
        OnPropertyChanged(nameof(Aanleiding));
    }

    private void BepaalOntvangers()
    {
        IReadOnlyList<Emailontvanger> ontvangers;
        if (_voorstel is not null)
        {
            ontvangers = _voorstel.Ontvangers;
            ZonderAdres = null;
        }
        else
        {
            var gekozen = Groepen.Where(g => g.IsGekozen).Select(g => g.Naam).ToHashSet(StringComparer.Ordinal);
            var vandaag = Vandaag;
            var leden = _leden.Where(l => l.Functies.Any(f => f.IsActief(vandaag) && gekozen.Contains(f.Groep.Naam))).ToList();
            ontvangers = Application.Email.Ontvangers.VoorLeden(leden, GekozenOntvangerkeuze.Keuze, vandaag, _vereniging());
            var zonder = Application.Email.Ontvangers.ZonderAdres(leden, GekozenOntvangerkeuze.Keuze, vandaag);
            ZonderAdres = zonder.Count == 0 ? null : $"Geen geldig e-mailadres bij: {string.Join(", ", zonder.Select(l => l.VolledigeNaam))}.";
        }

        Ontvangers.Clear();
        foreach (var ontvanger in ontvangers)
        {
            Ontvangers.Add(ontvanger);
        }

        GekozenOntvanger = Ontvangers.FirstOrDefault();
        OnPropertyChanged(nameof(Aantaltekst));
        VerstuurCommand.NotifyCanExecuteChanged();
        ProefCommand.NotifyCanExecuteChanged();
    }

    private bool KanVersturen() =>
        IsGekoppeld && !IsBezig && Ontvangers.Count > 0 && !string.IsNullOrWhiteSpace(Onderwerp) && !string.IsNullOrWhiteSpace(Tekst);

    /// <summary>Besluit 194: elke ontvanger een eigen, ingevulde e-mail; na bevestiging.</summary>
    [RelayCommand(CanExecute = nameof(KanVersturen))]
    private async Task VerstuurAsync()
    {
        var aantal = Ontvangers.Count;
        var tekst = aantal == 1 ? "1 e-mail" : $"{aantal} e-mails";
        if (!_dialogen.Bevestig("E-mail versturen", $"{tekst} versturen met het onderwerp '{Onderwerp}'?\n\nElke ontvanger krijgt een eigen e-mail."))
        {
            return;
        }

        IsBezig = true;
        Melding = null;
        Foutmelding = null;
        Mislukt.Clear();
        try
        {
            var resultaat = await _dienst.VerstuurAsync([.. Ontvangers], Onderwerp, Tekst, GekozenSjabloon?.Naam ?? "Eigen tekst",
                new Melder(i => Voortgang = $"Versturen... {i} van {aantal}"));
            foreach (var mislukking in resultaat.Mislukt)
            {
                Mislukt.Add(mislukking);
            }

            Melding = resultaat.Verzonden == 1 ? "1 e-mail verstuurd." : $"{resultaat.Verzonden} e-mails verstuurd.";
            Foutmelding = (resultaat.Mislukt.Count, resultaat.GestoptOpLimiet) switch
            {
                (_, true) => $"De daglimiet van {EmailDienst.Daglimiet} is bereikt: {resultaat.NietVerstuurd} niet verstuurd. Verstuur ze morgen opnieuw.",
                ( > 0, _) => $"{resultaat.Mislukt.Count} niet verstuurd; zie de lijst onder het bericht.",
                _ => null,
            };
        }
        catch (InvalidOperationException ex)
        {
            Foutmelding = ex.Message;
        }
        finally
        {
            Voortgang = null;
            IsBezig = false;
        }

        await VernieuwLogboekAsync();
    }

    /// <summary>Het bericht van de gekozen ontvanger naar het eigen adres, om te controleren.</summary>
    [RelayCommand(CanExecute = nameof(KanVersturen))]
    private async Task ProefAsync()
    {
        IsBezig = true;
        Melding = null;
        Foutmelding = null;
        try
        {
            await _dienst.ProefAsync(GekozenOntvanger ?? Ontvangers[0], Onderwerp, Tekst);
            Melding = "Proef verstuurd naar uw eigen adres.";
        }
        catch (Exception ex) when (ex is EmailVerzendException or InvalidOperationException)
        {
            Foutmelding = ex.Message;
        }
        finally
        {
            IsBezig = false;
        }
    }

    private async Task VernieuwLogboekAsync()
    {
        var stand = await _dienst.LaadAsync();
        _verzondenVandaag = _dienst.VerzondenVandaag(stand);
        Logboek.Clear();
        foreach (var bericht in stand.Logboek.OrderByDescending(b => b.TijdstipUtc))
        {
            Logboek.Add(new VerzondenRegel(bericht));
        }

        OnPropertyChanged(nameof(Daglimiettekst));
    }

    // ---- Sjablonen ---------------------------------------------------------------------------

    [RelayCommand]
    private void NieuwSjabloon()
    {
        Subtab = EmailSubtab.Sjablonen;
        Beheersjabloon = null;
        Bewerking = new SjabloonBewerking(null, Emailsoort.Vrij);
    }

    private bool KanVerwijderen() => Beheersjabloon is { IsStandaard: false };

    [RelayCommand(CanExecute = nameof(KanVerwijderen))]
    private async Task VerwijderSjabloonAsync()
    {
        if (Beheersjabloon is not { } sjabloon || !_dialogen.Bevestig("Sjabloon verwijderen", $"Het sjabloon '{sjabloon.Naam}' verwijderen?"))
        {
            return;
        }

        await _dienst.VerwijderSjabloonAsync(sjabloon);
        Beheersjabloon = null;
        Bewerking = null;
        VulSjablonen((await _dienst.LaadAsync()).Sjablonen);
        Melding = $"Sjabloon '{sjabloon.Naam}' verwijderd.";
    }

    public override async Task<bool> BewaarAsync()
    {
        if (!KanOpslaan)
        {
            return !HeeftOnopgeslagenWijzigingen;
        }

        var bewerking = Bewerking!;
        Emailsjabloon sjabloon;
        try
        {
            if (bewerking.Bron is { } bron)
            {
                bron.Wijzig(bewerking.Naam, bewerking.Onderwerp, bewerking.Tekst);
                sjabloon = bron;
            }
            else
            {
                sjabloon = new Emailsjabloon(bewerking.Soort, bewerking.Naam, bewerking.Onderwerp, bewerking.Tekst);
            }

            await _dienst.BewaarSjabloonAsync(sjabloon);
        }
        catch (ArgumentException ex)
        {
            Foutmelding = ex.Message;
            return false;
        }

        Foutmelding = null;
        VulSjablonen((await _dienst.LaadAsync()).Sjablonen);
        Beheersjabloon = Sjablonen.FirstOrDefault(s => s.Id == sjabloon.Id);
        Melding = $"Sjabloon '{sjabloon.Naam}' opgeslagen.";
        return true;
    }

    private sealed class Melder(Action<int> actie) : IProgress<int>
    {
        public void Report(int value) => actie(value);
    }
}

/// <summary>Bewerkbare kopie van een sjabloon; pas bij Opslaan verandert het sjabloon zelf.</summary>
public sealed partial class SjabloonBewerking : ObservableObject
{
    public SjabloonBewerking(Emailsjabloon? bron, Emailsoort soort)
    {
        Bron = bron;
        Soort = soort;
        Naam = bron?.Naam ?? string.Empty;
        Onderwerp = bron?.Onderwerp ?? string.Empty;
        Tekst = bron?.Tekst ?? string.Empty;
    }

    public Emailsjabloon? Bron { get; }

    public bool IsNieuw => Bron is null;

    public bool IsStandaard => Bron?.IsStandaard == true;

    public static IReadOnlyList<Emailsoort> Soorten { get; } = Enum.GetValues<Emailsoort>();

    /// <summary>Alleen bij een nieuw sjabloon te kiezen; het bepaalt de invulvelden.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Invulvelden), nameof(IsGewijzigd))]
    public partial Emailsoort Soort { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(IsGewijzigd))]
    public partial string Naam { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(IsGewijzigd))]
    public partial string Onderwerp { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(IsGewijzigd))]
    public partial string Tekst { get; set; }

    public IReadOnlyList<Invulveld> Invulvelden => Application.Email.Invulvelden.Voor(Soort);

    public string? Fout =>
        string.IsNullOrWhiteSpace(Naam) ? "Vul een naam in."
        : Naam.Trim().Length > 100 ? "De naam mag maximaal 100 tekens lang zijn."
        : string.IsNullOrWhiteSpace(Onderwerp) ? "Vul een onderwerp in."
        : Onderwerp.Trim().Length > 200 ? "Het onderwerp mag maximaal 200 tekens lang zijn."
        : string.IsNullOrWhiteSpace(Tekst) ? "Vul een tekst in."
        : null;

    public bool IsGewijzigd => Bron is null
        ? !string.IsNullOrWhiteSpace(Naam) || !string.IsNullOrWhiteSpace(Onderwerp) || !string.IsNullOrWhiteSpace(Tekst)
        : Naam != Bron.Naam || Onderwerp != Bron.Onderwerp || Tekst != Bron.Tekst;
}
