using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtwBoeker.Application.Administraties;
using BtwBoeker.Application.Modules;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;

namespace BtwBoeker.App.ViewModels;

/// <summary>
/// De pagina Instellingen: de naam van de administratie en de drempel voor <i>Zonder bon</i>. Een bewerkbare kopie;
/// pas bij Opslaan wordt het domeinmodel gewijzigd.
/// </summary>
public sealed partial class InstellingenViewModel : PaginaViewModel, IPaginameldingen
{
    private static readonly CultureInfo Nederlands = CultureInfo.GetCultureInfo("nl-NL");

    private readonly IAdministratieRepository _repository;
    private readonly AdministratieViewModel _administratie;

    public InstellingenViewModel(IAdministratieRepository repository, AdministratieViewModel administratie)
        : base(ModuleCatalogus.Geef(ModuleSoort.Instellingen))
    {
        _repository = repository;
        _administratie = administratie;
        OpslaanCommand = new AsyncRelayCommand(BewaarAsync, () => KanOpslaan);
        Naam = administratie.Naam;
        Drempeltekst = Drempelweergave(administratie.Model.ZonderBonDrempel);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(HeeftOnopgeslagenWijzigingen), nameof(KanOpslaan))]
    [NotifyCanExecuteChangedFor(nameof(OpslaanCommand))]
    public partial string Naam { get; set; }

    /// <summary>Vanaf welk bedrag het filter <i>Zonder bon</i> een boeking zonder bijlage toont.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(HeeftOnopgeslagenWijzigingen), nameof(KanOpslaan))]
    [NotifyCanExecuteChangedFor(nameof(OpslaanCommand))]
    public partial string Drempeltekst { get; set; }

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    public IAsyncRelayCommand OpslaanCommand { get; }

    private long? Drempel => Bedrag.TryParse(Drempeltekst, out var bedrag) && bedrag.Centen >= 0 ? bedrag.Centen : null;

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

            return Drempel is null ? "Vul bij de drempel voor 'zonder bon' een bedrag in, bijvoorbeeld 10,00." : null;
        }
    }

    public override bool HeeftOnopgeslagenWijzigingen =>
        !string.Equals(Naam?.Trim(), _administratie.Naam, StringComparison.Ordinal) || Drempel != _administratie.Model.ZonderBonDrempel;

    public override bool KanOpslaan => HeeftOnopgeslagenWijzigingen && Fout is null;

    public override async Task<bool> BewaarAsync()
    {
        if (!KanOpslaan)
        {
            return !HeeftOnopgeslagenWijzigingen;
        }

        _administratie.Model.WijzigNaam(Naam);
        _administratie.Model.WijzigZonderBonDrempel(Drempel!.Value);
        await _repository.BewaarAsync(_administratie.Model);
        _administratie.Ververs();
        Naam = _administratie.Naam;
        Drempeltekst = Drempelweergave(_administratie.Model.ZonderBonDrempel);
        Foutmelding = null;
        OnPropertyChanged(nameof(HeeftOnopgeslagenWijzigingen));
        OnPropertyChanged(nameof(KanOpslaan));
        OpslaanCommand.NotifyCanExecuteChanged();
        return true;
    }

    private static string Drempelweergave(long centen) => (centen / 100m).ToString("0.00", Nederlands);
}
