using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.ViewModels.Boekhouding;

public enum Rapportsoort
{
    Saldibalans,
    Balans,
    WinstEnVerlies,

    /// <summary>De balans volgens de eigen jaarrekeningindeling (besluit 27).</summary>
    JaarrekeningBalans,

    /// <summary>De staat van baten en lasten volgens de eigen jaarrekeningindeling.</summary>
    JaarrekeningBatenLasten,
}

public sealed record Rapportkeuze(Rapportsoort Soort, string Naam);

/// <summary>
/// Subtabbladen van het tabblad Boekhouding, in de volgorde van de tabbladen: de waarde is de index in de TabControl.
/// </summary>
public enum BoekhoudingSubtab
{
    Journaal,
    Bank,
    Postvak,
    Boekingsregels,
    Relaties,
    Rekeningschema,
    Grootboekkaart,
    Rapporten,
    Boekjaren,
}

/// <summary>Eén journaalpost in de lijst.</summary>
public sealed record PostRegel(Journaalpost Post, Woordenboek? Woorden = null, int AantalBijlagen = 0)
{
    /// <summary>Paperclip-kolom in het Journaal (besluit 140).</summary>
    public bool HeeftBijlage => AantalBijlagen > 0;

    public Guid Id => Post.Id;
    public string Nummer => Post.Nummer;
    public DateOnly DatumWaarde => Post.Datum;
    public string Datum => Weergave.Datum(Post.Datum);
    public string Dagboek => Post.Dagboek.Code;
    public string Omschrijving => Post.Omschrijving;
    public string Bedrag => Post.TotaalDebet.ToString();
    public string Status => Post.IsGeboekt ? Woorden?["Geboekt"] ?? "Geboekt" : Woorden?["Concept"] ?? "Concept";
    public bool IsGeboekt => Post.IsGeboekt;
    public bool IsConcept => !Post.IsGeboekt;

    /// <summary>Afsluiting en openingsbalans worden via het boekjaar beheerd, niet via tegenboeken.</summary>
    public bool KanTegenboeken => Post.IsGeboekt && Post.Soort is PostSoort.Gewoon or PostSoort.Tegenboeking;
}

/// <summary>Eén rekening in het rekeningschema; een nieuwe rekening heeft nog geen Id.</summary>
public sealed partial class RekeningRegel : ObservableObject
{
    private readonly string _origineel;

    public RekeningRegel(Grootboekrekening? rekening, Rubriek? rubriek, long saldo)
    {
        Id = rekening?.Id;
        Nummertekst = rekening?.Nummer.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        Naam = rekening?.Naam ?? string.Empty;
        Soort = rekening?.Soort ?? RekeningSoort.Balans;
        Rol = rekening?.Rol ?? RekeningRol.Geen;
        IsActief = rekening?.IsActief ?? true;
        Kant = rekening?.Kant ?? ResultaatKant.Geen;
        GekozenRubriek = rubriek;
        Saldotekst = rekening is null ? string.Empty : new Bedrag(saldo).ToString();
        _origineel = Momentopname();
    }

    public int? Id { get; }

    public bool IsNieuw => Id is null;

    public string Saldotekst { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout), nameof(Nummer))]
    public partial string Nummertekst { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string Naam { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(KantIsKiesbaar))]
    public partial RekeningSoort Soort { get; set; }

    /// <summary>Baten of lasten (besluit 152); alleen bij een resultaatrekening.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial ResultaatKant Kant { get; set; }

    public bool KantIsKiesbaar => Soort == RekeningSoort.Resultaat;

    public static IReadOnlyList<ResultaatKant> Kanten { get; } = [ResultaatKant.Baten, ResultaatKant.Lasten];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial RekeningRol Rol { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial bool IsActief { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial Rubriek? GekozenRubriek { get; set; }

    public int? Nummer => int.TryParse(Nummertekst, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 99999 ? n : null;

    public bool IsGewijzigd => !string.Equals(Momentopname(), _origineel, StringComparison.Ordinal);

    public string? Fout => Nummer is null ? "Gebruik een rekeningnummer van 1 tot en met 99999."
        : string.IsNullOrWhiteSpace(Naam) ? "Vul de naam in."
        : null;

    internal RekeningInvoer Invoer => new(Id, Nummer!.Value, Naam.Trim(), GekozenRubriek?.Id, Soort, Rol, IsActief,
        Soort == RekeningSoort.Resultaat && Kant != ResultaatKant.Geen ? Kant : null);

    partial void OnSoortChanged(RekeningSoort value) =>
        Kant = value == RekeningSoort.Balans ? ResultaatKant.Geen
            : Kant == ResultaatKant.Geen ? Grootboekrekening.StandaardKant(value, Nummer ?? 0) : Kant;

    private string Momentopname() => string.Join('|', Nummertekst?.Trim(), Naam?.Trim(), Soort, Rol, IsActief, GekozenRubriek?.Id, Kant);
}

public sealed record KaartRegel(KaartMutatie Mutatie)
{
    public Guid PostId => Mutatie.PostId;
    public string Datum => Weergave.Datum(Mutatie.Datum);
    public string Nummer => Mutatie.Nummer;
    public string Omschrijving => Mutatie.Omschrijving;
    public string Debet => Mutatie.Debet == Bedrag.Nul ? string.Empty : Mutatie.Debet.ToString();
    public string Credit => Mutatie.Credit == Bedrag.Nul ? string.Empty : Mutatie.Credit.ToString();
    public string Saldo => new Bedrag(Mutatie.LopendSaldo).ToString();
}

public sealed record JaarRegel(int Jaar, bool IsOpen, string Statustekst, int AantalConcepten, int AantalGeboekt)
{
    public string Jaartekst => Jaar.ToString(CultureInfo.InvariantCulture);

    public bool IsAfgesloten => !IsOpen;
}
