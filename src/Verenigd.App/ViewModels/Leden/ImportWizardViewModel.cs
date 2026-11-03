using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.App.Diensten;
using Verenigd.Application.Import;
using Verenigd.Domain.Leden;

namespace Verenigd.App.ViewModels.Leden;

public enum ImportStap
{
    Bestanden,
    Groepen,
    Controleren,
    Klaar,
}

/// <summary>Eén van de vier verplichte exports in stap 1.</summary>
public sealed partial class ExportBestandRegel(Sol3Export export) : ObservableObject
{
    public Sol3Export Export { get; } = export;

    /// <summary>Bijvoorbeeld "4 Functies".</summary>
    public string Omschrijving { get; } = export switch
    {
        Sol3Export.LedenBasis => "1  Leden-basis",
        Sol3Export.Huishouden => "2  Huishouden",
        Sol3Export.Voogden => "3  Voogden",
        _ => "4  Functies",
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bestandsnaam), nameof(IsGeldig), nameof(Statustekst))]
    public partial string? Pad { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Statustekst))]
    public partial int AantalRegels { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeldig), nameof(Statustekst))]
    public partial string? Fout { get; set; }

    public string? Bestandsnaam => Pad is null ? null : Path.GetFileName(Pad);

    public bool IsGeldig => Pad is not null && Fout is null;

    public string Statustekst => (Pad, Fout) switch
    {
        (null, _) => "Niet gekozen",
        (_, { } fout) => fout,
        _ => $"{AantalRegels.ToString(CultureInfo.CurrentCulture)} regels",
    };
}

public sealed partial class GroepKeuzeRegel(string naam, GroepSoort soort) : ObservableObject
{
    public string Naam { get; } = naam;

    [ObservableProperty]
    public partial GroepSoort Soort { get; set; } = soort;
}

public sealed record WijzigingRegel(string Lidnummer, string Naam, string Veld, string? Oud, string? Nieuw);

public sealed record MeldingRegel(string Export, string? Regel, string? Lidnummer, string Tekst);

public sealed partial class UitschrijfRegel(string lidnummer, string naam) : ObservableObject
{
    public string Lidnummer { get; } = lidnummer;
    public string Naam { get; } = naam;

    [ObservableProperty]
    public partial bool Uitschrijven { get; set; } = true;
}

/// <summary>Besluit 141: een afwijkend IBAN uit SOL3 alleen overnemen als het is aangevinkt (standaard uit).</summary>
public sealed partial class IbanKeuzeRegel(string lidnummer, string naam, string huidig, string sol3) : ObservableObject
{
    public string Lidnummer { get; } = lidnummer;
    public string Naam { get; } = naam;
    public string Huidig { get; } = huidig;
    public string Sol3 { get; } = sol3;

    [ObservableProperty]
    public partial bool Overnemen { get; set; }
}

/// <summary>
/// Importwizard SOL3: 1 bestanden kiezen, 2 nieuwe groepen indelen, 3 controleren (voorbeeldoverzicht), 4 klaar.
/// Pas in stap 3 wordt er (na een automatische back-up) iets gewijzigd.
/// </summary>
public sealed partial class ImportWizardViewModel : ObservableObject
{
    private readonly Sol3ImportDienst _dienst;
    private readonly IDialoogService _dialogen;
    private ImportAnalyse? _analyse;

    public ImportWizardViewModel(Sol3ImportDienst dienst, IDialoogService dialogen)
    {
        _dienst = dienst;
        _dialogen = dialogen;
        Bestanden = [.. Enum.GetValues<Sol3Export>().Select(e => new ExportBestandRegel(e))];
        VolgendeCommand = new AsyncRelayCommand(VolgendeAsync, KanVolgende);
        VorigeCommand = new RelayCommand(Vorige, KanVorige);
        WaarschuwingenOpslaanCommand = new AsyncRelayCommand(WaarschuwingenOpslaanAsync, () => Waarschuwingen.Count > 0);
    }

    public static IReadOnlyList<GroepSoort> Groepsoorten { get; } = Enum.GetValues<GroepSoort>();

    public IReadOnlyList<ExportBestandRegel> Bestanden { get; }

    public Verzameling<GroepKeuzeRegel> Groepen { get; } = [];

    public ObservableCollection<WijzigingRegel> Nieuw { get; } = [];

    public ObservableCollection<WijzigingRegel> Gewijzigd { get; } = [];

    public ObservableCollection<UitschrijfRegel> Uitgeschreven { get; } = [];

    public ObservableCollection<IbanKeuzeRegel> IbanVerschillen { get; } = [];

    public ObservableCollection<MeldingRegel> Waarschuwingen { get; } = [];

    public IAsyncRelayCommand VolgendeCommand { get; }

    public IRelayCommand VorigeCommand { get; }

    public IAsyncRelayCommand WaarschuwingenOpslaanCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Volgendetekst), nameof(Stapnummer), nameof(IsKlaar))]
    public partial ImportStap Stap { get; private set; }

    [ObservableProperty]
    public partial bool IsBezig { get; private set; }

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    [ObservableProperty]
    public partial ImportResultaat? Resultaat { get; private set; }

    public int Stapnummer => (int)Stap + 1;

    public bool IsKlaar => Stap == ImportStap.Klaar;

    public string Volgendetekst => Stap == ImportStap.Controleren ? "Importeren" : "Volgende";

    /// <summary>De import verandert niets (bijvoorbeeld twee keer dezelfde bestanden).</summary>
    public bool IsOngewijzigd => _analyse?.Voorstel.IsLeeg == true;

    public string NieuwKop => $"Nieuw ({Nieuw.Count.ToString(CultureInfo.CurrentCulture)})";

    public string GewijzigdKop => $"Gewijzigd ({Gewijzigd.Select(g => g.Lidnummer).Distinct().Count().ToString(CultureInfo.CurrentCulture)})";

    public string IbanVerschillenKop => $"IBAN-verschillen ({IbanVerschillen.Count.ToString(CultureInfo.CurrentCulture)})";

    public bool HeeftIbanVerschillen => IbanVerschillen.Count > 0;

    public string UitgeschrevenKop => $"Uitgeschreven ({Uitgeschreven.Count.ToString(CultureInfo.CurrentCulture)})";

    public string WaarschuwingenKop => $"Waarschuwingen ({Waarschuwingen.Count.ToString(CultureInfo.CurrentCulture)})";

    public string Samenvatting => Resultaat is not { } r
        ? string.Empty
        : string.Create(CultureInfo.CurrentCulture,
            $"{r.Nieuw} nieuw, {r.Gewijzigd} gewijzigd, {r.Uitgeschreven} uitgeschreven. Vóór de import is een back-up gemaakt: {Path.GetFileName(r.Backup)}");

    /// <summary>Gekozen of gesleepte bestanden; de export wordt herkend aan de kopregel.</summary>
    public void VoegBestandenToe(IEnumerable<string> paden)
    {
        ArgumentNullException.ThrowIfNull(paden);
        var fouten = new List<string>();
        foreach (var pad in paden)
        {
            var herkenning = Sol3ImportDienst.HerkenBestand(pad);
            if (herkenning.Export is not { } export)
            {
                fouten.Add($"{Path.GetFileName(pad)}: {herkenning.Fout}");
                continue;
            }

            var regel = Bestanden[(int)export - 1];
            regel.Pad = pad;
            regel.AantalRegels = herkenning.AantalRegels;
            regel.Fout = herkenning.Fout;
        }

        Foutmelding = fouten.Count == 0 ? null : string.Join(Environment.NewLine, fouten);
        _analyse = null;
        VolgendeCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void Bladeren() => VoegBestandenToe(_dialogen.KiesImportbestanden());

    private bool KanVolgende() => !IsBezig && Stap switch
    {
        ImportStap.Bestanden => Bestanden.All(b => b.IsGeldig),
        ImportStap.Groepen => true,
        ImportStap.Controleren => !IsOngewijzigd,
        _ => false,
    };

    private async Task VolgendeAsync()
    {
        IsBezig = true;
        Foutmelding = null;
        Vernieuw();
        try
        {
            switch (Stap)
            {
                case ImportStap.Bestanden:
                    await AnalyseerAsync();
                    break;
                case ImportStap.Groepen:
                    Stap = ImportStap.Controleren;
                    break;
                case ImportStap.Controleren:
                    await VoerUitAsync();
                    break;
            }
        }
        catch (Exception ex) when (ex is Sol3ImportException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Foutmelding = ex is Sol3ImportException ? ex.Message : $"De import is niet uitgevoerd; er is niets gewijzigd.\n\n{ex.Message}";
        }
        finally
        {
            IsBezig = false;
            Vernieuw();
        }
    }

    private async Task AnalyseerAsync()
    {
        _analyse = await _dienst.AnalyseerAsync(Bestanden.ToDictionary(b => b.Export, b => b.Pad!));
        var voorstel = _analyse.Voorstel;

        Groepen.Vervang((voorstel.NieuweGroepen).Select(groep => new GroepKeuzeRegel(groep.Naam, groep.VoorgesteldeSoort)));

        Vul(Nieuw, voorstel.Nieuw.Select(r => new WijzigingRegel(r.Lidnummer.Waarde, r.Naam, string.Empty, null, null)));
        Vul(Gewijzigd, voorstel.Gewijzigd.SelectMany(r => r.Wijzigingen.Select(w => new WijzigingRegel(r.Lidnummer.Waarde, r.Naam, w.Veld, w.Oud, w.Nieuw))));
        Vul(Uitgeschreven, voorstel.Uitgeschreven.Select(r => new UitschrijfRegel(r.Lidnummer.Waarde, r.Naam)));
        Vul(IbanVerschillen, voorstel.IbanVerschillen.Select(v => new IbanKeuzeRegel(v.Lidnummer.Waarde, v.Naam, v.Huidig.ToString(), v.Sol3.ToString())));
        Vul(Waarschuwingen, voorstel.Meldingen.Select(m => new MeldingRegel(
            m.Export is { } e ? ExportHerkenning.Omschrijving(e) : "Controle",
            m.Regel?.ToString(CultureInfo.InvariantCulture),
            m.Lidnummer,
            m.Tekst)));

        OnPropertyChanged(nameof(IsOngewijzigd));
        OnPropertyChanged(nameof(NieuwKop));
        OnPropertyChanged(nameof(GewijzigdKop));
        OnPropertyChanged(nameof(UitgeschrevenKop));
        OnPropertyChanged(nameof(IbanVerschillenKop));
        OnPropertyChanged(nameof(HeeftIbanVerschillen));
        OnPropertyChanged(nameof(WaarschuwingenKop));
        WaarschuwingenOpslaanCommand.NotifyCanExecuteChanged();
        Stap = Groepen.Count > 0 ? ImportStap.Groepen : ImportStap.Controleren;
    }

    private async Task VoerUitAsync()
    {
        var keuzes = new ImportKeuzes(
            Groepen.ToDictionary(g => g.Naam, g => g.Soort, StringComparer.Ordinal),
            _analyse!.Voorstel.Uitgeschreven
                .Where(r => Uitgeschreven.Any(u => u.Uitschrijven && u.Lidnummer == r.Lidnummer.Waarde))
                .Select(r => r.Lidnummer)
                .ToHashSet())
        {
            IbanOvernemen = _analyse.Voorstel.IbanVerschillen
                .Where(v => IbanVerschillen.Any(r => r.Overnemen && r.Lidnummer == v.Lidnummer.Waarde))
                .Select(v => v.Lidnummer)
                .ToHashSet(),
        };
        Resultaat = await _dienst.VoerUitAsync(_analyse, keuzes);
        OnPropertyChanged(nameof(Samenvatting));
        Stap = ImportStap.Klaar;
    }

    private bool KanVorige() => !IsBezig && Stap is ImportStap.Groepen or ImportStap.Controleren;

    private void Vorige()
    {
        Stap = Stap == ImportStap.Controleren && Groepen.Count > 0 ? ImportStap.Groepen : ImportStap.Bestanden;
        Foutmelding = null;
        Vernieuw();
    }

    private async Task WaarschuwingenOpslaanAsync()
    {
        var doel = _dialogen.KiesCsvDoel("importwaarschuwingen.csv");
        if (doel is null)
        {
            return;
        }

        var tekst = new StringBuilder();
        tekst.AppendLine(Regel("Export", "Regel", "Lidnummer", "Melding"));
        foreach (var melding in Waarschuwingen)
        {
            tekst.AppendLine(Regel(melding.Export, melding.Regel, melding.Lidnummer, melding.Tekst));
        }

        try
        {
            await File.WriteAllTextAsync(doel, tekst.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogen.ToonFout("Waarschuwingen opslaan", $"Het bestand kon niet worden opgeslagen.\n\n{ex.Message}");
        }

        static string Regel(params string?[] velden) =>
            string.Join(';', velden.Select(v => "\"" + (v ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal) + "\""));
    }

    private void Vernieuw()
    {
        VolgendeCommand.NotifyCanExecuteChanged();
        VorigeCommand.NotifyCanExecuteChanged();
    }

    private static void Vul<T>(ObservableCollection<T> doel, IEnumerable<T> items)
    {
        doel.Clear();
        foreach (var item in items)
        {
            doel.Add(item);
        }
    }
}
