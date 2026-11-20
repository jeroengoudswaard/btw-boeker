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

/// <summary>Een voorwaarde van een boekingsregel; in het detail staan alleen de ingevulde en de toegevoegde (besluit 38).</summary>
public enum Voorwaarde
{
    TegenIban,
    NaamBevat,
    OmschrijvingBevat,
    Richting,
    Bedrag,
    Transactiecode,
}

/// <summary>Eén boekingsregel in het subtabblad Boekingsregels; een nieuwe regel heeft nog geen Id.</summary>
/// <summary>Besluit 50: het btw-tarief van een boekingsregel; het bankbedrag is altijd inclusief btw.</summary>
public sealed record Btwtarief(string Tekst, int? Percentage)
{
    public override string ToString() => Tekst;
}

public sealed partial class BoekingsregelRegel : ObservableObject
{
    /// <summary>De keuzes voor <see cref="GekozenBtwtarief"/>.</summary>
    public static IReadOnlyList<Btwtarief> Btwtarieven { get; } =
        [new("Geen btw", null), new("21 %", 21), new("9 %", 9), new("0 %", 0)];

    private static readonly string[] Afgeleid =
    [
        nameof(Wanneer), nameof(Waarheen), nameof(ToeTeVoegenVoorwaarden), nameof(IsTegenIbanZichtbaar), nameof(IsNaamBevatZichtbaar),
        nameof(IsOmschrijvingBevatZichtbaar), nameof(IsRichtingZichtbaar), nameof(IsBedragZichtbaar), nameof(IsTransactiecodeZichtbaar),
        nameof(HeeftToeTeVoegenVoorwaarden),
    ];

    private readonly string _origineel;
    private readonly HashSet<Voorwaarde> _toegevoegd = [];

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
        GekozenBtwtarief = Btwtarieven.FirstOrDefault(t => t.Percentage == regel?.BtwPercentage) ?? Btwtarieven[0];
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout), nameof(Waarheen))]
    public partial Btwtarief GekozenBtwtarief { get; set; } = new("Geen btw", null);

    public bool IsGewijzigd => IsNieuw || !string.Equals(Momentopname(), _origineel, StringComparison.Ordinal);

    public string? Fout =>
        GekozenRekening is null ? "Kies een rekening."
        : !Leesbaar(BedragVanTekst) || !Leesbaar(BedragTotTekst) ? "Gebruik een bedrag zoals 12,50."
        : Voorwaarden.IsLeeg ? "Geef minstens één voorwaarde."
        : Voorwaarden is { BedragVan: { } van, BedragTot: { } tot } && van > tot ? "Het bedrag 'van' is groter dan 'tot'."
        : !string.IsNullOrWhiteSpace(TegenIban) && !Iban.Maak(TegenIban).ControlegetalGeldig ? "Het tegen-IBAN klopt niet."
        : GekozenBtwtarief.Percentage is > 0 && OntbrekendeBtwrol is { } rol ? $"Wijs eerst een rekening {rol} aan in het rekeningschema."
        : null;

    /// <summary>Besluit 50: de rol van de btw-rekening die voor dit tarief nog ontbreekt, of <c>null</c>.</summary>
    private string? OntbrekendeBtwrol
    {
        get
        {
            if (GekozenRekening is null)
            {
                return null;
            }

            var soort = Domain.Financien.Btw.BtwRegels.SoortVoor(GekozenRekening);
            return Domain.Financien.Btw.BtwRegels.Btwrekening(Rekeningen, soort) is null
                ? RekeningRolTekst.Naam(Domain.Financien.Btw.BtwRegels.RolVoor(soort))
                : null;
        }
    }

    // ---- Lijst en detail (besluit 38) ------------------------------------------------------------

    /// <summary>In de lijst: wanneer de regel geldt, in gewone woorden.</summary>
    public string Wanneer
    {
        get
        {
            var delen = new List<string>();
            if (!string.IsNullOrWhiteSpace(TegenIban))
            {
                delen.Add($"IBAN {TegenIban.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(NaamBevat))
            {
                delen.Add($"naam bevat \"{NaamBevat.Trim()}\"");
            }

            if (!string.IsNullOrWhiteSpace(OmschrijvingBevat))
            {
                delen.Add($"omschrijving bevat \"{OmschrijvingBevat.Trim()}\"");
            }

            if (Richting != Richting.Beide)
            {
                delen.Add(Richting == Richting.Af ? "af" : "bij");
            }

            switch (Lees(BedragVanTekst), Lees(BedragTotTekst))
            {
                case ({ } van, { } tot):
                    delen.Add($"van {van} tot {tot}");
                    break;
                case ({ } van, null):
                    delen.Add($"vanaf {van}");
                    break;
                case (null, { } tot):
                    delen.Add($"tot {tot}");
                    break;
            }

            if (!string.IsNullOrWhiteSpace(Transactiecode))
            {
                delen.Add($"code {Transactiecode.Trim()}");
            }

            return delen.Count == 0 ? "nog geen voorwaarde" : string.Join(", ", delen);
        }
    }

    /// <summary>In de lijst: de rekening waar de transactie heen gaat.</summary>
    public string Waarheen => (GekozenRekening is { } rekening ? $"→ {rekening}" : "→ nog geen rekening")
        + (GekozenBtwtarief.Percentage is { } percentage ? $" · {percentage} % btw" : string.Empty);

    public bool IsTegenIbanZichtbaar => !string.IsNullOrWhiteSpace(TegenIban) || _toegevoegd.Contains(Voorwaarde.TegenIban);

    public bool IsNaamBevatZichtbaar => !string.IsNullOrWhiteSpace(NaamBevat) || _toegevoegd.Contains(Voorwaarde.NaamBevat);

    public bool IsOmschrijvingBevatZichtbaar => !string.IsNullOrWhiteSpace(OmschrijvingBevat) || _toegevoegd.Contains(Voorwaarde.OmschrijvingBevat);

    public bool IsRichtingZichtbaar => Richting != Richting.Beide || _toegevoegd.Contains(Voorwaarde.Richting);

    public bool IsBedragZichtbaar =>
        !string.IsNullOrWhiteSpace(BedragVanTekst) || !string.IsNullOrWhiteSpace(BedragTotTekst) || _toegevoegd.Contains(Voorwaarde.Bedrag);

    public bool IsTransactiecodeZichtbaar => !string.IsNullOrWhiteSpace(Transactiecode) || _toegevoegd.Contains(Voorwaarde.Transactiecode);

    /// <summary>De voorwaarden die nog niet in het detail staan, voor <i>Voorwaarde toevoegen ▾</i>.</summary>
    public IReadOnlyList<Voorwaarde> ToeTeVoegenVoorwaarden =>
    [
        .. Enum.GetValues<Voorwaarde>().Where(v => !(v switch
        {
            Voorwaarde.TegenIban => IsTegenIbanZichtbaar,
            Voorwaarde.NaamBevat => IsNaamBevatZichtbaar,
            Voorwaarde.OmschrijvingBevat => IsOmschrijvingBevatZichtbaar,
            Voorwaarde.Richting => IsRichtingZichtbaar,
            Voorwaarde.Bedrag => IsBedragZichtbaar,
            _ => IsTransactiecodeZichtbaar,
        })),
    ];

    public bool HeeftToeTeVoegenVoorwaarden => ToeTeVoegenVoorwaarden.Count > 0;

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void VoorwaardeToevoegen(Voorwaarde voorwaarde)
    {
        if (_toegevoegd.Add(voorwaarde))
        {
            MeldAfgeleid();
        }
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is { } naam && !Afgeleid.Contains(naam) && naam is not (nameof(IsGewijzigd) or nameof(Fout)))
        {
            MeldAfgeleid();
        }
    }

    private void MeldAfgeleid()
    {
        foreach (var naam in Afgeleid)
        {
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(naam));
        }
    }

    internal BoekingsregelVoorwaarden Voorwaarden => new(
        Leeg(TegenIban), Leeg(NaamBevat), Leeg(OmschrijvingBevat), Richting, Lees(BedragVanTekst), Lees(BedragTotTekst), Leeg(Transactiecode));

    internal BoekingsregelInvoer Invoer => new(Id, Naam, GekozenRekening!.Id, Voorwaarden, Leeg(Omschrijving), IsActief, GekozenBtwtarief.Percentage);

    private static string? Leeg(string? tekst) => string.IsNullOrWhiteSpace(tekst) ? null : tekst.Trim();

    private static bool Leesbaar(string tekst) => string.IsNullOrWhiteSpace(tekst) || Bedrag.TryParse(tekst, out _);

    private static Bedrag? Lees(string tekst) => !string.IsNullOrWhiteSpace(tekst) && Bedrag.TryParse(tekst, out var b) ? b : null;

    private string Momentopname() => string.Join('|', Naam, TegenIban, NaamBevat, OmschrijvingBevat, Richting, BedragVanTekst, BedragTotTekst,
        Transactiecode, GekozenRekening?.Id, Omschrijving, IsActief, GekozenBtwtarief.Percentage);
}
