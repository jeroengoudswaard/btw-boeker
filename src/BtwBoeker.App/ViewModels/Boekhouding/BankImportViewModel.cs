using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtwBoeker.Application.Bank;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.ViewModels.Boekhouding;

public enum BankImportStap
{
    Bestanden,
    Controleren,
    Klaar,
}

/// <summary>Eén eigen bankrekening in de importwizard; bij een nieuwe IBAN kiest de gebruiker grootboekrekening en dagboekcode.</summary>
public sealed partial class BankImportRekeningRegel : ObservableObject
{
    private readonly BoekhoudingStand _stand;

    public BankImportRekeningRegel(BankimportRekening import, BoekhoudingStand stand, string voorgesteldeCode)
    {
        ArgumentNullException.ThrowIfNull(import);
        Import = import;
        _stand = stand;
        Code = import.Dagboek?.Code ?? voorgesteldeCode;
        Naam = import.Dagboek?.Naam ?? $"Bankrekening {import.Iban.Waarde[^4..]}";
        GekozenRekening = import.Dagboek?.Rekening;
        Meldingen = [.. import.Meldingen];
        VernieuwBeginsaldo();
    }

    public BankimportRekening Import { get; }
    public string Iban => Import.Iban.Waarde;
    public string Ibantekst => Import.Iban.ToString();
    public bool IsNieuw => Import.Dagboek is null;
    public string Periode => Import.Van is null ? "—" : $"{Weergave.Datum(Import.Van)} t/m {Weergave.Datum(Import.Tot)}";
    public int AantalNieuw => Import.Nieuw.Count;
    public int AlBekend => Import.AlBekend;
    public int BuitenBoekjaar => Import.BuitenBoekjaar;
    public Verzameling<string> Meldingen { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout))]
    public partial string Code { get; set; }

    [ObservableProperty]
    public partial string Naam { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout))]
    public partial Grootboekrekening? GekozenRekening { get; set; }

    /// <summary>Wordt door de wizard gezet als een andere nieuwe rekening dezelfde code kiest.</summary>
    internal bool CodeDubbel { get; set; }

    public string? Fout => !IsNieuw ? null
        : GekozenRekening is null ? "Kies de grootboekrekening van deze bankrekening."
        : Code?.Trim() is not { Length: >= 2 and <= 5 } c || !c.All(char.IsAsciiLetterOrDigit) ? "Een dagboekcode heeft 2–5 letters of cijfers."
        : CodeDubbel || _stand.Dagboeken.Any(d => string.Equals(d.Code, c, StringComparison.OrdinalIgnoreCase)) ? $"Dagboekcode {c.ToUpperInvariant()} bestaat al."
        : null;

    internal NieuweBankrekening Keuze => new(GekozenRekening!.Id, Code.Trim(), Naam);

    partial void OnGekozenRekeningChanged(Grootboekrekening? value) => VernieuwBeginsaldo();

    internal void VernieuwFout() => OnPropertyChanged(nameof(Fout));

    /// <summary>Besluit 80: waarschuwen als het eerste beginsaldo niet gelijk is aan het grootboek.</summary>
    private void VernieuwBeginsaldo()
    {
        Meldingen.Vervang(Import.Meldingen);

        if (GekozenRekening is { } rekening && BankimportPlanner.ControleerBeginsaldo(_stand, rekening, Import) is { } waarschuwing)
        {
            Meldingen.Add(waarschuwing);
        }
    }
}

/// <summary>
/// Wizard voor het inlezen van CAMT.053-bankafschriften: 1 bestanden lezen, 2 controleren (nieuw/al bekend, saldi, nieuwe
/// eigen rekening koppelen), 3 inlezen na een back-up. Herkende transacties worden concept (besluit 74), de rest komt in
/// de werkvoorraad (besluit 75).
/// </summary>
public sealed partial class BankImportViewModel : ObservableObject
{
    private readonly BankDienst _dienst;
    private readonly BoekhoudingStand _stand;
    private readonly AdministratieSoort _administratie;
    private readonly IReadOnlyList<string> _paden;
    private BankimportVoorstel? _voorstel;

    public BankImportViewModel(BankDienst dienst, BoekhoudingStand stand, AdministratieSoort administratie, IReadOnlyList<string> paden)
    {
        ArgumentNullException.ThrowIfNull(stand);
        _dienst = dienst;
        _stand = stand;
        _administratie = administratie;
        _paden = paden;
        Balansrekeningen = [.. stand.Rekeningen.Where(r => r.Soort == RekeningSoort.Balans && r.IsActief).OrderBy(r => r.Nummer)];
        VolgendeCommand = new AsyncRelayCommand(VolgendeAsync, KanVolgende);
        VorigeCommand = new RelayCommand(() => { Stap = BankImportStap.Bestanden; Vernieuw(); }, () => !IsBezig && Stap == BankImportStap.Controleren);
    }

    public IReadOnlyList<string> Bestandsnamen => [.. _paden.Select(Path.GetFileName).OfType<string>()];

    public IReadOnlyList<Grootboekrekening> Balansrekeningen { get; }

    public ObservableCollection<BankImportRekeningRegel> Rekeningen { get; } = [];

    public Verzameling<string> Meldingen { get; } = [];

    public IAsyncRelayCommand VolgendeCommand { get; }

    public IRelayCommand VorigeCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Volgendetekst), nameof(IsKlaar))]
    public partial BankImportStap Stap { get; private set; }

    [ObservableProperty]
    public partial bool IsBezig { get; private set; }

    [ObservableProperty]
    public partial string? Foutmelding { get; private set; }

    [ObservableProperty]
    public partial BankimportResultaat? Resultaat { get; private set; }

    [ObservableProperty]
    public partial string Samenvatting { get; private set; } = string.Empty;

    public bool IsKlaar => Stap == BankImportStap.Klaar;

    public string Volgendetekst => Stap == BankImportStap.Controleren ? "Inlezen" : "Volgende";

    public string Totaaltekst => _voorstel is null ? string.Empty : string.Create(CultureInfo.CurrentCulture,
        $"{_voorstel.AantalNieuw} nieuwe transacties, {_voorstel.Rekeningen.Sum(r => r.AlBekend)} al ingelezen, {_voorstel.Rekeningen.Sum(r => r.BuitenBoekjaar)} buiten een open boekjaar");

    private bool KanVolgende() => !IsBezig && Stap switch
    {
        BankImportStap.Bestanden => true,
        BankImportStap.Controleren => Rekeningen.Count > 0 && Rekeningen.All(r => r.Fout is null),
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
                case BankImportStap.Bestanden:
                    Lees();
                    Stap = BankImportStap.Controleren;
                    break;
                case BankImportStap.Controleren:
                    await InlezenAsync();
                    Stap = BankImportStap.Klaar;
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

    private void Lees()
    {
        var bestanden = new List<CamtBestand>();
        foreach (var pad in _paden)
        {
            try
            {
                using var stroom = File.OpenRead(pad);
                bestanden.Add(Camt053Lezer.Lees(stroom));
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException($"{Path.GetFileName(pad)}: {ex.Message}", ex);
            }
        }

        _voorstel = BankimportPlanner.Plan(_stand, bestanden);
        foreach (var regel in Rekeningen)
        {
            regel.PropertyChanged -= RegelGewijzigd;
        }

        Rekeningen.Clear();
        var codes = _stand.Dagboeken.Select(d => d.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var import in _voorstel.Rekeningen)
        {
            var regel = new BankImportRekeningRegel(import, _stand, import.Dagboek is null ? VoorgesteldeCode(import.Iban, codes) : import.Dagboek.Code);
            regel.PropertyChanged += RegelGewijzigd;
            Rekeningen.Add(regel);
        }

        Meldingen.Vervang(_voorstel.Meldingen);

        OnPropertyChanged(nameof(Totaaltekst));
        ControleerCodes();
    }

    private async Task InlezenAsync()
    {
        var nieuw = Rekeningen.Where(r => r.IsNieuw).ToDictionary(r => r.Iban, r => r.Keuze, StringComparer.Ordinal);
        Resultaat = await _dienst.ImporteerAsync(_administratie, _voorstel!, nieuw);
        Samenvatting = Resultaat.Nieuw == 0
            ? "Er waren geen nieuwe transacties; alles was al ingelezen."
            : string.Create(CultureInfo.CurrentCulture,
                $"{Resultaat.Nieuw} nieuwe transacties: {Resultaat.Herkend} herkend als concept, {Resultaat.Werkvoorraad} in de werkvoorraad. Vooraf is een back-up gemaakt.");
    }

    /// <summary>De bankcode uit het IBAN (bijvoorbeeld RABO), met een cijfer erachter als die al bestaat.</summary>
    private static string VoorgesteldeCode(Iban iban, HashSet<string> codes)
    {
        var basis = iban.Waarde.Length >= 8 && iban.Waarde[4..8].All(char.IsAsciiLetter) ? iban.Waarde[4..8] : "BANK";
        var code = basis;
        for (var i = 2; !codes.Add(code); i++)
        {
            code = basis + i.ToString(CultureInfo.InvariantCulture);
        }

        return code;
    }

    private void RegelGewijzigd(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BankImportRekeningRegel.Code))
        {
            ControleerCodes();
        }

        Vernieuw();
    }

    private void ControleerCodes()
    {
        var nieuw = Rekeningen.Where(r => r.IsNieuw).ToList();
        foreach (var regel in nieuw)
        {
            regel.CodeDubbel = nieuw.Exists(r => !ReferenceEquals(r, regel) && string.Equals(r.Code?.Trim(), regel.Code?.Trim(), StringComparison.OrdinalIgnoreCase));
            regel.VernieuwFout();
        }
    }

    private void Vernieuw()
    {
        VolgendeCommand.NotifyCanExecuteChanged();
        VorigeCommand.NotifyCanExecuteChanged();
    }
}
