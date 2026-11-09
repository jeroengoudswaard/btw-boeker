using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Begrotingen;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Begrotingen;

/// <summary>Eén resultaatrekening met een bedrag per jaar van het plan.</summary>
public sealed record MeerjarenOverzichtRegel(Grootboekrekening Rekening, IReadOnlyList<Bedrag> Bedragen, string? Toelichting);

/// <summary>Baten en lasten per jaar met het resultaat per jaar (besluit 162).</summary>
public sealed record Meerjarenoverzicht(Meerjarenplan Plan, IReadOnlyList<MeerjarenOverzichtRegel> Baten, IReadOnlyList<MeerjarenOverzichtRegel> Lasten)
{
    public IReadOnlyList<int> Jaren => [.. Plan.Jaren];

    public Bedrag TotaalBaten(int index) => Baten.Select(r => r.Bedragen[index]).Som();

    public Bedrag TotaalLasten(int index) => Lasten.Select(r => r.Bedragen[index]).Som();

    /// <summary>Baten min lasten; positief is een overschot.</summary>
    public long Resultaat(int index) => TotaalBaten(index).Centen - TotaalLasten(index).Centen;
}

/// <summary>Berekeningen voor het meerjarenplan (besluiten 159–164). Telt nooit mee in het grootboek.</summary>
public static class Meerjarenrekenaar
{
    /// <summary>Voor een startjaar: de laatste vastgestelde versie, anders het nieuwste concept.</summary>
    public static Meerjarenplan? Geldend(BoekhoudingStand stand, int startjaar)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var versies = stand.Meerjarenplannen.Where(p => p.Startjaar == startjaar).ToList();
        return versies.Where(p => p.IsVastgesteld).MaxBy(p => p.Versie) ?? versies.MaxBy(p => p.Versie);
    }

    /// <summary>Alle actieve resultaatrekeningen en de rekeningen die in het plan staan, per baten en lasten.</summary>
    public static Meerjarenoverzicht Overzicht(BoekhoudingStand stand, Meerjarenplan plan)
    {
        ArgumentNullException.ThrowIfNull(stand);
        ArgumentNullException.ThrowIfNull(plan);
        var regels = stand.Rekeningen
            .Where(r => r.Soort == RekeningSoort.Resultaat && (r.IsActief || plan.Posten.Any(p => p.Rekening.Id == r.Id)))
            .OrderBy(r => r.Rubriek?.Volgorde ?? int.MaxValue).ThenBy(r => r.Nummer)
            .Select(r => new MeerjarenOverzichtRegel(r, [.. plan.Jaren.Select(j => plan.Bedrag(r, j))], plan.Toelichting(r)))
            .ToList();
        return new Meerjarenoverzicht(plan,
            [.. regels.Where(r => r.Rekening.Kant == ResultaatKant.Baten)],
            [.. regels.Where(r => r.Rekening.Kant != ResultaatKant.Baten)]);
    }
}
