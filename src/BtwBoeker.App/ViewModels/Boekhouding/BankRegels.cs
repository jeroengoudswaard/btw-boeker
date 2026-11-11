using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using BtwBoeker.Application.Bank;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Bank;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.ViewModels.Boekhouding;

public enum MutatieFilter
{
    TeVerwerken,
    Concept,
    Geboekt,
    Alle,
}

public sealed record MutatieFilterKeuze(MutatieFilter Filter, string Naam);

/// <summary>Eén banktransactie in de lijst van het subtabblad Bank.</summary>
public sealed record MutatieRegel(Bankmutatie Mutatie, MutatieStatus Statuswaarde, string? Postnummer)
{
    public Guid Id => Mutatie.Id;
    public DateOnly DatumWaarde => Mutatie.Boekdatum;
    public string Datum => Weergave.Datum(Mutatie.Boekdatum);
    public string Tegenpartij => Mutatie.Tegennaam ?? (Mutatie.TegenIban is { } iban ? Iban.Maak(iban).ToString() : Mutatie.Standaardomschrijving);
    public string Tegenrekening => Mutatie.TegenIban is { } iban ? Iban.Maak(iban).ToString() : string.Empty;
    public string Omschrijving => Mutatie.Omschrijving ?? string.Empty;
    public string Bedragtekst => new Bedrag(Mutatie.Saldo).ToString();
    public string Soort => Mutatie.Code.Omschrijving + (Mutatie.BatchAantal is { } n ? string.Create(CultureInfo.CurrentCulture, $" ({n} posten)") : string.Empty);
    public string Code => Mutatie.Code.ToString();
    public bool IsTeVerwerken => Statuswaarde == MutatieStatus.TeVerwerken;

    public string Status => Statuswaarde switch
    {
        MutatieStatus.TeVerwerken => "Te verwerken",
        MutatieStatus.Concept => "Concept",
        _ => "Geboekt",
    };
}

public sealed record TransactiecodeKeuze(string Code, string Naam)
{
    public override string ToString() => $"{Code} · {Naam}";
}

/// <summary>Eén boekingsregel in het subtabblad Boekingsregels; een nieuwe regel heeft nog geen Id.</summary>
public sealed partial class BoekingsregelRegel : ObservableObject
{
    private readonly string _origineel;

    public BoekingsregelRegel(Boekingsregel? regel, Grootboekrekening? rekening, BoekingsregelVoorwaarden? voorwaarden = null)
    {
        Id = regel?.Id;
        Volgorde = regel?.Volgorde ?? 0;
        var v = regel?.Voorwaarden ?? voorwaarden ?? new BoekingsregelVoorwaarden();
        Naam = regel?.Naam ?? string.Empty;
        TegenIban = v.TegenIban;
        NaamBevat = v.NaamBevat;
        OmschrijvingBevat = v.OmschrijvingBevat;
        Richting = v.Richting;
        BedragVanTekst = v.BedragVan is { } van ? Weergave.Invoer(van) : string.Empty;
        BedragTotTekst = v.BedragTot is { } tot ? Weergave.Invoer(tot) : string.Empty;
        Transactiecode = v.Transactiecode;
        GekozenRekening = rekening;
        Omschrijving = regel?.Omschrijving;
        IsActief = regel?.IsActief ?? true;
        _origineel = regel is null ? string.Empty : Momentopname();
    }

    public int? Id { get; }

    public int Volgorde { get; }

    public bool IsNieuw => Id is null;

    /// <summary>
    /// De keuzelijst voor <see cref="GekozenRekening"/>. De weergave bindt hieraan en niet via de pagina: die wordt pas
    /// gevonden als de cel in beeld komt, en een keuzelijst zonder items maakte de rekening leeg.
    /// </summary>
    public IReadOnlyList<Grootboekrekening> Rekeningen { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Naam { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string? TegenIban { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string? NaamBevat { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string? OmschrijvingBevat { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial Richting Richting { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string BedragVanTekst { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string BedragTotTekst { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string? Transactiecode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial Grootboekrekening? GekozenRekening { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string? Omschrijving { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial bool IsActief { get; set; }

    public bool IsGewijzigd => IsNieuw || !string.Equals(Momentopname(), _origineel, StringComparison.Ordinal);

    public string? Fout =>
        GekozenRekening is null ? "Kies een rekening."
        : !Leesbaar(BedragVanTekst) || !Leesbaar(BedragTotTekst) ? "Gebruik een bedrag zoals 12,50."
        : Voorwaarden.IsLeeg ? "Geef minstens één voorwaarde."
        : Voorwaarden is { BedragVan: { } van, BedragTot: { } tot } && van > tot ? "Het bedrag 'van' is groter dan 'tot'."
        : !string.IsNullOrWhiteSpace(TegenIban) && !Iban.Maak(TegenIban).ControlegetalGeldig ? "Het tegen-IBAN klopt niet."
        : null;

    internal BoekingsregelVoorwaarden Voorwaarden => new(
        Leeg(TegenIban), Leeg(NaamBevat), Leeg(OmschrijvingBevat), Richting, Lees(BedragVanTekst), Lees(BedragTotTekst), Leeg(Transactiecode));

    internal BoekingsregelInvoer Invoer => new(Id, Naam, GekozenRekening!.Id, Voorwaarden, Leeg(Omschrijving), IsActief);

    private static string? Leeg(string? tekst) => string.IsNullOrWhiteSpace(tekst) ? null : tekst.Trim();

    private static bool Leesbaar(string tekst) => string.IsNullOrWhiteSpace(tekst) || Bedrag.TryParse(tekst, out _);

    private static Bedrag? Lees(string tekst) => !string.IsNullOrWhiteSpace(tekst) && Bedrag.TryParse(tekst, out var b) ? b : null;

    private string Momentopname() => string.Join('|', Naam, TegenIban, NaamBevat, OmschrijvingBevat, Richting, BedragVanTekst, BedragTotTekst,
        Transactiecode, GekozenRekening?.Id, Omschrijving, IsActief);
}
