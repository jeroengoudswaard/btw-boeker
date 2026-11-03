using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.App.ViewModels.Contributie;

/// <summary>Subtabbladen van het tabblad Contributie, in de volgorde van het scherm.</summary>
public enum ContributieSubtab
{
    Leden,
    Betalers,
    Tarieven,
    Runs,
    Openstaand,
}

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
    private readonly DateOnly? _huidigVanaf;
    private readonly bool _oudeIsGeincasseerd;

    public LidCategorieRegel(
        Persoon persoon, Contributiecategorie? huidig, DateOnly? huidigVanaf, Contributiecategorie? voorstel, string? betaler,
        bool heeftMachtiging, DateOnly vandaag, bool oudeIsGeincasseerd = false)
    {
        ArgumentNullException.ThrowIfNull(persoon);
        Lidnummer = persoon.Lidnummer;
        Naam = persoon.VolledigeNaam;
        Groepen = string.Join(", ", persoon.Functies
            .Where(f => f.IsActief(vandaag) && f.Groep.Soort != GroepSoort.Technisch)
            .Select(f => f.Groep.Naam)
            .Distinct());
        _origineel = huidig;
        _huidigVanaf = huidigVanaf;
        _oudeIsGeincasseerd = oudeIsGeincasseerd;
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
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Waarschuwing), nameof(Fout), nameof(KanCorrigerenOfWisselen), nameof(Vervangt))]
    public partial Contributiecategorie? GekozenCategorie { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial DateTime? Ingangsdatum { get; set; }

    public bool IsGewijzigd => GekozenCategorie is not null && !ReferenceEquals(GekozenCategorie, _origineel);

    /// <summary>Besluit 246: een lid met een categorie krijgt bij een wijziging de keuze tussen correctie en wissel.</summary>
    public bool KanCorrigerenOfWisselen => _origineel is not null && IsGewijzigd;

    /// <summary>Correctie: vervangt de oude categorie vanaf Lid sinds. Wissel: vanaf de 1e van de volgende maand (besluit 54).</summary>
    [ObservableProperty]
    public partial bool IsCorrectie { get; set; }

    /// <summary>Bij een correctie de ingangsdatum van de toewijzing die wordt vervangen.</summary>
    public DateOnly? Vervangt => KanCorrigerenOfWisselen && IsCorrectie ? _huidigVanaf : null;

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

        // Eerste categorie: vanaf "Lid sinds". Daarna (besluit 246) standaard een correctie zolang er met de oude categorie
        // niets is geïncasseerd, anders een wissel vanaf de 1e van de volgende maand (besluit 54).
        if (_origineel is null)
        {
            Ingangsdatum = Weergave.AlsDateTime(_lidSinds ?? new DateOnly(_vandaag.Year, _vandaag.Month, 1));
            return;
        }

        IsCorrectie = !_oudeIsGeincasseerd;
        ZetIngangsdatum();
    }

    partial void OnIsCorrectieChanged(bool value)
    {
        OnPropertyChanged(nameof(Vervangt));
        if (KanCorrigerenOfWisselen)
        {
            ZetIngangsdatum();
        }
    }

    private void ZetIngangsdatum() =>
        Ingangsdatum = IsCorrectie
            ? Weergave.AlsDateTime(_lidSinds ?? _huidigVanaf ?? new DateOnly(_vandaag.Year, _vandaag.Month, 1))
            : Weergave.AlsDateTime(Maand.Van(_vandaag).Volgende.EersteDag);
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

/// <summary>Eén incassorun; <paramref name="Postnummer"/> is de boeking (besluit 99), <paramref name="IsOntvangen"/> de uitkering door de bank.</summary>
public sealed record RunRegel(Incassorun Run, string? Postnummer = null, bool IsOntvangen = false)
{
    public string Geboekt => Postnummer ?? "—";
    public string Ontvangen => IsOntvangen ? "Ja" : "Nee";
    public bool KanBoeken => IsDefinitief && Postnummer is null;

    public string Maand => Run.Maand.ToString();
    public string Uitvoerdatum => Weergave.Datum(Run.Uitvoerdatum);
    public string Statustekst => Run.Status == IncassorunStatus.Definitief ? "Definitief" : "Geannuleerd";
    public int AantalPosten => Run.AantalPosten;
    public string Totaal => Run.Totaal.ToString();
    public bool IsDefinitief => Run.Status == IncassorunStatus.Definitief;
}

/// <summary>De opbrengstrekening van één contributiecategorie (besluit 63).</summary>
public sealed partial class OpbrengstRegel : ObservableObject
{
    private readonly int? _origineel;

    public OpbrengstRegel(Contributiecategorie categorie, Domain.Financien.Boekhouding.Grootboekrekening? rekening)
    {
        ArgumentNullException.ThrowIfNull(categorie);
        CategorieId = categorie.Id;
        Categorie = categorie.Naam;
        _origineel = categorie.OpbrengstrekeningId;
        GekozenRekening = rekening;
    }

    public int CategorieId { get; }
    public string Categorie { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial Domain.Financien.Boekhouding.Grootboekrekening? GekozenRekening { get; set; }

    public bool IsGewijzigd => GekozenRekening?.Id != _origineel;
}

/// <summary>Eén contributievordering op het subtabblad Openstaand.</summary>
public sealed record VorderingRegel(Application.Debiteuren.Vorderingstand Stand)
{
    public string Vordering => Stand.Vordering;
    public string Betaler => Stand.Post.Rekeninghouder;
    public string Iban => Stand.Post.Iban.ToString();
    public string Run => Stand.Run.Maand.ToString();
    public string Maanden => Application.Incasso.ContributieRekenaar.MaandenTekst(Stand.Post.Regels.Select(r => r.Maand));
    public string Bedrag => Stand.Bedrag.ToString();
    public string Saldo => new Bedrag(Stand.Saldo).ToString();
    public string? Reden => Stand.Redentekst;
    public string Datum => Weergave.Datum(Stand.LaatsteDatum);
    public bool HerincassoGepland => Stand.HerincassoGepland;
    public bool IsOpen => Stand.Saldo > 0;

    public string Status => Stand.Status switch
    {
        Application.Debiteuren.Vorderingstatus.Verwacht => "Verwacht",
        Application.Debiteuren.Vorderingstatus.Betaald => "Betaald",
        Application.Debiteuren.Vorderingstatus.Gestorneerd => HerincassoGepland ? "Gestorneerd · herincasso gepland" : "Gestorneerd",
        _ => "Afgeboekt",
    };
}
