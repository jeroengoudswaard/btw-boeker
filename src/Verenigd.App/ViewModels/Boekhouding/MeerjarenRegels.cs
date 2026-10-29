using CommunityToolkit.Mvvm.ComponentModel;
using Verenigd.App.ViewModels.Contributie;
using Verenigd.Application.Begrotingen;
using Verenigd.Domain.Financien.Begrotingen;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.App.ViewModels.Boekhouding;

/// <summary>Eén versie in de keuzelijst, bijvoorbeeld "Versie 1 · Oorspronkelijk · Vastgesteld 20-11-2026".</summary>
public sealed record MeerjarenVersie(Meerjarenplan Plan)
{
    public int Id => Plan.Id;

    public string Tekst => $"Versie {Plan.Versie} · {Plan.Omschrijving} · "
        + (Plan.IsVastgesteld ? $"Vastgesteld {Weergave.Datum(Plan.VastgesteldOp)}" : "Concept");

    public override string ToString() => Tekst;
}

/// <summary>Eén jaarvak in het raster; leeg is nul.</summary>
public sealed partial class JaarCel(int jaar, string tekst) : ObservableObject
{
    public int Jaar { get; } = jaar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bedrag), nameof(IsGeldig))]
    public partial string Tekst { get; set; } = tekst;

    public Domain.Financien.Bedrag? Bedrag => string.IsNullOrWhiteSpace(Tekst) ? Domain.Financien.Bedrag.Nul
        : Domain.Financien.Bedrag.TryParse(Tekst, out var bedrag) ? bedrag : null;

    public bool IsGeldig => Bedrag is not null;
}

/// <summary>Eén resultaatrekening met een bedrag per jaar (besluit 160).</summary>
public sealed partial class MeerjarenRij : ObservableObject
{
    private readonly string _origineel;

    public MeerjarenRij(MeerjarenOverzichtRegel regel, IReadOnlyList<int> jaren)
    {
        Rekening = regel.Rekening;
        Cellen = [.. jaren.Zip(regel.Bedragen, (j, b) => new JaarCel(j, b == Domain.Financien.Bedrag.Nul ? string.Empty : Weergave.Invoer(b)))];
        Toelichting = regel.Toelichting ?? string.Empty;
        foreach (var cel in Cellen)
        {
            cel.PropertyChanged += (_, _) => Meld();
        }

        _origineel = Momentopname();
    }

    public Grootboekrekening Rekening { get; }
    public string Rekeningtekst => Rekening.ToString();
    public string Kanttekst => Rekening.Kant == ResultaatKant.Baten ? "Baten" : "Lasten";
    public IReadOnlyList<JaarCel> Cellen { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Toelichting { get; set; }

    public string? Fout => Cellen.FirstOrDefault(c => !c.IsGeldig) is { } cel
        ? $"{Rekeningtekst}, {cel.Jaar}: vul een bedrag in zoals 1.250,00."
        : null;

    public bool IsGewijzigd => !string.Equals(Momentopname(), _origineel, StringComparison.Ordinal);

    internal MeerjarenInvoer Invoer => new(Rekening.Id, [.. Cellen.Select(c => c.Bedrag ?? Domain.Financien.Bedrag.Nul)], Toelichting);

    /// <summary>Besluit 166: het bedrag van <paramref name="vanafJaar"/> naar alle latere jaren.</summary>
    public void KopieerNaarVolgendeJaren(int vanafJaar)
    {
        if (Cellen.FirstOrDefault(c => c.Jaar == vanafJaar) is not { Bedrag: { } bedrag })
        {
            return;
        }

        var tekst = bedrag == Domain.Financien.Bedrag.Nul ? string.Empty : Weergave.Invoer(bedrag);
        foreach (var cel in Cellen.Where(c => c.Jaar > vanafJaar))
        {
            cel.Tekst = tekst;
        }
    }

    private void Meld()
    {
        OnPropertyChanged(nameof(Fout));
        OnPropertyChanged(nameof(IsGewijzigd));
    }

    private string Momentopname() => string.Join('|', Cellen.Select(c => c.Bedrag?.Centen)) + "|" + Toelichting.Trim();
}
