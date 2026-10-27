using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.App.ViewModels.Contributie;
using Verenigd.Application.Administraties;
using Verenigd.Application.Modules;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.ViewModels;

public sealed partial class InstellingenViewModel : TabbladViewModel
{
    private readonly IAdministratieRepository _repository;

    public InstellingenViewModel(
        IAdministratieRepository repository, IEnumerable<AdministratieViewModel> administraties, IncassoInstellingenViewModel? incasso = null)
        : base(ModuleCatalogus.Geef(ModuleSoort.Instellingen), administratie: null)
    {
        _repository = repository;
        Incasso = incasso;
        if (incasso is not null)
        {
            incasso.PropertyChanged += RegelGewijzigd;
        }

        OpslaanCommand = new AsyncRelayCommand(BewaarAsync, () => KanOpslaan);
        Regels = administraties.Select(a => new AdministratieInstellingRegel(a)).ToList();
        foreach (var regel in Regels)
        {
            regel.PropertyChanged += RegelGewijzigd;
        }
    }

    public IReadOnlyList<AdministratieInstellingRegel> Regels { get; }

    /// <summary>Incassogegevens van de Vereniging (fase 3).</summary>
    public IncassoInstellingenViewModel? Incasso { get; }

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    public IAsyncRelayCommand OpslaanCommand { get; }

    public override bool HeeftOnopgeslagenWijzigingen => Regels.Any(r => r.IsGewijzigd) || Incasso?.IsGewijzigd == true;

    public override bool KanOpslaan =>
        HeeftOnopgeslagenWijzigingen && Regels.All(r => r.Fout is null) && (Incasso is not { IsGewijzigd: true } || Incasso.Fout is null);

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

        if (Incasso is { IsGewijzigd: true })
        {
            try
            {
                await Incasso.BewaarAsync();
            }
            catch (ArgumentException ex)
            {
                Foutmelding = ex.Message;
                return false;
            }
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

        if (Incasso is not null)
        {
            Incasso.PropertyChanged -= RegelGewijzigd;
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

            return Voorbeeldkleur is null ? "Gebruik een kleurcode in de vorm #RRGGBB, bijvoorbeeld #2E7D32." : null;
        }
    }

    public bool IsGewijzigd =>
        !string.Equals(Naam?.Trim(), _bron.Naam, StringComparison.Ordinal) || Voorbeeldkleur != _bron.Accentkleur;

    [RelayCommand]
    private void KiesKleur(string hex) => KleurHex = hex;

    internal async Task BewaarAsync(IAdministratieRepository repository)
    {
        _bron.Model.WijzigNaam(Naam);
        _bron.Model.WijzigAccentkleur(Voorbeeldkleur!.Value);
        await repository.BewaarAsync(_bron.Model);
        _bron.Ververs();
        Naam = _bron.Naam;
        OnPropertyChanged(nameof(IsGewijzigd));
    }
}
