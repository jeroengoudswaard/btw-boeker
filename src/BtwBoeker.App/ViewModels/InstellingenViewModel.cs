using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtwBoeker.Application.Administraties;
using BtwBoeker.Application.Modules;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.App.ViewModels;

/// <summary>
/// De pagina Instellingen (besluit 42): de naam van de administratie, en thema en weergave (die van de shell, zodat ze
/// gelijk lopen met het menu Beeld). Alles wordt direct bewaard, zoals in Windows-Instellingen: de naam bij het verlaten van
/// het veld of bij Enter. Een ongeldige naam wordt niet bewaard.
/// </summary>
public sealed partial class InstellingenViewModel : PaginaViewModel, IPaginameldingen
{
    private readonly IAdministratieRepository _repository;
    private readonly AdministratieViewModel _administratie;

    public InstellingenViewModel(IAdministratieRepository repository, AdministratieViewModel administratie)
        : base(ModuleCatalogus.Geef(ModuleSoort.Instellingen))
    {
        _repository = repository;
        _administratie = administratie;
        Naam = administratie.Naam;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(HeeftOnopgeslagenWijzigingen), nameof(KanOpslaan))]
    public partial string Naam { get; set; }

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    public string? Fout =>
        string.IsNullOrWhiteSpace(Naam) ? "Vul een naam in."
        : Naam.Trim().Length > Administratie.MaximaleNaamLengte ? $"De naam mag maximaal {Administratie.MaximaleNaamLengte} tekens lang zijn."
        : null;

    public override bool BewaartDirect => true;

    public override bool HeeftOnopgeslagenWijzigingen => !string.Equals(Naam?.Trim(), _administratie.Naam, StringComparison.Ordinal);

    public override bool KanOpslaan => HeeftOnopgeslagenWijzigingen && Fout is null;

    /// <summary>Het naamveld verliest de focus of de gebruiker drukt op Enter.</summary>
    [RelayCommand]
    private async Task BevestigNaamAsync() => await BewaarAsync();

    public override async Task<bool> BewaarAsync()
    {
        if (!KanOpslaan)
        {
            return !HeeftOnopgeslagenWijzigingen;
        }

        _administratie.Model.WijzigNaam(Naam);
        await _repository.BewaarAsync(_administratie.Model);
        _administratie.Ververs();
        Naam = _administratie.Naam;
        Foutmelding = null;
        return true;
    }

    /// <summary>Een ongeldige naam vervalt bij het verlaten van de pagina; een geldige wordt alsnog bewaard.</summary>
    public override string? Verlaten()
    {
        if (!HeeftOnopgeslagenWijzigingen)
        {
            return null;
        }

        if (Fout is not null)
        {
            Naam = _administratie.Naam;
            return "De nieuwe naam is niet bewaard, omdat hij leeg of te lang was.";
        }

        _ = BewaarAsync();
        return null;
    }
}
