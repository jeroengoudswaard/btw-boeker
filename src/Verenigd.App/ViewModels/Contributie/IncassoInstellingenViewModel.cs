using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.Application.Incasso;
using Verenigd.Domain.Financien;

namespace Verenigd.App.ViewModels.Contributie;

/// <summary>Instellingen › Incasso: de Vereniging als incassant (besluiten 50, 56 en 57).</summary>
public sealed partial class IncassoInstellingenViewModel : ObservableObject
{
    private readonly IContributieRepository _repository;
    private string _origineel = string.Empty;

    public IncassoInstellingenViewModel(IContributieRepository repository, TimeProvider tijd)
    {
        ArgumentNullException.ThrowIfNull(tijd);
        _repository = repository;
        var huidig = Maand.Van(DateOnly.FromDateTime(tijd.GetLocalNow().DateTime));
        Maandkeuzes = new Maand(huidig.Jaar - 2, huidig.Nummer).Tot(new Maand(huidig.Jaar + 1, huidig.Nummer));
        VoorbeeldMaand = huidig.Volgende;
        EersteIncassomaand = huidig;
        LaadCommand = new AsyncRelayCommand(LaadAsync);
    }

    public IAsyncRelayCommand LaadCommand { get; }

    public IReadOnlyList<Maand> Maandkeuzes { get; }

    public static IReadOnlyList<int> Incassodagen { get; } = [.. Enumerable.Range(1, 28)];

    private Maand VoorbeeldMaand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(IsGewijzigd))]
    public partial string IncassantId { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(IsGewijzigd))]
    public partial string Naam { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(IsGewijzigd))]
    public partial string Iban { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(IsGewijzigd))]
    public partial int Incassodag { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial Maand EersteIncassomaand { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(IsGewijzigd), nameof(Voorbeeld))]
    public partial string Omschrijvingssjabloon { get; set; } = Incassoinstellingen.StandaardSjabloon;

    public string Voorbeeld => SepaTekst.Normaliseer(
        (Omschrijvingssjabloon ?? string.Empty)
            .Replace("{maanden}", VoorbeeldMaand.ToString(), StringComparison.Ordinal)
            .Replace("{voornamen}", "Anna, Bo", StringComparison.Ordinal),
        140);

    public string? Fout
    {
        get
        {
            if (!Domain.Financien.IncassantId.TryMaak(IncassantId, out _))
            {
                return "Het incassant-ID is ongeldig (controleer het controlegetal), bijvoorbeeld NL12ZZZ123456780000.";
            }

            if (string.IsNullOrWhiteSpace(Naam))
            {
                return "Vul de naam van de Vereniging in zoals die bij de bank bekend is.";
            }

            if (!Domain.Financien.Iban.TryMaak(Iban, out var iban) || !iban.ControlegetalGeldig)
            {
                return "Het IBAN van de Vereniging is ongeldig.";
            }

            if (Incassodag is < 1 or > 28)
            {
                return "Kies een incassodag van 1 tot en met 28.";
            }

            return string.IsNullOrWhiteSpace(Omschrijvingssjabloon) ? "Vul de omschrijving in." : null;
        }
    }

    public bool IsGewijzigd => Momentopname() != _origineel;

    public async Task LaadAsync()
    {
        var instellingen = (await _repository.LaadAsync()).Instellingen;
        if (instellingen is not null)
        {
            IncassantId = instellingen.IncassantId.Waarde;
            Naam = instellingen.Naam;
            Iban = instellingen.Iban.ToString();
            Incassodag = instellingen.Incassodag;
            EersteIncassomaand = instellingen.EersteIncassomaand;
            Omschrijvingssjabloon = instellingen.Omschrijvingssjabloon;
        }

        _origineel = Momentopname();
        OnPropertyChanged(nameof(IsGewijzigd));
    }

    /// <exception cref="ArgumentException">Ongeldige invoer.</exception>
    public async Task BewaarAsync()
    {
        await _repository.BewaarInstellingenAsync(
            new IncassoinstellingenInvoer(IncassantId, Naam, Iban, Incassodag, EersteIncassomaand, Omschrijvingssjabloon));
        _origineel = Momentopname();
        OnPropertyChanged(nameof(IsGewijzigd));
    }

    private string Momentopname() => string.Join('|', IncassantId, Naam, Iban, Incassodag, EersteIncassomaand, Omschrijvingssjabloon);
}
