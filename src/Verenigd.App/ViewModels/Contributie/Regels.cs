using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.App.ViewModels.Contributie;

internal static class Weergave
{
    public static readonly CultureInfo Nederlands = CultureInfo.GetCultureInfo("nl-NL");

    public static string Datum(DateOnly? datum) => datum?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>Bedrag zonder eurosteken, zoals in een invoerveld: <c>12,50</c>.</summary>
    public static string Invoer(Bedrag bedrag) => (bedrag.Centen / 100m).ToString("0.00", Nederlands);

    public static DateOnly? AlsDatum(DateTime? datum) => datum is { } d ? DateOnly.FromDateTime(d) : null;

    public static DateTime? AlsDateTime(DateOnly? datum) => datum?.ToDateTime(TimeOnly.MinValue);
}

/// <summary>Eén lid in het subtabblad "Leden &amp; categorie".</summary>
public sealed partial class LidCategorieRegel : ObservableObject
{
    private readonly Contributiecategorie? _origineel;
    private readonly DateOnly _vandaag;
    private readonly DateOnly? _lidSinds;
    private readonly bool _heeftBetaler;
    private readonly bool _heeftMachtiging;

    public LidCategorieRegel(
        Persoon persoon, Contributiecategorie? huidig, DateOnly? huidigVanaf, Contributiecategorie? voorstel, string? betaler,
        bool heeftMachtiging, DateOnly vandaag)
    {
        ArgumentNullException.ThrowIfNull(persoon);
        Lidnummer = persoon.Lidnummer;
        Naam = persoon.VolledigeNaam;
        Groepen = string.Join(", ", persoon.Functies
            .Where(f => f.IsActief(vandaag) && f.Groep.Soort != GroepSoort.Technisch)
            .Select(f => f.Groep.Naam)
            .Distinct());
        _origineel = huidig;
        _vandaag = vandaag;
        _lidSinds = persoon.LidSinds;
        _heeftBetaler = betaler is not null;
        _heeftMachtiging = heeftMachtiging;
        Voorstel = voorstel;
        Betalertekst = betaler ?? "—";
        GekozenCategorie = huidig;
        Ingangsdatum = Weergave.AlsDateTime(huidigVanaf);
    }

    public Lidnummer Lidnummer { get; }
    public string Naam { get; }
    public string Groepen { get; }
    public Contributiecategorie? Voorstel { get; }
    public string Betalertekst { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Waarschuwing), nameof(Fout))]
    public partial Contributiecategorie? GekozenCategorie { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial DateTime? Ingangsdatum { get; set; }

    public bool IsGewijzigd => GekozenCategorie is not null && !ReferenceEquals(GekozenCategorie, _origineel);

    public string? Fout => IsGewijzigd && Ingangsdatum is null ? "Vul de ingangsdatum in." : null;

    /// <summary>Waarom dit lid (nog) niet kan worden geïncasseerd.</summary>
    public string? Waarschuwing => GekozenCategorie switch
    {
        null => "Geen categorie",
        { HeeftTarief: false } => null,
        _ when !_heeftBetaler => "Geen betaler",
        _ when !_heeftMachtiging => "Geen geldige machtiging",
        _ => null,
    };

    /// <summary>Besluit 52: het voorstel overnemen, met "Lid sinds" als ingangsdatum.</summary>
    internal void NeemVoorstelOver()
    {
        if (GekozenCategorie is null && Voorstel is not null)
        {
            GekozenCategorie = Voorstel;
            Ingangsdatum = Weergave.AlsDateTime(_lidSinds ?? new DateOnly(_vandaag.Year, _vandaag.Month, 1));
        }
    }

    partial void OnGekozenCategorieChanged(Contributiecategorie? value)
    {
        if (value is null || ReferenceEquals(value, _origineel))
        {
            return;
        }

        // Eerste categorie: vanaf "Lid sinds". Wissel: vanaf de 1e van de volgende maand (besluit 54).
        Ingangsdatum = _origineel is null
            ? Weergave.AlsDateTime(_lidSinds ?? new DateOnly(_vandaag.Year, _vandaag.Month, 1))
            : Weergave.AlsDateTime(Maand.Van(_vandaag).Volgende.EersteDag);
    }
}

/// <summary>Eén categorie in het subtabblad "Tarieven" voor het gekozen boekjaar.</summary>
public sealed partial class TariefRegel : ObservableObject
{
    private readonly string _origineel;

    public TariefRegel(Contributiecategorie categorie, Tarief? tarief)
    {
        ArgumentNullException.ThrowIfNull(categorie);
        CategorieId = categorie.Id;
        Categorie = categorie.Naam;
        Soort = categorie.Soort;
        IsBewerkbaar = categorie.HeeftTarief;
        _origineel = IsBewerkbaar ? (tarief is null ? string.Empty : Weergave.Invoer(tarief.Maandbedrag)) : Weergave.Invoer(Domain.Financien.Bedrag.Nul);
        Bedragtekst = _origineel;
    }

    public int CategorieId { get; }
    public string Categorie { get; }
    public CategorieSoort Soort { get; }

    /// <summary>Leiding en Vrijgesteld staan vast op € 0,00 (besluit 44).</summary>
    public bool IsBewerkbaar { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fout), nameof(IsGewijzigd))]
    public partial string Bedragtekst { get; set; }

    public Bedrag? Bedrag => Domain.Financien.Bedrag.TryParse(Bedragtekst, out var bedrag) ? bedrag : null;

    public string? Fout => IsBewerkbaar && !string.IsNullOrWhiteSpace(Bedragtekst) && Bedrag is null
        ? "Gebruik een bedrag zoals 12,50."
        : null;

    public bool IsGewijzigd => IsBewerkbaar && !string.Equals(Bedragtekst?.Trim(), _origineel, StringComparison.Ordinal);
}

public sealed record BetalerRegel(Guid Id, string Rekeninghouder, string Iban, int AantalLeden, string Machtigingtekst, string? Waarschuwing);

public sealed record LidKeuze(Lidnummer Lidnummer, string Naam)
{
    public override string ToString() => $"{Naam} ({Lidnummer.Waarde})";
}

public sealed record RunRegel(Incassorun Run)
{
    public string Maand => Run.Maand.ToString();
    public string Uitvoerdatum => Weergave.Datum(Run.Uitvoerdatum);
    public string Statustekst => Run.Status == IncassorunStatus.Definitief ? "Definitief" : "Geannuleerd";
    public int AantalPosten => Run.AantalPosten;
    public string Totaal => Run.Totaal.ToString();
    public bool IsDefinitief => Run.Status == IncassorunStatus.Definitief;
}
