using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Boekhouding;

/// <summary>Berekeningen op het grootboek. Alleen geboekte posten tellen (besluit 71); niets wordt apart opgeslagen.</summary>
public static class Grootboek
{
    public static IEnumerable<Journaalpost> GeboektePosten(BoekhoudingStand stand, int jaar)
    {
        ArgumentNullException.ThrowIfNull(stand);
        return stand.Posten.Where(p => p.IsGeboekt && p.Datum.Year == jaar);
    }

    /// <summary>Saldo (debet positief) binnen het boekjaar, inclusief de openingsbalans.</summary>
    public static long Saldo(BoekhoudingStand stand, Grootboekrekening rekening, int jaar, DateOnly? totEnMet = null) =>
        Regels(stand, rekening, jaar)
            .Where(r => totEnMet is null || r.Post.Datum <= totEnMet)
            .Sum(r => r.Regel.Saldo);

    public static Saldibalans Saldibalans(BoekhoudingStand stand, int jaar)
    {
        var regels = GeboektePosten(stand, jaar)
            .SelectMany(p => p.Regels)
            .GroupBy(r => r.Rekening)
            .Select(g => new SaldiRegel(g.Key, g.Select(r => r.Debet).Som(), g.Select(r => r.Credit).Som()))
            .OrderBy(r => r.Rekening.Nummer)
            .ToList();
        return new Saldibalans(jaar, regels);
    }

    /// <summary>Staat van baten en lasten per rubriek (besluit 69), zonder de afsluitpost.</summary>
    public static WinstEnVerlies WinstEnVerlies(BoekhoudingStand stand, int jaar)
    {
        var saldi = GeboektePosten(stand, jaar)
            .Where(p => p.Soort != PostSoort.Afsluiting)
            .SelectMany(p => p.Regels)
            .Where(r => r.Rekening.Soort == RekeningSoort.Resultaat)
            .GroupBy(r => r.Rekening)
            .Select(g => (Rekening: g.Key, Saldo: g.Sum(r => r.Saldo)))
            .Where(s => s.Saldo != 0)
            .ToList();
        return new WinstEnVerlies(
            jaar,
            PerRubriek(stand, saldi.Where(s => s.Saldo < 0).Select(s => (s.Rekening, new Bedrag(-s.Saldo)))),
            PerRubriek(stand, saldi.Where(s => s.Saldo > 0).Select(s => (s.Rekening, new Bedrag(s.Saldo)))));
    }

    /// <summary>Balans per 31-12; zolang het jaar niet is afgesloten staat het resultaat apart aan de passivazijde.</summary>
    public static Balans Balans(BoekhoudingStand stand, int jaar)
    {
        var regels = GeboektePosten(stand, jaar).SelectMany(p => p.Regels).ToList();
        var balans = regels
            .Where(r => r.Rekening.Soort == RekeningSoort.Balans)
            .GroupBy(r => r.Rekening)
            .Select(g => (Rekening: g.Key, Saldo: g.Sum(r => r.Saldo)))
            .Where(s => s.Saldo != 0)
            .ToList();
        var resultaat = -regels.Where(r => r.Rekening.Soort == RekeningSoort.Resultaat).Sum(r => r.Saldo);
        return new Balans(
            jaar,
            PerRubriek(stand, balans.Where(s => s.Saldo > 0).Select(s => (s.Rekening, new Bedrag(s.Saldo)))),
            PerRubriek(stand, balans.Where(s => s.Saldo < 0).Select(s => (s.Rekening, new Bedrag(-s.Saldo)))),
            resultaat);
    }

    public static Grootboekkaart Kaart(BoekhoudingStand stand, Grootboekrekening rekening, int jaar, DateOnly van, DateOnly tot)
    {
        var regels = Regels(stand, rekening, jaar).OrderBy(r => r.Post.Datum).ThenBy(r => r.Post.Volgnummer).ToList();
        var saldo = regels.Where(r => r.Post.Datum < van).Sum(r => r.Regel.Saldo);
        var begin = saldo;
        var mutaties = new List<KaartMutatie>();
        foreach (var (post, regel) in regels.Where(r => r.Post.Datum >= van && r.Post.Datum <= tot))
        {
            saldo += regel.Saldo;
            mutaties.Add(new KaartMutatie(post.Id, post.Datum, post.Nummer, regel.Omschrijving ?? post.Omschrijving, regel.Debet, regel.Credit, saldo));
        }

        return new Grootboekkaart(rekening, van, tot, begin, mutaties);
    }

    private static IEnumerable<(Journaalpost Post, Journaalregel Regel)> Regels(BoekhoudingStand stand, Grootboekrekening rekening, int jaar) =>
        GeboektePosten(stand, jaar).SelectMany(p => p.Regels.Where(r => ZelfdeRekening(r.Rekening, rekening)).Select(r => (p, r)));

    internal static bool ZelfdeRekening(Grootboekrekening a, Grootboekrekening b) => ReferenceEquals(a, b) || (a.Id != 0 && a.Id == b.Id);

    private static List<RubriekBedragen> PerRubriek(BoekhoudingStand stand, IEnumerable<(Grootboekrekening Rekening, Bedrag Bedrag)> bedragen) =>
        [.. bedragen
            .GroupBy(b => b.Rekening.Rubriek)
            .OrderBy(g => g.Key?.Volgorde ?? int.MaxValue)
            .Select(g => new RubriekBedragen(g.Key?.Naam ?? "Overig", [.. g.OrderBy(b => b.Rekening.Nummer)]))];
}
