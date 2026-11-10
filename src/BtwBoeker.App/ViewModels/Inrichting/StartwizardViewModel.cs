using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtwBoeker.Application.Database;
using BtwBoeker.Application.Inrichting;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.App.ViewModels.Inrichting;

/// <summary>De routes van een lege boekhouding, zoals op de startkaart van het tabblad Boekhouding.</summary>
public enum Boekhoudingkeuze
{
    Standaard,
    Sjabloon,
    Leeg,
}

/// <summary>
/// Wat de startwizard aan het hoofdvenster vraagt (besluit 172): de bestaande wizards, diensten en tabbladen, zodat de
/// startwizard alleen regisseert en niets opnieuw bouwt.
/// </summary>
public interface IStartwizardRegie
{
    IDatabaseSessie? Sessie { get; }

    IReadOnlyList<AdministratieViewModel> Administraties { get; }

    Task NieuweDatabaseAsync();

    Task OpenDatabaseAsync();

    Task RichtBoekhoudingInAsync(AdministratieSoort administratie, Boekhoudingkeuze keuze);

    Task ImporteerBankafschriftAsync(AdministratieSoort administratie);
}

/// <summary>Eén stap in de stappenbalk bovenaan.</summary>
public sealed partial class StapRegel(Startstap stap, string titel) : ObservableObject
{
    public Startstap Stap { get; } = stap;

    public string Titel { get; } = titel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Teken), nameof(Toegankelijkenaam))]
    public partial bool IsHuidig { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Teken), nameof(Toegankelijkenaam))]
    public partial bool IsKlaar { get; set; }

    /// <summary>✓ klaar, ● huidige stap, ○ nog te doen; de tekst ernaast maakt het ook zonder kleur duidelijk.</summary>
    public string Teken => IsHuidig ? "●" : IsKlaar ? "✓" : "○";

    public string Toegankelijkenaam => $"{Titel}{(IsHuidig ? ", huidige stap" : string.Empty)}{(IsKlaar ? ", klaar" : string.Empty)}";
}

/// <summary>Eén regel in de samenvatting van de stap Klaar.</summary>
public sealed record KlaarRegel(bool IsKlaar, string Tekst)
{
    public string Teken => IsKlaar ? "✓" : "⚠";
}

/// <summary>
/// De startwizard: Welkom → Database → Administraties → Boekhouding → Bank → Klaar. Elke stap is over te slaan;
/// het ✓ volgt uit de gegevens zelf; <i>Later verder</i> bewaart de stap
/// in de database.
/// </summary>
public sealed partial class StartwizardViewModel : ObservableObject
{
    private readonly IStartwizardRegie _regie;
    private readonly TimeProvider _tijd;
    private IDatabaseSessie? _geladenSessie;

    public StartwizardViewModel(IStartwizardRegie regie, TimeProvider tijd, Startstap begin)
    {
        _regie = regie;
        _tijd = tijd;
        Stappen =
        [
            new(Startstap.Welkom, "Welkom"),
            new(Startstap.Database, "Database"),
            new(Startstap.Administraties, "Administraties"),
            new(Startstap.Boekhouding, "Boekhouding"),
            new(Startstap.Bank, "Bank"),
            new(Startstap.Klaar, "Klaar"),
        ];
        Huidig = begin;
        MarkeerStappen();
    }

    /// <summary>Vraagt het venster om te sluiten.</summary>
    public event EventHandler? SluitenGevraagd;

    public IReadOnlyList<StapRegel> Stappen { get; }

    public int Jaar => _tijd.GetLocalNow().Year;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HuidigeTitel), nameof(IsHuidigeStapKlaar), nameof(Stapnummer))]
    [NotifyCanExecuteChangedFor(nameof(VolgendeCommand), nameof(VorigeCommand), nameof(OverslaanCommand))]
    public partial Startstap Huidig { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHuidigeStapKlaar), nameof(Klaarregels), nameof(KanBankInlezen))]
    [NotifyCanExecuteChangedFor(nameof(VolgendeCommand), nameof(OverslaanCommand), nameof(GaNaarCommand))]
    public partial Inrichtingsstatus Status { get; private set; } = Inrichtingsstatus.Leeg;

    /// <summary>Naam en accentkleur per administratie (stap Vereniging), dezelfde regels als Extra › Instellingen.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<AdministratieInstellingRegel> Namen { get; private set; } = [];

    [ObservableProperty]
    public partial AdministratieSoort BoekhoudingVoor { get; set; } = AdministratieSoort.Vereniging;

    public static IReadOnlyList<AdministratieSoort> BoekhoudingKeuzes { get; } = [AdministratieSoort.Vereniging, AdministratieSoort.Stichting];

    /// <summary>Besluit 209: ook de Stichting heeft eigen bankrekeningen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KanBankInlezen))]
    public partial AdministratieSoort BankVoor { get; set; } = AdministratieSoort.Vereniging;

    /// <summary>Een bankafschrift kan pas als de boekhouding van die administratie is ingericht.</summary>
    public bool KanBankInlezen => BankVoor == AdministratieSoort.Stichting ? Status.BoekhoudingStichting : Status.BoekhoudingVereniging;

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VolgendeCommand), nameof(VorigeCommand), nameof(OverslaanCommand), nameof(LaterVerderCommand))]
    public partial bool IsBezig { get; private set; }

    public bool IsGesloten { get; private set; }

    public string HuidigeTitel => Stappen[(int)Huidig].Titel;

    public string Stapnummer => string.Create(CultureInfo.CurrentCulture, $"Stap {(int)Huidig + 1} van {Stappen.Count}");

    /// <summary>Besluit 175: een stap die al klaar is toont "Al ingericht" en de knoppen om het toch opnieuw te doen.</summary>
    public bool IsHuidigeStapKlaar => Status.IsKlaar(Huidig);

    public IReadOnlyList<KlaarRegel> Klaarregels =>
    [
        new(Status.DatabaseOpen, Status.DatabaseOpen ? "De database is aangemaakt of geopend." : "Er is nog geen database: kies Bestand › Nieuwe database."),
        new(Status.NamenIngevuld, Status.NamenIngevuld ? "De administraties hebben een eigen naam en kleur." : "Pas de namen aan bij Instellingen."),
        new(Status.BoekhoudingVereniging, Status.BoekhoudingVereniging ? "De boekhouding van de Vereniging is ingericht." : "De boekhouding van de Vereniging is nog leeg: open Modules › Boekhouding."),
        new(Status.BankVereniging, Status.BankVereniging ? "Een bankrekening van de Vereniging is gekoppeld." : "Nog geen bankrekening: kies Bestand › Importeren › Bankafschrift."),
        .. (Status.BoekhoudingStichting ? [new KlaarRegel(true, "De boekhouding van de Stichting is ingericht.")] : Array.Empty<KlaarRegel>()),
        .. (Status.BankStichting ? [new KlaarRegel(true, "Een bankrekening van de Stichting is gekoppeld.")] : Array.Empty<KlaarRegel>()),
    ];

    /// <summary>Laadt de status en de gegevens van de stappen.</summary>
    public Task InitialiseerAsync() => VernieuwAsync();

    [RelayCommand(CanExecute = nameof(KanVolgende))]
    private async Task VolgendeAsync()
    {
        if (await BewaarStapAsync())
        {
            Huidig = Huidig + 1;
            MarkeerStappen();
        }
    }

    private bool KanVolgende() => !IsBezig && Huidig != Startstap.Klaar && (Huidig != Startstap.Database || Status.DatabaseOpen);

    /// <summary>Naar de volgende stap zonder de wijzigingen in deze stap op te slaan.</summary>
    [RelayCommand(CanExecute = nameof(KanOverslaan))]
    private async Task OverslaanAsync()
    {
        Foutmelding = null;
        await VernieuwAsync(gegevensOpnieuw: true); // ongedaan maken van niet-opgeslagen invoer
        Huidig = Huidig + 1;
        MarkeerStappen();
    }

    private bool KanOverslaan() => !IsBezig && Huidig is not (Startstap.Welkom or Startstap.Database or Startstap.Klaar);

    [RelayCommand(CanExecute = nameof(KanVorige))]
    private void Vorige()
    {
        Foutmelding = null;
        Huidig = Huidig - 1;
        MarkeerStappen();
    }

    private bool KanVorige() => !IsBezig && Huidig != Startstap.Welkom;

    /// <summary>Een stap in de stappenbalk kiezen; zonder database alleen Welkom en Database.</summary>
    [RelayCommand(CanExecute = nameof(KanNaar))]
    private void GaNaar(StapRegel? stap)
    {
        if (stap is null)
        {
            return;
        }

        Foutmelding = null;
        Huidig = stap.Stap;
        MarkeerStappen();
    }

    private bool KanNaar(StapRegel? stap) => stap is not null && (stap.Stap <= Startstap.Database || Status.DatabaseOpen);

    /// <summary>Sluiten en de stap onthouden (ook bij ✕); de volgende keer biedt de app aan om hier verder te gaan.</summary>
    [RelayCommand(CanExecute = nameof(KanSluiten))]
    private async Task LaterVerderAsync()
    {
        if (_regie.Sessie is { } sessie)
        {
            await sessie.Startwizard.BewaarAsync(new Startwizardvoortgang(Huidig, IsAfgerond: false));
        }

        Sluit();
    }

    private bool KanSluiten() => !IsBezig;

    [RelayCommand]
    private async Task VoltooienAsync()
    {
        if (_regie.Sessie is { } sessie)
        {
            await sessie.Startwizard.BewaarAsync(new Startwizardvoortgang(null, IsAfgerond: true));
        }

        Sluit();
    }

    [RelayCommand]
    private Task NieuweDatabaseAsync() => NaDatabaseAsync(_regie.NieuweDatabaseAsync);

    [RelayCommand]
    private Task OpenDatabaseAsync() => NaDatabaseAsync(_regie.OpenDatabaseAsync);

    [RelayCommand]
    private Task BoekhoudingInrichtenAsync(Boekhoudingkeuze keuze) => VoerUitAsync(() => _regie.RichtBoekhoudingInAsync(BoekhoudingVoor, keuze));

    [RelayCommand]
    private Task BankInlezenAsync() => VoerUitAsync(() => _regie.ImporteerBankafschriftAsync(BankVoor));

    private async Task NaDatabaseAsync(Func<Task> actie)
    {
        await VoerUitAsync(actie);
        if (Status.DatabaseOpen && Huidig == Startstap.Database)
        {
            Huidig = Startstap.Administraties;
            MarkeerStappen();
        }
    }

    private async Task VoerUitAsync(Func<Task> actie)
    {
        IsBezig = true;
        Foutmelding = null;
        try
        {
            await actie();
        }
        finally
        {
            IsBezig = false;
        }

        await VernieuwAsync();
    }

    /// <returns><c>false</c> als de invoer van deze stap niet klopt; de stap blijft dan staan met een melding.</returns>
    private async Task<bool> BewaarStapAsync()
    {
        Foutmelding = null;
        if (Huidig == Startstap.Administraties && _regie.Sessie is { } sessie)
        {
            if (Namen.FirstOrDefault(n => n.Fout is not null) is { } fout)
            {
                Foutmelding = fout.Fout;
                return false;
            }

            foreach (var regel in Namen.Where(n => n.IsGewijzigd))
            {
                await regel.BewaarAsync(sessie.Administraties);
            }
        }

        await VernieuwAsync();
        return true;
    }

    private async Task VernieuwAsync(bool gegevensOpnieuw = false)
    {
        Status = await Inrichtingsstatus.BepaalAsync(_regie.Sessie);
        var sessie = _regie.Sessie;
        if (gegevensOpnieuw || !ReferenceEquals(sessie, _geladenSessie))
        {
            _geladenSessie = sessie;
            Namen = sessie is null ? [] : [.. _regie.Administraties.Select(a => new AdministratieInstellingRegel(a))];
        }

        MarkeerStappen();
    }

    private void MarkeerStappen()
    {
        foreach (var stap in Stappen)
        {
            stap.IsHuidig = stap.Stap == Huidig;
            stap.IsKlaar = Status.IsKlaar(stap.Stap);
        }

        OnPropertyChanged(nameof(IsHuidigeStapKlaar));
        GaNaarCommand.NotifyCanExecuteChanged();
    }

    private void Sluit()
    {
        IsGesloten = true;
        SluitenGevraagd?.Invoke(this, EventArgs.Empty);
    }
}
