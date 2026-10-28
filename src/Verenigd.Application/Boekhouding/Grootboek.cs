using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Boekhouding;

/// <summary>
/// Berekeningen op het grootboek. Alleen geboekte posten tellen (besluit 71); niets wordt apart opgeslagen. Een historisch
/// jaar (besluit 122) heeft geen posten: saldi en rapporten komen dan uit de totalen van het Voorblad, de kaart uit de
/// regels van de T-rekeningen.
/// </summary>
public static class Grootboek
{
    public static IEnumerable<Journaalpost> GeboektePosten(BoekhoudingStand stand, int jaar)
    {
        ArgumentNullException.ThrowIfNull(stand);
        return stand.Posten.Where(p => p.IsGeboekt && p.Datum.Year == jaar);
    }

    /// <summary>Saldo (debet positief) binnen het boekjaar, inclusief de openingsbalans.</summary>
    public static long Saldo(BoekhoudingStand stand, Grootboekrekening rekening, int jaar, DateOnly? totEnMet = null) =>
        stand.IsHistorisch(jaar) ? Totaal(stand, rekening, jaar)?.Saldo ?? 0 : Regels(stand, rekening, jaar)
            .Where(r => totEnMet is null || r.Post.Datum <= totEnMet)
            .Sum(r => r.Regel.Saldo);

    public static Saldibalans Saldibalans(BoekhoudingStand stand, int jaar)
    {
        if (stand.IsHistorisch(jaar))
        {
            return new Saldibalans(jaar, [.. Totalen(stand, jaar).Select(t => new SaldiRegel(t.Rekening, t.Debet, t.Credit)).OrderBy(r => r.Rekening.Nummer)]);
        }

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
        var saldi = (stand.IsHistorisch(jaar)
                ? Totalen(stand, jaar).Where(t => t.Rekening.Soort == RekeningSoort.Resultaat).Select(t => (t.Rekening, t.Saldo))
                : GeboektePosten(stand, jaar)
                    .Where(p => p.Soort != PostSoort.Afsluiting)
                    .SelectMany(p => p.Regels)
                    .Where(r => r.Rekening.Soort == RekeningSoort.Resultaat)
                    .GroupBy(r => r.Rekening)
                    .Select(g => (Rekening: g.Key, Saldo: g.Sum(r => r.Saldo))))
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
        var saldi = stand.IsHistorisch(jaar)
            ? [.. Totalen(stand, jaar).Select(t => (t.Rekening, t.Saldo))]
            : GeboektePosten(stand, jaar).SelectMany(p => p.Regels).GroupBy(r => r.Rekening).Select(g => (Rekening: g.Key, Saldo: g.Sum(r => r.Saldo))).ToList();
        var balans = saldi.Where(s => s.Rekening.Soort == RekeningSoort.Balans && s.Saldo != 0).ToList();
        var resultaat = -saldi.Where(s => s.Rekening.Soort == RekeningSoort.Resultaat).Sum(s => s.Saldo);
        return new Balans(
            jaar,
            PerRubriek(stand, balans.Where(s => s.Saldo > 0).Select(s => (s.Rekening, new Bedrag(s.Saldo)))),
            PerRubriek(stand, balans.Where(s => s.Saldo < 0).Select(s => (s.Rekening, new Bedrag(-s.Saldo)))),
            resultaat);
    }

    public static Grootboekkaart Kaart(BoekhoudingStand stand, Grootboekrekening rekening, int jaar, DateOnly van, DateOnly tot)
    {
        if (stand.IsHistorisch(jaar))
        {
            return HistorischeKaart(stand, rekening, jaar, van, tot);
        }

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

    private static Grootboekkaart HistorischeKaart(BoekhoudingStand stand, Grootboekrekening rekening, int jaar, DateOnly van, DateOnly tot)
    {
        var regels = stand.HistorischeRegels.Where(r => r.Jaar == jaar && ZelfdeRekening(r.Rekening, rekening))
            .OrderBy(r => r.Datum).ThenBy(r => r.Volgorde).ToList();
        var saldo = regels.Where(r => r.Datum < van).Sum(r => r.Saldo);
        var begin = saldo;
        var mutaties = new List<KaartMutatie>();
        foreach (var regel in regels.Where(r => r.Datum >= van && r.Datum <= tot))
        {
            saldo += regel.Saldo;
            mutaties.Add(new KaartMutatie(Guid.Empty, regel.Datum, regel.Soort == HistorischeRegelSoort.Saldoregel ? "Saldo" : "Excel",
                regel.Omschrijving, regel.Debet, regel.Credit, saldo));
        }

        return new Grootboekkaart(rekening, van, tot, begin, mutaties) { IsHistorisch = true, TotaalVolgensVoorblad = Totaal(stand, rekening, jaar)?.Saldo };
    }

    private static IEnumerable<HistorischTotaal> Totalen(BoekhoudingStand stand, int jaar) => stand.HistorischeTotalen.Where(t => t.Jaar == jaar);

    private static HistorischTotaal? Totaal(BoekhoudingStand stand, Grootboekrekening rekening, int jaar) =>
        Totalen(stand, jaar).FirstOrDefault(t => ZelfdeRekening(t.Rekening, rekening));

    private static IEnumerable<(Journaalpost Post, Journaalregel Regel)> Regels(BoekhoudingStand stand, Grootboekrekening rekening, int jaar) =>
        GeboektePosten(stand, jaar).SelectMany(p => p.Regels.Where(r => ZelfdeRekening(r.Rekening, rekening)).Select(r => (p, r)));

    internal static bool ZelfdeRekening(Grootboekrekening a, Grootboekrekening b) => ReferenceEquals(a, b) || (a.Id != 0 && a.Id == b.Id);

    private static List<RubriekBedragen> PerRubriek(BoekhoudingStand stand, IEnumerable<(Grootboekrekening Rekening, Bedrag Bedrag)> bedragen) =>
        [.. bedragen
            .GroupBy(b => b.Rekening.Rubriek)
            .OrderBy(g => g.Key?.Volgorde ?? int.MaxValue)
            .Select(g => new RubriekBedragen(g.Key?.Naam ?? "Overig", [.. g.OrderBy(b => b.Rekening.Nummer)]))];
}
