using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.ViewModels.Boekhouding;

public enum BoekhoudingImportStap
{
    Bestand,
    Rekeningen,
    Beginbalans,
    Klaar,
}

/// <summary>Eén rekening uit het Voorblad; soort en overslaan zijn aan te passen.</summary>
public sealed partial class ImportRekeningRegel : ObservableObject
{
    public ImportRekeningRegel(VoorbladRekening rekening)
    {
        ArgumentNullException.ThrowIfNull(rekening);
        Bron = rekening;
        Soort = rekening.Soort;
        Overslaan = rekening.Overslaan;
    }

    public VoorbladRekening Bron { get; }
    public int Nummer => Bron.Nummer;
    public string Naam => Bron.Naam;
    public string Rubriek => Bron.Rubriek ?? string.Empty;
    public string Roltekst => Bron.Rol == RekeningRol.Geen ? string.Empty : Bron.Rol.ToString();
    public string Debettekst => Bron.Debet == Bedrag.Nul ? string.Empty : Bron.Debet.ToString();
    public string Credittekst => Bron.Credit == Bedrag.Nul ? string.Empty : Bron.Credit.ToString();

    [ObservableProperty]
    public partial RekeningSoort Soort { get; set; }

    [ObservableProperty]
    public partial bool Overslaan { get; set; }

    internal VoorbladRekening Gekozen => Bron with { Soort = Soort, Overslaan = Overslaan };
}

/// <summary>
/// Eenmalige overname van rekeningschema en eindbalans uit de huidige Excel-boekhouding (besluit 62): 1 bestand lezen,
/// 2 rekeningen controleren, 3 beginbalans controleren en importeren (na een back-up), 4 klaar. Een verschil in de
/// beginbalans wordt alleen na een bewuste keuze op een tussenrekening geboekt (besluit 73). Het Excel-bestand wordt
/// alleen gelezen, niet bewaard.
/// </summary>
public sealed partial class BoekhoudingImportViewModel : ObservableObject
{
    private readonly BoekhoudingDienst _dienst;
    private readonly IWerkbladLezer _lezer;
    private readonly AdministratieSoort _administratie;
    private readonly string _pad;
    private VoorbladVoorstel? _voorstel;

    public BoekhoudingImportViewModel(BoekhoudingDienst dienst, IWerkbladLezer lezer, AdministratieSoort administratie, string pad)
    {
        _dienst = dienst;
        _lezer = lezer;
        _administratie = administratie;
        _pad = pad;
        VolgendeCommand = new AsyncRelayCommand(VolgendeAsync, KanVolgende);
        VorigeCommand = new RelayCommand(Vorige, KanVorige);
    }

    public string Bestandsnaam => Path.GetFileName(_pad);

    public ObservableCollection<ImportRekeningRegel> Rekeningen { get; } = [];

    public Verzameling<ImportRekeningRegel> Beginbalans { get; } = [];

    public Verzameling<string> Meldingen { get; } = [];

    public IAsyncRelayCommand VolgendeCommand { get; }

    public IRelayCommand VorigeCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Volgendetekst), nameof(IsKlaar))]
    public partial BoekhoudingImportStap Stap { get; private set; }

    [ObservableProperty]
    public partial bool IsBezig { get; private set; }

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    [ObservableProperty]
    public partial bool VerschilBewustBoeken { get; set; }

    /// <summary>Na de overname ook de historie van het jaar zelf overnemen (besluit 125).</summary>
    [ObservableProperty]
    public partial bool OokHistorie { get; set; } = true;

    [ObservableProperty]
    public partial string Verschilnummer { get; set; } = VerschilKeuze.Standaard.Nummer.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial string Verschilnaam { get; set; } = VerschilKeuze.Standaard.Naam;

    [ObservableProperty]
    public partial bool VerschilGeboekt { get; private set; }

    [ObservableProperty]
    public partial string Samenvatting { get; private set; } = string.Empty;

    public bool IsKlaar => Stap == BoekhoudingImportStap.Klaar;

    public string Volgendetekst => Stap == BoekhoudingImportStap.Beginbalans ? "Importeren" : "Volgende";

    public string Beginbalanstitel => _voorstel is null ? "Beginbalans" : $"Beginbalans per 01-01-{_voorstel.Beginjaar.ToString(CultureInfo.InvariantCulture)}";

    public string TotaalDebettekst => Voorstel.TotaalDebet.ToString();

    public string TotaalCredittekst => Voorstel.TotaalCredit.ToString();

    public bool IsInEvenwicht => Voorstel.IsInEvenwicht;

    /// <summary>Het verschil tussen debet en credit, altijd positief weergegeven.</summary>
    public string Verschiltekst => new Bedrag(Math.Abs(Voorstel.TotaalDebet.Centen - Voorstel.TotaalCredit.Centen)).ToString();

    public string? Verschilfout => !VerschilBewustBoeken ? null
        : !int.TryParse(Verschilnummer, NumberStyles.None, CultureInfo.InvariantCulture, out var nummer) || nummer is < 1 or > 99999 ? "Vul een rekeningnummer in."
        : Rekeningen.FirstOrDefault(r => r.Nummer == nummer && !r.Overslaan) is { Soort: not RekeningSoort.Balans } ? "Kies een balansrekening."
        : Rekeningen.Any(r => r.Nummer == nummer && !r.Overslaan) || !string.IsNullOrWhiteSpace(Verschilnaam) ? null
        : "Vul de naam van de nieuwe rekening in.";

    private VoorbladVoorstel Voorstel => _voorstel is null
        ? new VoorbladVoorstel(0, [], [])
        : _voorstel with { Rekeningen = [.. Rekeningen.Select(r => r.Gekozen)] };

    private bool KanVolgende() => !IsBezig && Stap switch
    {
        BoekhoudingImportStap.Bestand or BoekhoudingImportStap.Rekeningen => true,
        BoekhoudingImportStap.Beginbalans => IsInEvenwicht || (VerschilBewustBoeken && Verschilfout is null),
        _ => false,
    };

    private bool KanVorige() => !IsBezig && Stap is BoekhoudingImportStap.Rekeningen or BoekhoudingImportStap.Beginbalans;

    private async Task VolgendeAsync()
    {
        IsBezig = true;
        Foutmelding = null;
        Vernieuw();
        try
        {
            switch (Stap)
            {
                case BoekhoudingImportStap.Bestand:
                    Lees();
                    Stap = BoekhoudingImportStap.Rekeningen;
                    break;
                case BoekhoudingImportStap.Rekeningen:
                    ToonBeginbalans();
                    Stap = BoekhoudingImportStap.Beginbalans;
                    break;
                case BoekhoudingImportStap.Beginbalans:
                    await ImporteerAsync();
                    Stap = BoekhoudingImportStap.Klaar;
                    break;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            Foutmelding = ex.Message;
        }
        finally
        {
            IsBezig = false;
            Vernieuw();
        }
    }

    private void Vorige()
    {
        Stap = Stap == BoekhoudingImportStap.Beginbalans ? BoekhoudingImportStap.Rekeningen : BoekhoudingImportStap.Bestand;
        Foutmelding = null;
        Vernieuw();
    }

    private void Lees()
    {
        var voorstel = VoorbladInlezer.Lees(_lezer.Lees(_pad, VoorbladInlezer.Werkbladnaam));
        if (voorstel.Rekeningen.Count == 0)
        {
            throw new InvalidOperationException("Het werkblad Voorblad bevat geen rekeningen.");
        }

        _voorstel = voorstel;
        foreach (var regel in Rekeningen)
        {
            regel.PropertyChanged -= RegelGewijzigd;
        }

        Rekeningen.Clear();
        foreach (var rekening in voorstel.Rekeningen)
        {
            var regel = new ImportRekeningRegel(rekening);
            regel.PropertyChanged += RegelGewijzigd;
            Rekeningen.Add(regel);
        }

        Meldingen.Vervang(voorstel.Meldingen);

        OnPropertyChanged(nameof(Beginbalanstitel));
    }

    private void ToonBeginbalans()
    {
        Beginbalans.Vervang(Rekeningen.Where(r => !r.Overslaan && (r.Bron.Debet != Bedrag.Nul || r.Bron.Credit != Bedrag.Nul)));

        VerschilBewustBoeken = false;
        Totalen();
    }

    private async Task ImporteerAsync()
    {
        var voorstel = Voorstel;
        VerschilKeuze? verschil = null;
        if (!voorstel.IsInEvenwicht)
        {
            var nummer = int.Parse(Verschilnummer, NumberStyles.None, CultureInfo.InvariantCulture);
            var naam = Rekeningen.FirstOrDefault(r => r.Nummer == nummer)?.Naam ?? Verschilnaam.Trim();
            verschil = new VerschilKeuze(nummer, naam);
        }

        await _dienst.ImporteerAsync(_administratie, voorstel, verschil);
        VerschilGeboekt = verschil is not null;
        var aantal = voorstel.Rekeningen.Count(r => !r.Overslaan);
        Samenvatting = string.Create(CultureInfo.CurrentCulture,
            $"{aantal} rekeningen en de beginbalans {voorstel.Beginjaar} zijn overgenomen (post MEM-{voorstel.Beginjaar}-0001). Vooraf is een back-up gemaakt.")
            + (verschil is null ? string.Empty : $" Het verschil van {Verschiltekst} staat op rekening {verschil.Nummer} {verschil.Naam}; zoek het uit en boek het met een memoriaalpost weg.");
    }

    internal int AantalOvergenomen => Rekeningen.Count(r => !r.Overslaan);

    internal string? Verschilrekening => VerschilGeboekt ? Verschilnummer : null;

    partial void OnVerschilBewustBoekenChanged(bool value)
    {
        OnPropertyChanged(nameof(Verschilfout));
        Vernieuw();
    }

    partial void OnVerschilnummerChanged(string value)
    {
        OnPropertyChanged(nameof(Verschilfout));
        Vernieuw();
    }

    partial void OnVerschilnaamChanged(string value)
    {
        OnPropertyChanged(nameof(Verschilfout));
        Vernieuw();
    }

    private void RegelGewijzigd(object? sender, PropertyChangedEventArgs e) => Totalen();

    private void Totalen()
    {
        OnPropertyChanged(nameof(TotaalDebettekst));
        OnPropertyChanged(nameof(TotaalCredittekst));
        OnPropertyChanged(nameof(IsInEvenwicht));
        OnPropertyChanged(nameof(Verschiltekst));
        OnPropertyChanged(nameof(Verschilfout));
        Vernieuw();
    }

    private void Vernieuw()
    {
        VolgendeCommand.NotifyCanExecuteChanged();
        VorigeCommand.NotifyCanExecuteChanged();
    }
}
