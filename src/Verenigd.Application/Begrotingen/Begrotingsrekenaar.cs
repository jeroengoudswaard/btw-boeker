using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Begrotingen;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Begrotingen;

public enum BegrotingBron
{
    /// <summary>De werkelijke cijfers van een jaar.</summary>
    Werkelijk,

    /// <summary>De geldende begroting van een jaar.</summary>
    Begroting,
}

/// <summary>Eén resultaatrekening; alle bedragen in de richting van de rekening (baten of lasten, besluit 152).</summary>
public sealed record BegrotingOverzichtRegel(Grootboekrekening Rekening, Bedrag WerkelijkVorigJaar, Bedrag Begroot, Bedrag Werkelijk, Bedrag NaarRato)
{
    /// <summary>Werkelijk min naar rato: bij baten is positief gunstig, bij lasten ongunstig.</summary>
    public long Verschil => Werkelijk.Centen - NaarRato.Centen;
}

public sealed record BegrotingOverzichtRubriek(string Naam, IReadOnlyList<BegrotingOverzichtRegel> Regels)
{
    public Bedrag Totaal(Func<BegrotingOverzichtRegel, Bedrag> kolom) => Regels.Select(kolom).Som();
}

/// <summary>Begroot tegenover werkelijk voor één jaar (besluiten 147 en 148).</summary>
public sealed record Begrotingsoverzicht(
    int Jaar, Begroting? Begroting, decimal Fractie, IReadOnlyList<BegrotingOverzichtRubriek> Baten, IReadOnlyList<BegrotingOverzichtRubriek> Lasten)
{
    public Bedrag TotaalBegroot(ResultaatKant kant) => Kant(kant).SelectMany(r => r.Regels).Select(r => r.Begroot).Som();

    public Bedrag Totaal(ResultaatKant kant, Func<BegrotingOverzichtRegel, Bedrag> kolom) => Kant(kant).SelectMany(r => r.Regels).Select(kolom).Som();

    /// <summary>Begrote baten min lasten; positief is een overschot.</summary>
    public long BegrootResultaat => TotaalBegroot(ResultaatKant.Baten).Centen - TotaalBegroot(ResultaatKant.Lasten).Centen;

    private IReadOnlyList<BegrotingOverzichtRubriek> Kant(ResultaatKant kant) => kant == ResultaatKant.Baten ? Baten : Lasten;
}

/// <summary>Berekeningen voor de begroting (besluiten 147–150 en 152–153). Telt nooit mee in het grootboek.</summary>
public static class Begrotingsrekenaar
{
    /// <summary>De laatste vastgestelde versie, anders het nieuwste concept (besluit 149).</summary>
    public static Begroting? Geldend(BoekhoudingStand stand, int jaar)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var versies = stand.Begrotingen.Where(b => b.Jaar == jaar).ToList();
        return versies.Where(b => b.IsVastgesteld).MaxBy(b => b.Versie) ?? versies.MaxBy(b => b.Versie);
    }

    /// <summary>Het werkelijke bedrag in de richting van de rekening: baten als credit, lasten als debet.</summary>
    public static Bedrag Werkelijk(BoekhoudingStand stand, Grootboekrekening rekening, int jaar, DateOnly? totEnMet = null)
    {
        ArgumentNullException.ThrowIfNull(rekening);
        var saldo = Grootboek.Resultaat(stand, rekening, jaar, totEnMet);
        return new Bedrag(rekening.Kant == ResultaatKant.Baten ? -saldo : saldo);
    }

    /// <summary>
    /// Bedragen per resultaatrekening uit de werkelijke cijfers of de geldende begroting van <paramref name="bronjaar"/>,
    /// verhoogd met <paramref name="percentage"/> en afgerond op tientallen euro's (besluit 153). Negatief wordt nul.
    /// </summary>
    public static IReadOnlyDictionary<Grootboekrekening, Bedrag> Overnemen(BoekhoudingStand stand, BegrotingBron bron, int bronjaar, decimal percentage)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var begroting = bron == BegrotingBron.Begroting ? Geldend(stand, bronjaar) : null;
        var uitkomst = new Dictionary<Grootboekrekening, Bedrag>();
        foreach (var rekening in stand.Rekeningen.Where(r => r.Soort == RekeningSoort.Resultaat))
        {
            var basis = bron == BegrotingBron.Werkelijk ? Werkelijk(stand, rekening, bronjaar) : begroting?.Bedrag(rekening) ?? Bedrag.Nul;
            uitkomst[rekening] = Tientallen(basis.Centen * (1 + (percentage / 100m)));
        }

        return uitkomst;
    }

    /// <summary>
    /// Begroot tegenover werkelijk. In het lopende jaar wordt vergeleken naar rato per dag (besluit 148), daarvoor met het
    /// volle bedrag, daarna met nul. Rekeningen zonder bedragen en zonder activiteit vallen weg.
    /// </summary>
    public static Begrotingsoverzicht Overzicht(BoekhoudingStand stand, int jaar, DateOnly vandaag)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var begroting = Geldend(stand, jaar);
        var fractie = jaar < vandaag.Year ? 1m
            : jaar > vandaag.Year ? 0m
            : (decimal)vandaag.DayOfYear / (DateTime.IsLeapYear(jaar) ? 366 : 365);
        var regels = stand.Rekeningen
            .Where(r => r.Soort == RekeningSoort.Resultaat)
            .Select(r =>
            {
                var begroot = begroting?.Bedrag(r) ?? Bedrag.Nul;
                return new BegrotingOverzichtRegel(r,
                    Werkelijk(stand, r, jaar - 1),
                    begroot,
                    Werkelijk(stand, r, jaar, jaar == vandaag.Year ? vandaag : null),
                    new Bedrag((long)Math.Round(begroot.Centen * fractie, MidpointRounding.AwayFromZero)));
            })
            .Where(r => r.Rekening.IsActief || r.Begroot != Bedrag.Nul || r.Werkelijk != Bedrag.Nul || r.WerkelijkVorigJaar != Bedrag.Nul)
            .ToList();
        return new Begrotingsoverzicht(jaar, begroting, fractie,
            PerRubriek(regels.Where(r => r.Rekening.Kant == ResultaatKant.Baten)),
            PerRubriek(regels.Where(r => r.Rekening.Kant != ResultaatKant.Baten)));
    }

    private static Bedrag Tientallen(decimal centen) =>
        centen <= 0 ? Bedrag.Nul : new Bedrag((long)Math.Round(centen / 1000m, MidpointRounding.AwayFromZero) * 1000);

    private static List<BegrotingOverzichtRubriek> PerRubriek(IEnumerable<BegrotingOverzichtRegel> regels) =>
        [.. regels
            .GroupBy(r => r.Rekening.Rubriek)
            .OrderBy(g => g.Key?.Volgorde ?? int.MaxValue)
            .Select(g => new BegrotingOverzichtRubriek(g.Key?.Naam ?? "Overig", [.. g.OrderBy(r => r.Rekening.Nummer)]))];
}
