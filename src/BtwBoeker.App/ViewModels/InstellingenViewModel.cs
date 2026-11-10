using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtwBoeker.Application.Administraties;
using BtwBoeker.Application.Modules;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;

namespace BtwBoeker.App.ViewModels;

public sealed partial class InstellingenViewModel : TabbladViewModel, IPaginameldingen
{
    private readonly IAdministratieRepository _repository;

    public InstellingenViewModel(IAdministratieRepository repository, IEnumerable<AdministratieViewModel> administraties)
        : base(ModuleCatalogus.Geef(ModuleSoort.Instellingen), administratie: null)
    {
        _repository = repository;
        OpslaanCommand = new AsyncRelayCommand(BewaarAsync, () => KanOpslaan);
        Regels = administraties.Select(a => new AdministratieInstellingRegel(a)).ToList();
        foreach (var regel in Regels)
        {
            regel.PropertyChanged += RegelGewijzigd;
        }
    }

    public IReadOnlyList<AdministratieInstellingRegel> Regels { get; }

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    public IAsyncRelayCommand OpslaanCommand { get; }

    public override bool HeeftOnopgeslagenWijzigingen => Regels.Any(r => r.IsGewijzigd);

    public override bool KanOpslaan => HeeftOnopgeslagenWijzigingen && Regels.All(r => r.Fout is null);

    public override async Task<bool> BewaarAsync()
    {
        if (!KanOpslaan)
        {
            return !HeeftOnopgeslagenWijzigingen;
        }

        foreach (var regel in Regels.Where(r => r.IsGewijzigd))
        {
            await regel.BewaarAsync(_repository);
        }

        Foutmelding = null;
        return true;
    }

    public override void Opruimen()
    {
        foreach (var regel in Regels)
        {
            regel.PropertyChanged -= RegelGewijzigd;
        }

        base.Opruimen();
    }

    private void RegelGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HeeftOnopgeslagenWijzigingen));
        OnPropertyChanged(nameof(KanOpslaan));
        OpslaanCommand.NotifyCanExecuteChanged();
    }
}

/// <summary>Bewerkbare kopie van één administratie; pas bij Opslaan wordt het domeinmodel gewijzigd.</summary>
public sealed partial class AdministratieInstellingRegel : ObservableObject
{
    private readonly AdministratieViewModel _bron;

    public AdministratieInstellingRegel(AdministratieViewModel bron)
    {
        _bron = bron;
        Naam = bron.Naam;
        KleurHex = bron.Accentkleur.ToString();
        Drempeltekst = Drempelweergave(bron.Model.ZonderBonDrempel);
    }

    /// <summary>Voorgestelde accentkleuren; allemaal goed leesbaar met witte tekst.</summary>
    public static IReadOnlyList<string> Kleurvoorstellen { get; } =
        ["#2E7D32", "#1565C0", "#6A1B9A", "#C62828", "#EF6C00", "#00838F", "#5D4037", "#37474F"];

    public AdministratieSoort Soort => _bron.Soort;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(IsGewijzigd))]
    public partial string Naam { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(IsGewijzigd), nameof(Voorbeeldkleur), nameof(Voorbeeldtekstkleur))]
    public partial string KleurHex { get; set; }

    /// <summary>Vanaf welk bedrag het filter <i>Zonder bon</i> een boeking zonder bijlage toont.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(IsGewijzigd))]
    public partial string Drempeltekst { get; set; }

    private long? Drempel => Bedrag.TryParse(Drempeltekst, out var bedrag) && bedrag.Centen >= 0 ? bedrag.Centen : null;

    public Kleur? Voorbeeldkleur => Kleur.TryParse(KleurHex, out var k) ? k : null;

    public Kleur? Voorbeeldtekstkleur => Voorbeeldkleur?.LeesbareTekstkleur();

    public string? Fout
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Naam))
            {
                return "Vul een naam in.";
            }

            if (Naam.Trim().Length > Administratie.MaximaleNaamLengte)
            {
                return $"De naam mag maximaal {Administratie.MaximaleNaamLengte} tekens lang zijn.";
            }

            if (Voorbeeldkleur is null)
            {
                return "Gebruik een kleurcode in de vorm #RRGGBB, bijvoorbeeld #2E7D32.";
            }

            return Drempel is null ? "Vul bij de drempel voor 'zonder bon' een bedrag in, bijvoorbeeld 10,00." : null;
        }
    }

    public bool IsGewijzigd =>
        !string.Equals(Naam?.Trim(), _bron.Naam, StringComparison.Ordinal) || Voorbeeldkleur != _bron.Accentkleur
        || Drempel != _bron.Model.ZonderBonDrempel;

    [RelayCommand]
    private void KiesKleur(string hex) => KleurHex = hex;

    internal async Task BewaarAsync(IAdministratieRepository repository)
    {
        _bron.Model.WijzigNaam(Naam);
        _bron.Model.WijzigAccentkleur(Voorbeeldkleur!.Value);
        _bron.Model.WijzigZonderBonDrempel(Drempel!.Value);
        await repository.BewaarAsync(_bron.Model);
        _bron.Ververs();
        Naam = _bron.Naam;
        Drempeltekst = Drempelweergave(_bron.Model.ZonderBonDrempel);
        OnPropertyChanged(nameof(IsGewijzigd));
    }

    private static string Drempelweergave(long centen) =>
        (centen / 100m).ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("nl-NL"));
}
