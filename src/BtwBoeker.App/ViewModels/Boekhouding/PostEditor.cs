using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.ViewModels.Boekhouding;

/// <summary>Eén regel in de journaalpost-editor.</summary>
public sealed partial class PostRegelEditor : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(IsLeeg))]
    public partial Grootboekrekening? GekozenRekening { get; set; }

    [ObservableProperty]
    public partial string? Omschrijving { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(IsLeeg), nameof(Debet))]
    public partial string Debettekst { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(IsLeeg), nameof(Credit))]
    public partial string Credittekst { get; set; } = string.Empty;

    public Bedrag Debet => Lees(Debettekst) ?? Bedrag.Nul;

    public Bedrag Credit => Lees(Credittekst) ?? Bedrag.Nul;

    public bool IsLeeg => GekozenRekening is null && string.IsNullOrWhiteSpace(Debettekst) && string.IsNullOrWhiteSpace(Credittekst);

    public string? Fout => IsLeeg ? null
        : (!string.IsNullOrWhiteSpace(Debettekst) && Lees(Debettekst) is null) || (!string.IsNullOrWhiteSpace(Credittekst) && Lees(Credittekst) is null)
            ? "Gebruik een bedrag zoals 12,50."
        : Debet != Bedrag.Nul && Credit != Bedrag.Nul ? "Vul debet óf credit in."
        : GekozenRekening is null ? "Kies een rekening."
        : null;

    private static Bedrag? Lees(string tekst) => string.IsNullOrWhiteSpace(tekst) ? null : Bedrag.TryParse(tekst, out var b) ? b : null;
}

/// <summary>
/// Een journaalpost bewerken (concept) of bekijken (geboekt). Wijzigingen worden pas opgeslagen via het
/// tabblad (Ctrl+S voor concept, Ctrl+Enter voor boeken).
/// </summary>
public sealed partial class PostEditor : ObservableObject
{
    private readonly string _origineel;

    public PostEditor(Journaalpost? post, IReadOnlyList<Dagboek> dagboeken, IReadOnlyList<Grootboekrekening> rekeningen, DateOnly vandaag)
    {
        ArgumentNullException.ThrowIfNull(dagboeken);
        ArgumentNullException.ThrowIfNull(rekeningen);
        Id = post?.Id;
        Nummer = post?.Nummer ?? "(nieuw)";
        IsAlleenLezen = post?.IsGeboekt == true;
        Dagboeken = dagboeken;
        Rekeningen = IsAlleenLezen ? rekeningen : [.. rekeningen.Where(r => r.IsActief || post?.Regels.Any(g => g.Rekening.Id == r.Id) == true)];
        GekozenDagboek = post is null
            ? dagboeken.FirstOrDefault(d => d.Soort == DagboekSoort.Memoriaal) ?? (dagboeken.Count > 0 ? dagboeken[0] : null)
            : dagboeken.FirstOrDefault(d => d.Id == post.Dagboek.Id) ?? post.Dagboek;
        Datum = (post?.Datum ?? vandaag).ToDateTime(TimeOnly.MinValue);
        Omschrijving = post?.Omschrijving ?? string.Empty;
        Statustekst = post is null ? "Nieuw" : post.IsGeboekt ? $"Geboekt op {Weergave.Datum(DateOnly.FromDateTime(post.GeboektUtc!.Value.ToLocalTime()))}" : "Concept";
        Regels.CollectionChanged += RegelsGewijzigd;
        if (post is null)
        {
            Regels.Add(new PostRegelEditor());
            Regels.Add(new PostRegelEditor());
        }
        else
        {
            foreach (var regel in post.Regels)
            {
                Regels.Add(new PostRegelEditor
                {
                    GekozenRekening = Rekeningen.FirstOrDefault(r => r.Id == regel.Rekening.Id) ?? regel.Rekening,
                    Omschrijving = regel.Omschrijving,
                    Debettekst = regel.Debet == Bedrag.Nul ? string.Empty : Weergave.Invoer(regel.Debet),
                    Credittekst = regel.Credit == Bedrag.Nul ? string.Empty : Weergave.Invoer(regel.Credit),
                });
            }
        }

        _origineel = Momentopname();
        NieuweRegelCommand = new RelayCommand(() => Regels.Add(new PostRegelEditor()), () => !IsAlleenLezen);
        VerwijderRegelCommand = new RelayCommand<PostRegelEditor>(r => Regels.Remove(r!), r => !IsAlleenLezen && r is not null);
    }

    public Guid? Id { get; }

    public string Nummer { get; }

    public string Statustekst { get; }

    public bool IsAlleenLezen { get; }

    public bool IsBewerkbaar => !IsAlleenLezen;

    public IReadOnlyList<Dagboek> Dagboeken { get; }

    public IReadOnlyList<Grootboekrekening> Rekeningen { get; }

    public ObservableCollection<PostRegelEditor> Regels { get; } = [];

    public IRelayCommand NieuweRegelCommand { get; }

    public IRelayCommand<PostRegelEditor> VerwijderRegelCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fouten))]
    public partial Dagboek? GekozenDagboek { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fouten))]
    public partial DateTime? Datum { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fouten))]
    public partial string Omschrijving { get; set; }

    public Bedrag TotaalDebet => Regels.Select(r => r.Debet).Som();

    public Bedrag TotaalCredit => Regels.Select(r => r.Credit).Som();

    public string TotaalDebettekst => TotaalDebet.ToString();

    public string TotaalCredittekst => TotaalCredit.ToString();

    /// <summary>Het verschil tussen debet en credit, altijd positief weergegeven.</summary>
    public string Verschiltekst => new Bedrag(Math.Abs(TotaalDebet.Centen - TotaalCredit.Centen)).ToString();

    public bool IsInEvenwicht => TotaalDebet == TotaalCredit && TotaalDebet != Bedrag.Nul;

    public bool IsGewijzigd => !IsAlleenLezen && !string.Equals(Momentopname(), _origineel, StringComparison.Ordinal);

    /// <summary>Wat het opslaan als concept in de weg staat.</summary>
    public IReadOnlyList<string> Fouten
    {
        get
        {
            var fouten = new List<string>();
            if (GekozenDagboek is null)
            {
                fouten.Add("Kies een dagboek.");
            }

            if (Datum is null)
            {
                fouten.Add("Vul de datum in.");
            }

            if (string.IsNullOrWhiteSpace(Omschrijving))
            {
                fouten.Add("Vul een omschrijving in.");
            }

            fouten.AddRange(Regels.Select(r => r.Fout).OfType<string>().Distinct());
            return fouten;
        }
    }

    /// <summary>Wat er nog ontbreekt om te boeken.</summary>
    public string? Boekmelding => Fouten.Count > 0 ? Fouten[0]
        : Regels.Count(r => !r.IsLeeg) < 2 ? "Een post heeft minstens twee regels."
        : !IsInEvenwicht ? $"Debet en credit verschillen {Verschiltekst}."
        : null;

    public bool KanBoeken => !IsAlleenLezen && Boekmelding is null;

    internal PostInvoer Invoer => new(
        Id, GekozenDagboek!.Id, DateOnly.FromDateTime(Datum!.Value), Omschrijving.Trim(),
        [.. Regels.Where(r => !r.IsLeeg).Select(r => new RegelInvoerId(r.GekozenRekening!.Id, Leeg(r.Omschrijving), r.Debet, r.Credit))]);

    private static string? Leeg(string? tekst) => string.IsNullOrWhiteSpace(tekst) ? null : tekst.Trim();

    private string Momentopname() => string.Join('|',
        GekozenDagboek?.Id, Datum?.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture), Omschrijving,
        string.Join(';', Regels.Where(r => !r.IsLeeg).Select(r => $"{r.GekozenRekening?.Id}:{r.Omschrijving}:{r.Debettekst?.Trim()}:{r.Credittekst?.Trim()}")));

    private void RegelsGewijzigd(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (PostRegelEditor regel in e.OldItems ?? Array.Empty<PostRegelEditor>())
        {
            regel.PropertyChanged -= RegelGewijzigd;
        }

        foreach (PostRegelEditor regel in e.NewItems ?? Array.Empty<PostRegelEditor>())
        {
            regel.PropertyChanged += RegelGewijzigd;
        }

        Totalen();
    }

    private void RegelGewijzigd(object? sender, PropertyChangedEventArgs e) => Totalen();

    private void Totalen()
    {
        OnPropertyChanged(nameof(TotaalDebettekst));
        OnPropertyChanged(nameof(TotaalCredittekst));
        OnPropertyChanged(nameof(Verschiltekst));
        OnPropertyChanged(nameof(IsInEvenwicht));
        OnPropertyChanged(nameof(IsGewijzigd));
        OnPropertyChanged(nameof(Fouten));
        OnPropertyChanged(nameof(Boekmelding));
        OnPropertyChanged(nameof(KanBoeken));
    }

    partial void OnGekozenDagboekChanged(Dagboek? value) => Totalen();

    partial void OnDatumChanged(DateTime? value) => Totalen();

    partial void OnOmschrijvingChanged(string value) => Totalen();
}
