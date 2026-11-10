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

public enum InrichtStap
{
    Rekeningen,
    Beginsaldi,
    Klaar,
}

/// <summary>Eén rekening uit het sjabloon: aan of uit, naam aan te passen en (bij een balansrekening) een beginsaldo.</summary>
public sealed partial class InrichtRekeningRegel : ObservableObject
{
    public InrichtRekeningRegel(Inrichtrekening bron)
    {
        Bron = bron;
        Naam = bron.Naam;
    }

    public Inrichtrekening Bron { get; }
    public int Nummer => Bron.Nummer;
    public string Rubriek => Bron.Rubriek ?? string.Empty;
    public bool IsBalans => Bron.Soort == RekeningSoort.Balans;
    public string Soorttekst => IsBalans ? "Balans" : "Resultaat";
    public string Roltekst => Bron.Rol == RekeningRol.Geen ? string.Empty : Bron.Rol.ToString();

    /// <summary>De reserverekening is nodig voor de jaarafsluiting en kan niet uit.</summary>
    public bool IsVerplicht => Bron.Rol == RekeningRol.Reserve;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout))]
    public partial string Naam { get; set; }

    [ObservableProperty]
    public partial bool IsGekozen { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout))]
    public partial string Debettekst { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout))]
    public partial string Credittekst { get; set; } = string.Empty;

    public Bedrag? Debet => Lees(Debettekst);
    public Bedrag? Credit => Lees(Credittekst);

    public string? Fout =>
        string.IsNullOrWhiteSpace(Naam) || Naam.Trim().Length > Grootboekrekening.MaximaleNaamLengte ? $"Rekening {Nummer}: vul een naam in."
        : Debet is null || Credit is null ? $"Rekening {Nummer}: vul een bedrag in als 1.234,56, of laat het leeg."
        : null;

    internal Inrichtrekening Gekozen => Bron with
    {
        Naam = Naam.Trim(), Overslaan = !IsGekozen, Debet = IsBalans ? Debet ?? Bedrag.Nul : Bedrag.Nul, Credit = IsBalans ? Credit ?? Bedrag.Nul : Bedrag.Nul,
    };

    partial void OnIsGekozenChanged(bool value)
    {
        if (!value && IsVerplicht)
        {
            IsGekozen = true;
        }
    }

    private static Bedrag? Lees(string tekst) =>
        string.IsNullOrWhiteSpace(tekst) ? Bedrag.Nul : Bedrag.TryParse(tekst, out var bedrag) ? bedrag : null;
}

/// <summary>
/// Inrichten met een rekeningschema-sjabloon: 1 rekeningen aan/uit en namen, 2 beginsaldi met de controle debet = credit
/// (een verschil alleen bewust op een tussenrekening), 3 klaar. Het inrichten zelf loopt via BoekhoudingDienst.ImporteerAsync.
/// </summary>
public sealed partial class SjablooninrichtingViewModel : ObservableObject
{
    private readonly BoekhoudingDienst _dienst;
    private readonly AdministratieSoort _administratie;
    private readonly Rekeningsjabloon _sjabloon;

    public SjablooninrichtingViewModel(BoekhoudingDienst dienst, AdministratieSoort administratie, Rekeningsjabloon sjabloon, int beginjaar)
    {
        ArgumentNullException.ThrowIfNull(sjabloon);
        _dienst = dienst;
        _administratie = administratie;
        _sjabloon = sjabloon;
        Beginjaren = [.. Enumerable.Range(beginjaar - 5, 6)];
        Beginjaar = beginjaar;
        VolgendeCommand = new AsyncRelayCommand(VolgendeAsync, KanVolgende);
        VorigeCommand = new RelayCommand(Vorige, KanVorige);
        VulRekeningen();
    }

    public string Sjabloonnaam => _sjabloon.Naam;

    /// <summary>Besluit 198: ook beginnen in een eerder jaar (tot 5 jaar terug), nooit in de toekomst.</summary>
    public IReadOnlyList<int> Beginjaren { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Beginsaldititel))]
    public partial int Beginjaar { get; set; }

    /// <summary>Gevuld als het sjabloon voor de andere administratie bedoeld is.</summary>
    public string? Waarschuwing => _sjabloon.Administratie is { } soort && soort != _administratie
        ? $"Dit sjabloon is bedoeld voor de {soort}; controleer de rekeningen goed."
        : null;

    public ObservableCollection<InrichtRekeningRegel> Rekeningen { get; } = [];

    public Verzameling<InrichtRekeningRegel> Balansrekeningen { get; } = [];

    public IAsyncRelayCommand VolgendeCommand { get; }

    public IRelayCommand VorigeCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Volgendetekst), nameof(IsKlaar))]
    public partial InrichtStap Stap { get; private set; }

    [ObservableProperty]
    public partial bool IsBezig { get; private set; }

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    [ObservableProperty]
    public partial bool VerschilBewustBoeken { get; set; }

    [ObservableProperty]
    public partial string Verschilnummer { get; set; } = VerschilKeuze.Standaard.Nummer.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial string Verschilnaam { get; set; } = VerschilKeuze.Standaard.Naam;

    [ObservableProperty]
    public partial string Samenvatting { get; private set; } = string.Empty;

    public bool IsKlaar => Stap == InrichtStap.Klaar;

    /// <summary>
    /// Besluit 244: vraagt vóór het boeken van de beginsaldi om bevestiging (titel, vraag); <c>false</c> laat alles staan.
    /// Zonder bevestiging (bijvoorbeeld in tests) wordt direct geboekt.
    /// </summary>
    public Func<string, string, bool>? Bevestiging { get; set; }

    /// <summary>De vraag in het bevestigingsvenster: beginjaar, totalen en dat dit definitief is.</summary>
    public string Bevestigingsvraag => string.Create(CultureInfo.CurrentCulture,
        $"Het boekjaar begint op 1 januari {Beginjaar} met deze beginsaldi:\n\nTotaal debet: {TotaalDebettekst}\nTotaal credit: {TotaalCredittekst}\n\nDit is definitief. Het beginjaar en de beginsaldi worden de openingsbalans en kunnen daarna niet meer worden gewijzigd. Een fout herstelt u later met een memoriaalboeking.\n\nWilt u de boekhouding zo inrichten?");

    public string Volgendetekst => Stap == InrichtStap.Beginsaldi ? "Inrichten" : "Volgende";

    public string Beginsaldititel => $"Beginsaldi per 01-01-{Beginjaar.ToString(CultureInfo.InvariantCulture)}";

    public string? Rekeningfout => Rekeningen.Where(r => r.IsGekozen).Select(r => r.Fout).FirstOrDefault(f => f is not null);

    public string TotaalDebettekst => Voorstel.TotaalDebet.ToString();

    public string TotaalCredittekst => Voorstel.TotaalCredit.ToString();

    public bool IsInEvenwicht => Voorstel.IsInEvenwicht;

    public string Verschiltekst => new Bedrag(Math.Abs(Voorstel.TotaalDebet.Centen - Voorstel.TotaalCredit.Centen)).ToString();

    public string? Verschilfout => !VerschilBewustBoeken ? null
        : !int.TryParse(Verschilnummer, NumberStyles.None, CultureInfo.InvariantCulture, out var nummer) || nummer is < 1 or > 99999 ? "Vul een rekeningnummer in."
        : Rekeningen.FirstOrDefault(r => r.Nummer == nummer && r.IsGekozen) is { IsBalans: false } ? "Kies een balansrekening."
        : Rekeningen.Any(r => r.Nummer == nummer && r.IsGekozen) || !string.IsNullOrWhiteSpace(Verschilnaam) ? null
        : "Vul de naam van de nieuwe rekening in.";

    private Inrichtvoorstel Voorstel => Rekeningsjablonen.Voorstel([.. Rekeningen.Where(r => r.Fout is null).Select(r => r.Gekozen)], Beginjaar);

    private bool KanVolgende() => !IsBezig && Stap switch
    {
        InrichtStap.Rekeningen => Rekeningfout is null,
        InrichtStap.Beginsaldi => Rekeningfout is null && (IsInEvenwicht || (VerschilBewustBoeken && Verschilfout is null)),
        _ => false,
    };

    private bool KanVorige() => !IsBezig && Stap == InrichtStap.Beginsaldi;

    private async Task VolgendeAsync()
    {
        IsBezig = true;
        Foutmelding = null;
        Vernieuw();
        try
        {
            switch (Stap)
            {
                case InrichtStap.Rekeningen:
                    Balansrekeningen.Vervang(Rekeningen.Where(r => r.IsGekozen && r.IsBalans));

                    VerschilBewustBoeken = false;
                    Stap = InrichtStap.Beginsaldi;
                    Totalen();
                    break;
                case InrichtStap.Beginsaldi:
                    if (Bevestiging is { } bevestig && !bevestig("Beginsaldi definitief boeken", Bevestigingsvraag))
                    {
                        break;
                    }

                    await RichtInAsync();
                    Stap = InrichtStap.Klaar;
                    break;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException)
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
        Stap = InrichtStap.Rekeningen;
        Foutmelding = null;
        Vernieuw();
    }

    private void VulRekeningen()
    {
        foreach (var bron in Rekeningsjablonen.Rekeningen(_sjabloon))
        {
            var regel = new InrichtRekeningRegel(bron);
            regel.PropertyChanged += RegelGewijzigd;
            Rekeningen.Add(regel);
        }

        Totalen();
    }

    private async Task RichtInAsync()
    {
        var voorstel = Voorstel;
        VerschilKeuze? verschil = null;
        if (!voorstel.IsInEvenwicht)
        {
            var nummer = int.Parse(Verschilnummer, NumberStyles.None, CultureInfo.InvariantCulture);
            verschil = new VerschilKeuze(nummer, Rekeningen.FirstOrDefault(r => r.Nummer == nummer)?.Naam.Trim() ?? Verschilnaam.Trim());
        }

        await _dienst.ImporteerAsync(_administratie, voorstel, verschil);
        AantalIngericht = voorstel.Rekeningen.Count(r => !r.Overslaan);
        var opening = voorstel.TotaalDebet != Bedrag.Nul || voorstel.TotaalCredit != Bedrag.Nul;
        Samenvatting = string.Create(CultureInfo.CurrentCulture, $"{AantalIngericht} rekeningen zijn ingericht en boekjaar {Beginjaar} is geopend")
            + (opening ? $", met de beginsaldi als openingsbalans (post MEM-{Beginjaar}-0001)." : ". Er zijn geen beginsaldi ingevoerd.")
            + " Vooraf is een back-up gemaakt."
            + (verschil is null ? string.Empty : $" Het verschil van {Verschiltekst} staat op rekening {verschil.Nummer} {verschil.Naam}; zoek het uit en boek het met een memoriaalpost weg.");
    }

    internal int AantalIngericht { get; private set; }

    partial void OnVerschilBewustBoekenChanged(bool value) => Totalen();

    partial void OnVerschilnummerChanged(string value) => Totalen();

    partial void OnVerschilnaamChanged(string value) => Totalen();

    private void RegelGewijzigd(object? sender, PropertyChangedEventArgs e) => Totalen();

    private void Totalen()
    {
        OnPropertyChanged(nameof(TotaalDebettekst));
        OnPropertyChanged(nameof(TotaalCredittekst));
        OnPropertyChanged(nameof(IsInEvenwicht));
        OnPropertyChanged(nameof(Verschiltekst));
        OnPropertyChanged(nameof(Verschilfout));
        OnPropertyChanged(nameof(Rekeningfout));
        Vernieuw();
    }

    private void Vernieuw()
    {
        VolgendeCommand?.NotifyCanExecuteChanged();
        VorigeCommand?.NotifyCanExecuteChanged();
    }
}
