using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Bijlagen;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Bijlagen;

/// <summary>Stelt bij een bon uit het postvak posten voor (besluit 85): precies hetzelfde bedrag, datum hooguit 7 dagen ernaast.</summary>
public static class KoppelVoorsteller
{
    public const int MaximaalAantalDagen = 7;

    /// <summary>Dichtstbijzijnde datum eerst; posten die al een bijlage hebben achteraan.</summary>
    public static IReadOnlyList<Journaalpost> Voorstellen(BoekhoudingStand stand, Bijlage bijlage)
    {
        ArgumentNullException.ThrowIfNull(stand);
        ArgumentNullException.ThrowIfNull(bijlage);
        if (bijlage is not { Datum: { } datum, Bedrag: { } bedrag })
        {
            return [];
        }

        return [.. stand.Posten
            .Where(p => p.TotaalDebet == bedrag && Math.Abs(p.Datum.DayNumber - datum.DayNumber) <= MaximaalAantalDagen)
            .OrderBy(p => stand.HeeftBijlage(p.Id))
            .ThenBy(p => Math.Abs(p.Datum.DayNumber - datum.DayNumber))
            .ThenBy(p => p.Datum)];
    }
}

/// <summary>
/// Uitgaven zonder bon (besluiten 88, 97 en 138): posten in een bank- of kasdagboek waarbij er geld van de bank of uit de
/// kas gaat, boven de drempel, zonder bijlage. Concepten tellen mee.
/// </summary>
public static class ZonderBon
{
    /// <summary>Besluit 97: € 10.</summary>
    public static Bedrag StandaardDrempel { get; } = new(1000);

    public static IReadOnlyList<Journaalpost> Posten(BoekhoudingStand stand, Bedrag drempel)
    {
        ArgumentNullException.ThrowIfNull(stand);
        return [.. stand.Posten.Where(p => Uitgave(p) > drempel && !stand.HeeftBijlage(p.Id))];
    }

    /// <summary>Wat er per saldo van de bank of uit de kas gaat; nul als het geen uitgave is.</summary>
    public static Bedrag Uitgave(Journaalpost post)
    {
        ArgumentNullException.ThrowIfNull(post);
        if (post.Dagboek is not { Soort: DagboekSoort.Bank or DagboekSoort.Kas, Rekening: { } geld })
        {
            return Bedrag.Nul;
        }

        var eraf = post.Regels.Where(r => r.Rekening.Nummer == geld.Nummer).Sum(r => r.Credit.Centen - r.Debet.Centen);
        return eraf > 0 ? new Bedrag(eraf) : Bedrag.Nul;
    }
}
