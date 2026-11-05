using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.Application.Leden;
using Verenigd.Application.Modules;
using Verenigd.Domain.Leden;

namespace Verenigd.App.ViewModels.Leden;

public sealed record Lidtypekeuze(Lidtype? Waarde, string Tekst);

/// <summary>Tabblad Leden: ledenlijst met filters en de gegevens van het geselecteerde lid.</summary>
public sealed partial class LedenViewModel : TabbladViewModel, IPaginameldingen
{
    public const string AlleGroepen = "Alle groepen";
    private static readonly StringComparer Sortering = StringComparer.Create(CultureInfo.GetCultureInfo("nl-NL"), ignoreCase: true);

    private readonly ILedenRepository _repository;
    private readonly TimeProvider _tijd;
    private List<LidRegel> _alle = [];

    public LedenViewModel(ILedenRepository repository, TimeProvider tijd)
        : base(ModuleCatalogus.Geef(ModuleSoort.Leden), administratie: null)
    {
        _repository = repository;
        _tijd = tijd;
        VernieuwCommand = new AsyncRelayCommand(VernieuwAsync);
        OpslaanCommand = new AsyncRelayCommand(BewaarAsync, () => KanOpslaan);
    }

    public static IReadOnlyList<Lidtypekeuze> Lidtypekeuzes { get; } =
        [new(null, "Alle lidtypes"), new(Lidtype.Jeugdlid, "Jeugdlid"), new(Lidtype.Kaderlid, "Kaderlid")];

    public Verzameling<LidRegel> Leden { get; } = [];

    public IAsyncRelayCommand VernieuwCommand { get; }

    public IAsyncRelayCommand OpslaanCommand { get; }

    [ObservableProperty]
    public partial IReadOnlyList<string> Groepfilters { get; private set; } = [AlleGroepen];

    [ObservableProperty]
    public partial string Zoektekst { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Groepfilter { get; set; } = AlleGroepen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GekozenLidtype))]
    public partial Lidtype? Lidtypefilter { get; set; }

    /// <summary>Voor de keuzelijst; "Alle lidtypes" is <see cref="Lidtypefilter"/> = <c>null</c>.</summary>
    public Lidtypekeuze GekozenLidtype
    {
        get => Lidtypekeuzes.First(k => k.Waarde == Lidtypefilter);
        set => Lidtypefilter = value?.Waarde;
    }

    [ObservableProperty]
    public partial bool ToonUitgeschreven { get; set; }

    /// <summary>Besluit 270: <i>Toon uitgeschreven</i> alleen als er uitgeschreven leden zijn.</summary>
    public bool HeeftUitgeschreven => _alle.Any(r => r.IsUitgeschreven) || ToonUitgeschreven;

    [ObservableProperty]
    public partial LidRegel? GeselecteerdLid { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KanOpslaan))]
    public partial bool IsBezig { get; private set; }

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    /// <summary>Er zijn nog geen leden: toon de verwijzing naar de import.</summary>
    public bool IsLeeg => _alle.Count == 0 && !IsBezig;

    public string Aantaltekst => $"{Leden.Count} van {_alle.Count} personen";

    public override bool HeeftOnopgeslagenWijzigingen => _alle.Exists(r => r.IsGewijzigd);

    public override bool KanOpslaan => HeeftOnopgeslagenWijzigingen && !IsBezig && _alle.TrueForAll(r => r.Fout is null);

    public override async Task<bool> BewaarAsync()
    {
        if (!KanOpslaan)
        {
            return !HeeftOnopgeslagenWijzigingen;
        }

        IsBezig = true;
        try
        {
            foreach (var regel in _alle.Where(r => r.IsGewijzigd).ToList())
            {
                await _repository.BewaarNotitiesAsync(regel.Id, regel.BewaarbareNotities, _tijd.GetUtcNow().UtcDateTime);
                regel.Bevestig();
            }

            Foutmelding = null;
            return true;
        }
        catch (KeyNotFoundException)
        {
            Foutmelding = "Een lid bestaat niet meer. Vernieuw de lijst (F5) en probeer het opnieuw.";
            return false;
        }
        finally
        {
            IsBezig = false;
            WijzigingenGewijzigd();
        }
    }

    public override void Opruimen()
    {
        foreach (var regel in _alle)
        {
            regel.PropertyChanged -= RegelGewijzigd;
        }

        base.Opruimen();
    }

    /// <summary>Laadt de leden opnieuw (F5). Niet-opgeslagen notities en de selectie blijven behouden.</summary>
    private async Task VernieuwAsync()
    {
        IsBezig = true;
        try
        {
            var vandaag = DateOnly.FromDateTime(_tijd.GetLocalNow().DateTime);
            var onopgeslagen = _alle.Where(r => r.IsGewijzigd).ToDictionary(r => r.Id, r => r.Notities);
            var selectie = GeselecteerdLid?.Id;
            var personen = await _repository.HaalAlleAsync();

            foreach (var regel in _alle)
            {
                regel.PropertyChanged -= RegelGewijzigd;
            }

            _alle = [.. personen
                .Select(p => new LidRegel(p, vandaag))
                .OrderBy(r => r.Achternaam, Sortering)
                .ThenBy(r => r.Voornaam, Sortering)
                .ThenBy(r => r.Lidnummer, StringComparer.Ordinal)];
            foreach (var regel in _alle)
            {
                if (onopgeslagen.TryGetValue(regel.Id, out var notities))
                {
                    regel.Notities = notities;
                }

                regel.PropertyChanged += RegelGewijzigd;
            }

            var groepen = _alle.SelectMany(r => r.Functies).Where(f => !f.IsTechnisch).Select(f => f.Groep).Distinct().Order(Sortering);
            Groepfilters = [AlleGroepen, .. groepen];
            if (!Groepfilters.Contains(Groepfilter))
            {
                Groepfilter = AlleGroepen;
            }

            Foutmelding = null;
            PasFilterToe();
            GeselecteerdLid = Leden.FirstOrDefault(r => r.Id == selectie);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Foutmelding = "De leden konden niet worden geladen.";
        }
        finally
        {
            IsBezig = false;
            OnPropertyChanged(nameof(IsLeeg));
            WijzigingenGewijzigd();
        }
    }

    partial void OnZoektekstChanged(string value) => PasFilterToe();

    partial void OnGroepfilterChanged(string value) => PasFilterToe();

    partial void OnLidtypefilterChanged(Lidtype? value) => PasFilterToe();

    partial void OnToonUitgeschrevenChanged(bool value) => PasFilterToe();

    private void PasFilterToe()
    {
        var zoek = Zoektekst?.Trim() ?? string.Empty;
        var geselecteerd = GeselecteerdLid;
        Leden.Vervang(_alle.Where(Voldoet));

        GeselecteerdLid = geselecteerd is not null && Leden.Contains(geselecteerd) ? geselecteerd : null;
        OnPropertyChanged(nameof(Aantaltekst));
        OnPropertyChanged(nameof(HeeftUitgeschreven));

        bool Voldoet(LidRegel regel) =>
            (ToonUitgeschreven || !regel.IsUitgeschreven)
            && (Groepfilter == AlleGroepen || regel.HeeftActieveFunctie(f => f.Groep == Groepfilter && !f.IsTechnisch))
            && (Lidtypefilter is not { } type || regel.HeeftActieveFunctie(f => f.Lidtype == type))
            && (zoek.Length == 0
                || regel.Naam.Contains(zoek, StringComparison.CurrentCultureIgnoreCase)
                || regel.Lidnummer.StartsWith(zoek, StringComparison.Ordinal)
                || regel.Email?.Contains(zoek, StringComparison.OrdinalIgnoreCase) == true);
    }

    private void RegelGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LidRegel.IsGewijzigd) or nameof(LidRegel.Fout))
        {
            WijzigingenGewijzigd();
        }
    }

    private void WijzigingenGewijzigd()
    {
        OnPropertyChanged(nameof(HeeftOnopgeslagenWijzigingen));
        OnPropertyChanged(nameof(KanOpslaan));
        OpslaanCommand.NotifyCanExecuteChanged();
    }
}
