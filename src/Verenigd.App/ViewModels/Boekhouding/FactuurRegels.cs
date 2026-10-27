using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Verenigd.App.ViewModels.Contributie;
using Verenigd.Application.Facturen;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Domain.Financien.Facturen;
using Verenigd.Domain.Financien.Relaties;

namespace Verenigd.App.ViewModels.Boekhouding;

/// <summary>Filter van het subtabblad Facturen (besluit 109).</summary>
public enum Factuurfilter
{
    Open,
    Vervallen,
    Concept,
    Alle,
}

/// <summary>Eén relatie in het subtabblad Relaties; een nieuwe relatie heeft nog geen Id.</summary>
public sealed partial class RelatieRegel : ObservableObject
{
    private readonly string _origineel;

    public RelatieRegel(Relatie? relatie)
    {
        Id = relatie?.Id;
        Naam = relatie?.Naam ?? string.Empty;
        Contactpersoon = relatie?.Contactpersoon ?? string.Empty;
        Straat = relatie?.Straat ?? string.Empty;
        Postcode = relatie?.Postcode ?? string.Empty;
        Plaats = relatie?.Plaats ?? string.Empty;
        Email = relatie?.Email ?? string.Empty;
        Telefoon = relatie?.Telefoon ?? string.Empty;
        Iban = relatie?.Iban is { } iban ? Domain.Financien.Iban.Maak(iban).ToString() : string.Empty;
        IsDebiteur = relatie?.IsDebiteur ?? true;
        IsCrediteur = relatie?.IsCrediteur ?? false;
        Notitie = relatie?.Notitie ?? string.Empty;
        IsActief = relatie?.IsActief ?? true;
        _origineel = Momentopname();
    }

    public Guid? Id { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string Naam { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Contactpersoon { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Straat { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Postcode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Plaats { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string Email { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Telefoon { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string Iban { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial bool IsDebiteur { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial bool IsCrediteur { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Notitie { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial bool IsActief { get; set; }

    public string Rol => (IsDebiteur, IsCrediteur) switch
    {
        (true, true) => "Debiteur en crediteur",
        (false, true) => "Crediteur",
        _ => "Debiteur",
    };

    public bool IsGewijzigd => !string.Equals(Momentopname(), _origineel, StringComparison.Ordinal);

    /// <summary>De controle van het domein (naam, IBAN-controlegetal, e-mail, rol).</summary>
    public string? Fout
    {
        get
        {
            try
            {
                _ = new Relatie(Gegevens);
                return null;
            }
            catch (ArgumentException ex)
            {
                return Meldingen.Zonder(ex);
            }
        }
    }

    internal RelatieInvoer Invoer => new(Id, Gegevens, IsActief);

    private RelatieGegevens Gegevens => new(Naam, Contactpersoon, Straat, Postcode, Plaats, Email, Telefoon, Iban, IsDebiteur, IsCrediteur, Notitie);

    private string Momentopname() =>
        string.Join('|', Naam?.Trim(), Contactpersoon?.Trim(), Straat?.Trim(), Postcode?.Trim(), Plaats?.Trim(), Email?.Trim(), Telefoon?.Trim(),
            Iban?.Trim(), IsDebiteur, IsCrediteur, Notitie?.Trim(), IsActief);
}

/// <summary>Eén factuur in de lijst van het subtabblad Facturen.</summary>
public sealed record FactuurRegel(Factuurstand Stand)
{
    public Guid Id => Stand.Factuur.Id;
    public Verkoopfactuur Factuur => Stand.Factuur;
    public string Nummer => Stand.Factuur.Nummer ?? "Concept";
    public string Datum => Weergave.Datum(Stand.Factuur.Factuurdatum);
    public string Vervaldatum => Stand.Factuur.IsCreditfactuur ? string.Empty : Weergave.Datum(Stand.Factuur.Vervaldatum);
    public string Relatie => Stand.Relatie?.Naam ?? string.Empty;
    public string Totaal => Stand.Factuur.Totaal.ToString();
    public string Open => Stand.IsOpen ? new Bedrag(Stand.Saldo).ToString() : string.Empty;
    public string Status => Stand.Statustekst;
    public bool IsVervallen => Stand.IsVervallen;
    public bool IsConcept => !Stand.Factuur.IsDefinitief;
    public bool KanCrediteren => Stand.Factuur.IsDefinitief && !Stand.Factuur.IsCreditfactuur && !Stand.IsGecrediteerd;
}

/// <summary>Eén regel van een conceptfactuur.</summary>
public sealed partial class FactuurregelRij : ObservableObject
{
    public FactuurregelRij(Factuurregel? regel)
    {
        Omschrijving = regel?.Omschrijving ?? string.Empty;
        Aantal = (regel?.Aantal ?? 1m).ToString("0.##", CultureInfo.GetCultureInfo("nl-NL"));
        Prijs = regel is null ? string.Empty : Invoertekst(regel.Prijs);
        Rekening = regel?.Rekening;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout))]
    public partial string Omschrijving { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(Bedrag), nameof(Bedragtekst))]
    public partial string Aantal { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(Bedrag), nameof(Bedragtekst))]
    public partial string Prijs { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout))]
    public partial Grootboekrekening? Rekening { get; set; }

    public decimal? Aantalwaarde =>
        decimal.TryParse(Aantal, NumberStyles.Number, CultureInfo.GetCultureInfo("nl-NL"), out var a) && a != 0 ? a : null;

    public Bedrag? Prijswaarde => Domain.Financien.Bedrag.TryParse(Prijs?.Replace("-", string.Empty, StringComparison.Ordinal), out var p)
        ? (Prijs!.TrimStart().StartsWith('-') ? new Bedrag(-p.Centen) : p)
        : null;

    public long? Bedrag => Aantalwaarde is { } a && Prijswaarde is { } p ? (long)Math.Round(a * p.Centen, MidpointRounding.AwayFromZero) : null;

    public string Bedragtekst => Bedrag is { } b ? new Bedrag(b).ToString() : string.Empty;

    public string? Fout =>
        string.IsNullOrWhiteSpace(Omschrijving) ? "Vul de omschrijving in."
        : Aantalwaarde is null ? "Vul een aantal in, bijvoorbeeld 1 of 2,5."
        : Prijswaarde is null ? "Vul een prijs in, bijvoorbeeld 125,00."
        : Rekening is null ? "Kies de opbrengstrekening."
        : null;

    internal static string Invoertekst(Bedrag bedrag) =>
        (bedrag.Centen < 0 ? "-" : string.Empty) + (Math.Abs(bedrag.Centen) / 100m).ToString("0.00", CultureInfo.GetCultureInfo("nl-NL"));
}

/// <summary>Bewerken van een conceptfactuur (besluit 66); een definitieve factuur is onveranderlijk.</summary>
public sealed partial class FactuurEditor : ObservableObject
{
    private readonly string _origineel;

    public FactuurEditor(Verkoopfactuur? factuur, IReadOnlyList<Relatie> relaties, IReadOnlyList<Grootboekrekening> rekeningen, DateOnly vandaag, int termijn)
    {
        Id = factuur?.Id;
        IsCreditfactuur = factuur?.IsCreditfactuur ?? false;
        Relatiekeuzes = [.. relaties.Where(r => (r.IsActief && r.IsDebiteur) || r.Id == factuur?.RelatieId)];
        Rekeningkeuzes = [.. rekeningen.Where(r => r.IsActief && r.Soort == RekeningSoort.Resultaat)];
        Relatie = Relatiekeuzes.FirstOrDefault(r => r.Id == factuur?.RelatieId);
        Datum = (factuur?.Factuurdatum ?? vandaag).ToDateTime(TimeOnly.MinValue);
        Termijn = (factuur?.Termijn ?? termijn).ToString(CultureInfo.InvariantCulture);
        foreach (var regel in factuur?.Regels ?? [])
        {
            Voeg(new FactuurregelRij(regel));
        }

        if (Regels.Count == 0)
        {
            Voeg(new FactuurregelRij(null));
        }

        _origineel = Momentopname();
    }

    public Guid? Id { get; }

    public bool IsCreditfactuur { get; }

    public string Titel => IsCreditfactuur ? "Creditfactuur (concept)" : Id is null ? "Nieuwe factuur" : "Factuur (concept)";

    public IReadOnlyList<Relatie> Relatiekeuzes { get; }

    public IReadOnlyList<Grootboekrekening> Rekeningkeuzes { get; }

    public ObservableCollection<FactuurregelRij> Regels { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fouten))]
    public partial Relatie? Relatie { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fouten))]
    public partial DateTime? Datum { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fouten))]
    public partial string Termijn { get; set; }

    public int? Termijnwaarde => int.TryParse(Termijn, NumberStyles.None, CultureInfo.InvariantCulture, out var t) && t <= 365 ? t : null;

    public string Totaaltekst => new Bedrag(Regels.Sum(r => r.Bedrag ?? 0)).ToString();

    public bool IsGewijzigd => !string.Equals(Momentopname(), _origineel, StringComparison.Ordinal);

    public IReadOnlyList<string> Fouten
    {
        get
        {
            var fouten = new List<string>();
            if (Relatie is null)
            {
                fouten.Add("Kies de relatie.");
            }

            if (Datum is null)
            {
                fouten.Add("Vul de factuurdatum in.");
            }

            if (Termijnwaarde is null)
            {
                fouten.Add("Vul een betaaltermijn van 0 tot en met 365 dagen in.");
            }

            fouten.AddRange(Regels.Select((r, i) => r.Fout is { } f ? $"Regel {i + 1}: {f}" : null).OfType<string>());
            return fouten;
        }
    }

    public string? Fouttekst => Fouten.Count == 0 ? null : string.Join(Environment.NewLine, Fouten);

    internal FactuurInvoer Invoer => new(Id, Relatie!.Id, DateOnly.FromDateTime(Datum!.Value), Termijnwaarde!.Value,
        [.. Regels.Select(r => new FactuurregelInvoerId(r.Omschrijving.Trim(), r.Aantalwaarde!.Value, r.Prijswaarde!.Value, r.Rekening!.Id))]);

    [RelayCommand]
    private void RegelToevoegen() => Voeg(new FactuurregelRij(null) { Rekening = Regels.LastOrDefault()?.Rekening });

    [RelayCommand]
    private void RegelVerwijderen(FactuurregelRij? regel)
    {
        if (regel is not null && Regels.Count > 1)
        {
            regel.PropertyChanged -= RegelGewijzigd;
            Regels.Remove(regel);
            RegelGewijzigd(null, new PropertyChangedEventArgs(null));
        }
    }

    private void Voeg(FactuurregelRij regel)
    {
        regel.PropertyChanged += RegelGewijzigd;
        Regels.Add(regel);
        RegelGewijzigd(null, new PropertyChangedEventArgs(null));
    }

    private void RegelGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Totaaltekst));
        OnPropertyChanged(nameof(IsGewijzigd));
        OnPropertyChanged(nameof(Fouten));
        OnPropertyChanged(nameof(Fouttekst));
    }

    partial void OnRelatieChanged(Relatie? value) => OnPropertyChanged(nameof(Fouttekst));

    partial void OnDatumChanged(DateTime? value) => OnPropertyChanged(nameof(Fouttekst));

    partial void OnTermijnChanged(string value) => OnPropertyChanged(nameof(Fouttekst));

    private string Momentopname() => string.Join('|',
        [Relatie?.Id.ToString(), Datum?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Termijn?.Trim(),
         .. Regels.Select(r => string.Join('~', r.Omschrijving?.Trim(), r.Aantal?.Trim(), r.Prijs?.Trim(), r.Rekening?.Id))]);
}

internal static class Meldingen
{
    /// <summary>De melding van een <see cref="ArgumentException"/> zonder de toevoeging "(Parameter '…')".</summary>
    public static string Zonder(ArgumentException ex) =>
        ex.ParamName is null ? ex.Message : ex.Message.Replace($" (Parameter '{ex.ParamName}')", string.Empty, StringComparison.Ordinal);
}
