using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;
using BtwBoeker.Domain.Financien.Btw;

namespace BtwBoeker.App.ViewModels.Boekhouding;

/// <summary>Besluit 50: een keuze in de kolom <i>Btw</i>: de code en of het ingevoerde bedrag inclusief btw is.</summary>
public sealed record BtwKeuze(string Tekst, BtwCode Code, bool Inclusief)
{
    public static BtwKeuze Geen { get; } = new("Geen btw", BtwCode.Geen, Inclusief: true);

    public BtwSoort Soort => Code.Soort;

    public override string ToString() => Tekst;
}

/// <summary>
/// Eén regel in de journaalpost-editor. Met een btw-tarief (besluit 50) is het ingevoerde bedrag inclusief of exclusief
/// btw; de regel rekent grondslag en btw uit en toont ze als onderregel. Bij opslaan maakt de editor er een grondslagregel
/// en een btw-regel op de btw-rekening van.
/// </summary>
public sealed partial class PostRegelEditor : ObservableObject
{
    private static readonly string[] Afgeleid =
    [
        nameof(Debet), nameof(Credit), nameof(Grondslag), nameof(BtwBedrag), nameof(Onderregel), nameof(IsBtwRegel),
        nameof(Btwwaarschuwing), nameof(Btwrekening), nameof(Fout), nameof(IsLeeg),
    ];

    /// <summary>
    /// De keuzelijst van de eigen editor. De weergave bindt hieraan in plaats van aan de editor erboven, zodat lijst en
    /// keuze tegelijk wisselen: een hergebruikte rij met de lijst van de vorige post maakte de rekening anders leeg.
    /// </summary>
    public IReadOnlyList<Grootboekrekening> Rekeningen { get; init; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Btwkeuzes))]
    public partial Grootboekrekening? GekozenRekening { get; set; }

    [ObservableProperty]
    public partial string? Omschrijving { get; set; }

    [ObservableProperty]
    public partial string Debettekst { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Credittekst { get; set; } = string.Empty;

    [ObservableProperty]
    public partial BtwKeuze GekozenBtw { get; set; } = BtwKeuze.Geen;

    /// <summary>Een met de hand aangepast btw-bedrag (van de factuur); leeg is berekend.</summary>
    [ObservableProperty]
    public partial string? Btwtekst { get; set; }

    /// <summary>Of het veld voor het btw-bedrag open staat (<i>btw aanpassen…</i>).</summary>
    [ObservableProperty]
    public partial bool IsBtwAanpassen { get; set; }

    /// <summary><i>btw aanpassen…</i>: het veld opent met het berekende bedrag.</summary>
    [RelayCommand]
    private void BtwAanpassen()
    {
        Btwtekst ??= Weergave.Invoer(BtwBedrag);
        IsBtwAanpassen = true;
    }

    /// <summary>De keuzes bij deze rekening: omzet of voorbelasting volgt uit de kant (besluit 50, keuze 1A).</summary>
    public IReadOnlyList<BtwKeuze> Btwkeuzes => Keuzes(GekozenRekening);

    /// <summary>Het ingevoerde bedrag, aan de kant waar het staat.</summary>
    private Bedrag Ingevoerd => Lees(Debettekst) ?? Lees(Credittekst) ?? Bedrag.Nul;

    private bool IsDebet => !string.IsNullOrWhiteSpace(Debettekst);

    /// <summary>Een regel waarbij de app een btw-regel maakt (niet een regel op de btw-rekening zelf, niet 0 %).</summary>
    public bool IsBtwRegel => GekozenBtw.Code.HeeftBtw && GekozenBtw.Code.Percentage > 0 && !IsOpBtwrekening;

    private bool IsOpBtwrekening => GekozenRekening?.Rol is RekeningRol.TeBetalenBtw or RekeningRol.Voorbelasting;

    private BtwBedragen Berekend => BtwBerekening.Bereken(Ingevoerd, GekozenBtw.Code.Percentage, GekozenBtw.Inclusief);

    private Bedrag? Aangepast => string.IsNullOrWhiteSpace(Btwtekst) ? null : Lees(Btwtekst);

    public Bedrag BtwBedrag => IsBtwRegel ? Aangepast ?? Berekend.Btw : Bedrag.Nul;

    public Bedrag Grondslag => IsBtwRegel && GekozenBtw.Inclusief ? new Bedrag(Ingevoerd.Centen - BtwBedrag.Centen) : Ingevoerd;

    private Bedrag Totaal => IsBtwRegel && !GekozenBtw.Inclusief ? Grondslag + BtwBedrag : Ingevoerd;

    public Bedrag Debet => IsDebet ? Totaal : Bedrag.Nul;

    public Bedrag Credit => IsDebet ? Bedrag.Nul : string.IsNullOrWhiteSpace(Credittekst) ? Bedrag.Nul : Totaal;

    /// <summary>De rekening voor het btw-bedrag, of <c>null</c> als die nog niet is aangewezen.</summary>
    public Grootboekrekening? Btwrekening => IsBtwRegel ? BtwRegels.Btwrekening(Rekeningen, GekozenBtw.Soort) : null;

    /// <summary>Bijvoorbeeld "grondslag € 8,65 · btw € 1,82 → 1520 Voorbelasting".</summary>
    public string? Onderregel => IsBtwRegel
        ? $"grondslag {Grondslag} · btw {BtwBedrag} → {Btwrekening?.ToString() ?? "(nog geen btw-rekening)"}"
        : null;

    /// <summary>Besluit 49: een aangepast btw-bedrag mag, maar de app zegt het als het afwijkt van het tarief.</summary>
    public string? Btwwaarschuwing => IsBtwRegel && Aangepast is { } aangepast && aangepast != Berekend.Btw
        ? $"⚠ De btw wijkt {new Bedrag(Math.Abs(aangepast.Centen - Berekend.Btw.Centen))} af van {GekozenBtw.Code.Percentage} %."
        : null;

    /// <summary>De naam van de rol die nog moet worden aangewezen, zoals "Voorbelasting".</summary>
    public string? OntbrekendeRol => IsBtwRegel && Btwrekening is null ? RekeningRolTekst.Naam(BtwRegels.RolVoor(GekozenBtw.Soort)) : null;

    public bool IsLeeg => GekozenRekening is null && string.IsNullOrWhiteSpace(Debettekst) && string.IsNullOrWhiteSpace(Credittekst);

    public string? Fout => IsLeeg ? null
        : (!string.IsNullOrWhiteSpace(Debettekst) && Lees(Debettekst) is null) || (!string.IsNullOrWhiteSpace(Credittekst) && Lees(Credittekst) is null)
            ? "Gebruik een bedrag zoals 12,50."
        : !string.IsNullOrWhiteSpace(Debettekst) && !string.IsNullOrWhiteSpace(Credittekst) && Lees(Debettekst) != Bedrag.Nul && Lees(Credittekst) != Bedrag.Nul ? "Vul debet óf credit in."
        : GekozenRekening is null ? "Kies een rekening."
        : IsBtwRegel && !string.IsNullOrWhiteSpace(Btwtekst) && Aangepast is null ? "Gebruik voor de btw een bedrag zoals 1,82."
        : IsBtwRegel && GekozenBtw.Inclusief && BtwBedrag.Centen > Ingevoerd.Centen ? "De btw is groter dan het bedrag."
        : OntbrekendeRol is { } rol ? $"Wijs eerst een rekening {rol} aan in het rekeningschema."
        : null;

    /// <summary>De regels voor de journaalpost: de grondslag en, bij btw, direct erna het btw-bedrag (besluit 50).</summary>
    internal IEnumerable<RegelInvoerId> NaarInvoer()
    {
        var omschrijving = string.IsNullOrWhiteSpace(Omschrijving) ? null : Omschrijving.Trim();
        var code = GekozenBtw.Code.HeeftBtw ? GekozenBtw.Code : (BtwCode?)null;
        yield return Zijde(GekozenRekening!.Id, omschrijving, IsBtwRegel ? Grondslag : Ingevoerd, code);
        if (IsBtwRegel && Btwrekening is { } btwrekening && BtwBedrag != Bedrag.Nul)
        {
            yield return Zijde(btwrekening.Id, omschrijving, BtwBedrag, code);
        }
    }

    private RegelInvoerId Zijde(int rekening, string? omschrijving, Bedrag bedrag, BtwCode? code) =>
        IsDebet ? new(rekening, omschrijving, bedrag, Bedrag.Nul, code) : new(rekening, omschrijving, Bedrag.Nul, bedrag, code);

    /// <summary>De keuzes voor een rekening; op een btw-rekening zelf alleen het tarief (geen incl. of excl.).</summary>
    internal static IReadOnlyList<BtwKeuze> Keuzes(Grootboekrekening? rekening)
    {
        if (rekening?.Rol is RekeningRol.TeBetalenBtw or RekeningRol.Voorbelasting)
        {
            var soort = rekening.Rol == RekeningRol.Voorbelasting ? BtwSoort.Voorbelasting : BtwSoort.Omzet;
            return [BtwKeuze.Geen, new("21 %", new(soort, 21), true), new("9 %", new(soort, 9), true), new("0 %", new(soort, 0), true)];
        }

        var s = rekening is null ? BtwSoort.Voorbelasting : BtwRegels.SoortVoor(rekening);
        List<BtwKeuze> keuzes =
        [
            BtwKeuze.Geen,
            new("21 % incl.", new(s, 21), true),
            new("21 % excl.", new(s, 21), false),
            new("9 % incl.", new(s, 9), true),
            new("9 % excl.", new(s, 9), false),
            new("0 %", new(s, 0), true),
        ];
        if (s == BtwSoort.Omzet)
        {
            keuzes.Add(new("Privégebruik 21 %", new(BtwSoort.Privegebruik, 21), false));
        }

        return keuzes;
    }

    partial void OnGekozenRekeningChanged(Grootboekrekening? value)
    {
        // Een andere rekening kan een andere soort hebben: zelfde tekst houden als die er is, anders geen btw.
        GekozenBtw = Btwkeuzes.FirstOrDefault(k => k.Tekst == GekozenBtw.Tekst) ?? BtwKeuze.Geen;
        MeldAfgeleid();
    }

    partial void OnDebettekstChanged(string value) => MeldAfgeleid();

    partial void OnCredittekstChanged(string value) => MeldAfgeleid();

    partial void OnGekozenBtwChanged(BtwKeuze value) => MeldAfgeleid();

    partial void OnBtwtekstChanged(string? value) => MeldAfgeleid();

    private void MeldAfgeleid()
    {
        foreach (var naam in Afgeleid)
        {
            OnPropertyChanged(naam);
        }

        OnPropertyChanged(nameof(OntbrekendeRol));
    }

    private static Bedrag? Lees(string? tekst) => string.IsNullOrWhiteSpace(tekst) ? null : Bedrag.TryParse(tekst, out var b) ? b : null;
}

/// <summary>
/// Een journaalpost bewerken (concept) of bekijken (geboekt). Wijzigingen worden pas opgeslagen via het
/// tabblad (Ctrl+S voor concept, Ctrl+Enter voor boeken).
/// </summary>
public sealed partial class PostEditor : ObservableObject
{
    private readonly string _origineel;

    public PostEditor(Journaalpost? post, IReadOnlyList<Dagboek> dagboeken, IReadOnlyList<Grootboekrekening> rekeningen, DateOnly vandaag)
    {
        ArgumentNullException.ThrowIfNull(dagboeken);
        ArgumentNullException.ThrowIfNull(rekeningen);
        Id = post?.Id;
        Nummer = post?.Nummer ?? "(nieuw)";
        IsAlleenLezen = post?.IsGeboekt == true;
        Dagboeken = dagboeken;
        Rekeningen = IsAlleenLezen ? rekeningen : [.. rekeningen.Where(r => r.IsActief || post?.Regels.Any(g => g.Rekening.Id == r.Id) == true)];
        GekozenDagboek = post is null
            ? dagboeken.FirstOrDefault(d => d.Soort == DagboekSoort.Memoriaal) ?? (dagboeken.Count > 0 ? dagboeken[0] : null)
            : dagboeken.FirstOrDefault(d => d.Id == post.Dagboek.Id) ?? post.Dagboek;
        Datum = (post?.Datum ?? vandaag).ToDateTime(TimeOnly.MinValue);
        Omschrijving = post?.Omschrijving ?? string.Empty;
        Statustekst = post is null ? "Nieuw" : post.IsGeboekt ? $"Geboekt op {Weergave.Datum(DateOnly.FromDateTime(post.GeboektUtc!.Value.ToLocalTime()))}" : "Concept";
        Regels.CollectionChanged += RegelsGewijzigd;
        if (post is null)
        {
            Regels.Add(new PostRegelEditor { Rekeningen = Rekeningen });
            Regels.Add(new PostRegelEditor { Rekeningen = Rekeningen });
        }
        else
        {
            LaadRegels(post);
        }

        _origineel = Momentopname();
        NieuweRegelCommand = new RelayCommand(() => Regels.Add(new PostRegelEditor { Rekeningen = Rekeningen }), () => !IsAlleenLezen);
        VerwijderRegelCommand = new RelayCommand<PostRegelEditor>(r => Regels.Remove(r!), r => !IsAlleenLezen && r is not null);
    }

    public Guid? Id { get; }

    public string Nummer { get; }

    public string Statustekst { get; }

    public bool IsAlleenLezen { get; }

    public bool IsBewerkbaar => !IsAlleenLezen;

    public IReadOnlyList<Dagboek> Dagboeken { get; }

    public IReadOnlyList<Grootboekrekening> Rekeningen { get; }

    public ObservableCollection<PostRegelEditor> Regels { get; } = [];

    public IRelayCommand NieuweRegelCommand { get; }

    public IRelayCommand<PostRegelEditor> VerwijderRegelCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fouten))]
    public partial Dagboek? GekozenDagboek { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fouten))]
    public partial DateTime? Datum { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fouten))]
    public partial string Omschrijving { get; set; }

    public Bedrag TotaalDebet => Regels.Select(r => r.Debet).Som();

    public Bedrag TotaalCredit => Regels.Select(r => r.Credit).Som();

    public string TotaalDebettekst => TotaalDebet.ToString();

    public string TotaalCredittekst => TotaalCredit.ToString();

    /// <summary>Het verschil tussen debet en credit, altijd positief weergegeven.</summary>
    public string Verschiltekst => new Bedrag(Math.Abs(TotaalDebet.Centen - TotaalCredit.Centen)).ToString();

    public bool IsInEvenwicht => TotaalDebet == TotaalCredit && TotaalDebet != Bedrag.Nul;

    public bool IsGewijzigd => !IsAlleenLezen && !string.Equals(Momentopname(), _origineel, StringComparison.Ordinal);

    /// <summary>Wat het opslaan als concept in de weg staat.</summary>
    public IReadOnlyList<string> Fouten
    {
        get
        {
            var fouten = new List<string>();
            if (GekozenDagboek is null)
            {
                fouten.Add("Kies een dagboek.");
            }

            if (Datum is null)
            {
                fouten.Add("Vul de datum in.");
            }

            if (string.IsNullOrWhiteSpace(Omschrijving))
            {
                fouten.Add("Vul een omschrijving in.");
            }

            fouten.AddRange(Regels.Select(r => r.Fout).OfType<string>().Distinct());
            return fouten;
        }
    }

    /// <summary>Wat er nog ontbreekt om te boeken.</summary>
    public string? Boekmelding => Fouten.Count > 0 ? Fouten[0]
        : Regels.Count(r => !r.IsLeeg) < 2 ? "Een post heeft minstens twee regels."
        : !IsInEvenwicht ? $"Debet en credit verschillen {Verschiltekst}."
        : null;

    public bool KanBoeken => !IsAlleenLezen && Boekmelding is null;

    internal PostInvoer Invoer => new(
        Id, GekozenDagboek!.Id, DateOnly.FromDateTime(Datum!.Value), Omschrijving.Trim(),
        [.. Regels.Where(r => !r.IsLeeg).SelectMany(r => r.NaarInvoer())]);

    /// <summary>De naam van een btw-rekening die nog moet worden aangewezen, zoals "Voorbelasting" (besluit 50).</summary>
    public string? OntbrekendeBtwrekening => Regels.Select(r => r.OntbrekendeRol).FirstOrDefault(r => r is not null);

    /// <summary>
    /// Besluit 50: een grondslagregel met direct erna zijn btw-regel (zelfde code, op de btw-rekening) wordt één regel met
    /// onderregel. Incl. of excl. volgt uit wat precies uitkomt; anders incl. met het bedrag als aangepaste btw.
    /// </summary>
    private void LaadRegels(Journaalpost post)
    {
        var regels = post.Regels.ToList();
        for (var i = 0; i < regels.Count; i++)
        {
            var regel = regels[i];
            var editor = new PostRegelEditor
            {
                Rekeningen = Rekeningen,
                GekozenRekening = Rekeningen.FirstOrDefault(r => r.Id == regel.Rekening.Id) ?? regel.Rekening,
                Omschrijving = regel.Omschrijving,
            };
            var debet = regel.Debet != Bedrag.Nul;
            var grondslag = debet ? regel.Debet : regel.Credit;
            var btw = Bedrag.Nul;
            if (regel.Btw.HeeftBtw && !regel.IsBtwBedrag && i + 1 < regels.Count && regels[i + 1] is { IsBtwBedrag: true } volgende && volgende.Btw == regel.Btw
                && (volgende.Debet != Bedrag.Nul) == debet)
            {
                btw = debet ? volgende.Debet : volgende.Credit;
                i++;
            }

            var keuzes = editor.Btwkeuzes;
            var inclusief = keuzes.FirstOrDefault(k => k.Code == regel.Btw && k.Inclusief);
            var exclusief = keuzes.FirstOrDefault(k => k.Code == regel.Btw && !k.Inclusief);
            var totaal = grondslag + btw;
            string? aangepast = null;
            BtwKeuze keuze;
            Bedrag ingevoerd;
            if (!regel.Btw.HeeftBtw)
            {
                (keuze, ingevoerd) = (BtwKeuze.Geen, grondslag);
            }
            else if (inclusief is not null && BtwBerekening.Bereken(totaal, regel.Btw.Percentage, true).Btw == btw)
            {
                (keuze, ingevoerd) = (inclusief, totaal);
            }
            else if (exclusief is not null && BtwBerekening.Bereken(grondslag, regel.Btw.Percentage, false).Btw == btw)
            {
                (keuze, ingevoerd) = (exclusief, grondslag);
            }
            else
            {
                keuze = inclusief ?? exclusief ?? keuzes.FirstOrDefault(k => k.Code == regel.Btw) ?? BtwKeuze.Geen;
                ingevoerd = keuze.Inclusief ? totaal : grondslag;
                aangepast = Weergave.Invoer(btw);
            }

            editor.GekozenBtw = keuze;
            editor.Btwtekst = aangepast;
            editor.IsBtwAanpassen = aangepast is not null;
            editor.Debettekst = debet ? Weergave.Invoer(ingevoerd) : string.Empty;
            editor.Credittekst = debet ? string.Empty : Weergave.Invoer(ingevoerd);
            Regels.Add(editor);
        }
    }

    private string Momentopname() => string.Join('|',
        GekozenDagboek?.Id, Datum?.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture), Omschrijving,
        string.Join(';', Regels.Where(r => !r.IsLeeg).Select(r => $"{r.GekozenRekening?.Id}:{r.Omschrijving}:{r.Debettekst?.Trim()}:{r.Credittekst?.Trim()}:{r.GekozenBtw.Tekst}:{r.Btwtekst?.Trim()}")));

    private void RegelsGewijzigd(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (PostRegelEditor regel in e.OldItems ?? Array.Empty<PostRegelEditor>())
        {
            regel.PropertyChanged -= RegelGewijzigd;
        }

        foreach (PostRegelEditor regel in e.NewItems ?? Array.Empty<PostRegelEditor>())
        {
            regel.PropertyChanged += RegelGewijzigd;
        }

        Totalen();
    }

    private void RegelGewijzigd(object? sender, PropertyChangedEventArgs e) => Totalen();

    private void Totalen()
    {
        OnPropertyChanged(nameof(TotaalDebettekst));
        OnPropertyChanged(nameof(TotaalCredittekst));
        OnPropertyChanged(nameof(Verschiltekst));
        OnPropertyChanged(nameof(IsInEvenwicht));
        OnPropertyChanged(nameof(IsGewijzigd));
        OnPropertyChanged(nameof(Fouten));
        OnPropertyChanged(nameof(Boekmelding));
        OnPropertyChanged(nameof(KanBoeken));
        OnPropertyChanged(nameof(OntbrekendeBtwrekening));
    }

    partial void OnGekozenDagboekChanged(Dagboek? value) => Totalen();

    partial void OnDatumChanged(DateTime? value) => Totalen();

    partial void OnOmschrijvingChanged(string value) => Totalen();
}
