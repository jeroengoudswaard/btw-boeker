using CommunityToolkit.Mvvm.ComponentModel;
using Verenigd.Application.Begrotingen;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Begrotingen;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.App.ViewModels.Boekhouding;

/// <summary>Eén versie in de keuzelijst, bijvoorbeeld "Versie 1 · Oorspronkelijk · Vastgesteld 12-12-2026".</summary>
public sealed record BegrotingVersie(Begroting Begroting)
{
    public int Id => Begroting.Id;

    public string Tekst => $"Versie {Begroting.Versie} · {Begroting.Omschrijving} · "
        + (Begroting.IsVastgesteld ? $"Vastgesteld {Weergave.Datum(Begroting.VastgesteldOp)}" : "Concept");

    public override string ToString() => Tekst;
}

/// <summary>Eén resultaatrekening in de begroting; het bedrag is met de hand bij te stellen (besluit 150).</summary>
public sealed partial class BegrotingRij : ObservableObject
{
    private readonly string _origineel;

    public BegrotingRij(BegrotingOverzichtRegel regel, string? toelichting)
    {
        Rekening = regel.Rekening;
        WerkelijkVorigJaar = Tekst(regel.WerkelijkVorigJaar);
        Werkelijk = Tekst(regel.Werkelijk);
        NaarRato = Tekst(regel.NaarRato);
        Verschil = regel.Begroot == Domain.Financien.Bedrag.Nul && regel.Werkelijk == Domain.Financien.Bedrag.Nul ? string.Empty : new Domain.Financien.Bedrag(regel.Verschil).ToString();
        IsNegatiefVerschil = regel.Rekening.Kant == ResultaatKant.Baten ? regel.Verschil < 0 : regel.Verschil > 0;
        Bedragtekst = regel.Begroot == Domain.Financien.Bedrag.Nul ? string.Empty : Weergave.Invoer(regel.Begroot);
        Toelichting = toelichting ?? string.Empty;
        _origineel = Momentopname();
    }

    public Grootboekrekening Rekening { get; }
    public string Rekeningtekst => Rekening.ToString();
    public string Kanttekst => Rekening.Kant == ResultaatKant.Baten ? "Baten" : "Lasten";
    public string Rubriek => Rekening.Rubriek?.Naam ?? "Overig";
    public string WerkelijkVorigJaar { get; }
    public string Werkelijk { get; }
    public string NaarRato { get; }
    public string Verschil { get; }

    /// <summary>Ongunstig verschil: minder baten of meer lasten dan naar rato verwacht.</summary>
    public bool IsNegatiefVerschil { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout), nameof(Bedrag))]
    public partial string Bedragtekst { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd))]
    public partial string Toelichting { get; set; }

    /// <summary>Leeg is nul; <c>null</c> bij een ongeldig bedrag.</summary>
    public Bedrag? Bedrag => string.IsNullOrWhiteSpace(Bedragtekst) ? Domain.Financien.Bedrag.Nul
        : Domain.Financien.Bedrag.TryParse(Bedragtekst, out var bedrag) ? bedrag : null;

    public string? Fout => Bedrag is null ? $"{Rekeningtekst}: vul een bedrag in zoals 1.250,00." : null;

    public bool IsGewijzigd => !string.Equals(Momentopname(), _origineel, StringComparison.Ordinal);

    internal BegrotingInvoer Invoer => new(Rekening.Id, Bedrag ?? Domain.Financien.Bedrag.Nul, Toelichting);

    private string Momentopname() => $"{Bedrag?.Centen}|{Toelichting.Trim()}";

    private static string Tekst(Domain.Financien.Bedrag bedrag) => bedrag == Domain.Financien.Bedrag.Nul ? string.Empty : bedrag.ToString();
}
