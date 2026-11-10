using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Bijlagen;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Bijlagen;

/// <summary>
/// Stelt bij een bon uit het postvak posten voor: precies hetzelfde bedrag, datum hooguit 7 dagen ernaast. Een
/// creditnota (negatief bedrag) hoort bij een post waarbij hetzelfde bedrag op bank of kas binnenkomt.
/// </summary>
public static class KoppelVoorsteller
{
    public const int MaximaalAantalDagen = 7;

    /// <summary>Dichtstbijzijnde datum eerst; posten die al een bijlage hebben achteraan.</summary>
    public static IReadOnlyList<Journaalpost> Voorstellen(BoekhoudingStand stand, Bijlage bijlage)
    {
        ArgumentNullException.ThrowIfNull(stand);
        ArgumentNullException.ThrowIfNull(bijlage);
        return Voorstellen(stand, bijlage.Datum, bijlage.Bedrag);
    }

    /// <summary>Voor datum en bedrag die de gebruiker nog aan het invullen is.</summary>
    public static IReadOnlyList<Journaalpost> Voorstellen(BoekhoudingStand stand, DateOnly? datum, Bedrag? bedrag)
    {
        ArgumentNullException.ThrowIfNull(stand);
        if (datum is not { } d || bedrag is not { } b)
        {
            return [];
        }

        return Zoek(stand, d, b);
    }

    private static Journaalpost[] Zoek(BoekhoudingStand stand, DateOnly datum, Bedrag bedrag)
    {
        return [.. stand.Posten
            .Where(p => (bedrag.Centen < 0 ? ZonderBon.Geldbeweging(p).Centen == bedrag.Centen : p.TotaalDebet == bedrag)
                && Math.Abs(p.Datum.DayNumber - datum.DayNumber) <= MaximaalAantalDagen)
            .OrderBy(p => stand.HeeftBijlage(p.Id))
            .ThenBy(p => Math.Abs(p.Datum.DayNumber - datum.DayNumber))
            .ThenBy(p => p.Datum)];
    }
}

/// <summary>
/// Zonder bon: posten in een bank- of kasdagboek waarbij er geld van de bank of uit de kas
/// gaat of erbij komt, boven de drempel, zonder bijlage. Concepten tellen mee.
/// </summary>
public static class ZonderBon
{
    /// <summary>Standaard € 10.</summary>
    public static Bedrag StandaardDrempel { get; } = new(1000);

    public static IReadOnlyList<Journaalpost> Posten(BoekhoudingStand stand, Bedrag drempel)
    {
        ArgumentNullException.ThrowIfNull(stand);
        return [.. stand.Posten.Where(p => Math.Abs(Geldbeweging(p).Centen) > drempel.Centen && !stand.HeeftBijlage(p.Id))];
    }

    /// <summary>
    /// Wat er per saldo van de bank of uit de kas gaat (positief) of erbij komt (negatief), zoals op een bon of creditnota;
    /// nul buiten een bank- of kasdagboek.
    /// </summary>
    public static Bedrag Geldbeweging(Journaalpost post)
    {
        ArgumentNullException.ThrowIfNull(post);
        if (post.Dagboek is not { Soort: DagboekSoort.Bank or DagboekSoort.Kas, Rekening: { } geld })
        {
            return Bedrag.Nul;
        }

        return new Bedrag(post.Regels.Where(r => r.Rekening.Nummer == geld.Nummer).Sum(r => r.Credit.Centen - r.Debet.Centen));
    }
}
