using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Bank;
using Verenigd.Domain.Financien.Debiteuren;

namespace Verenigd.Application.Debiteuren;

public enum Vorderingstatus
{
    /// <summary>Geboekt; de bank heeft de incasso nog niet uitgekeerd.</summary>
    Verwacht,

    Betaald,

    /// <summary>Teruggeboekt; staat open (besluit 92).</summary>
    Gestorneerd,

    /// <summary>Afgeboekt als oninbaar (besluit 101).</summary>
    Afgeboekt,
}

/// <summary>De stand van één contributievordering (één incassopost).</summary>
public sealed record Vorderingstand(
    string Vordering, Incassorun Run, Incassopost Post, long Saldo, Vorderingstatus Status, string? Reden, DateOnly LaatsteDatum,
    bool HerincassoGepland)
{
    public Bedrag Bedrag => Post.Bedrag;

    public string? Redentekst => Reden is null ? null : SepaRetourreden.Weergave(Reden);

    public bool IsOpen => Saldo > 0 && Status is Vorderingstatus.Gestorneerd;
}

/// <summary>Voorgestelde mutaties op vorderingen bij een herkende banktransactie.</summary>
public sealed record Afletterregel(string Vordering, Guid BetalerId, Guid? IncassorunId, DebiteurmutatieSoort Soort, long Saldo, string? Reden);

/// <summary>
/// Berekent de stand van contributievorderingen uit de mutaties. Een mutatie telt alleen als haar journaalpost bestaat en
/// niet is tegengeboekt; zo maken een verwijderd concept of een tegenboeking een aflettering vanzelf ongedaan.
/// </summary>
public static class Contributievorderingen
{
    public static IEnumerable<Debiteurmutatie> Effectief(BoekhoudingStand stand)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var posten = stand.Posten.Select(p => p.Id).ToHashSet();
        var tegengeboekt = stand.Posten.Where(p => p.TegenboekingVan is not null).Select(p => p.TegenboekingVan!.Value).ToHashSet();
        return stand.Debiteurmutaties.Where(m => posten.Contains(m.PostId) && !tegengeboekt.Contains(m.PostId));
    }

    public static bool IsGeboekt(BoekhoudingStand stand, Guid runId) =>
        Effectief(stand).Any(m => m.IncassorunId == runId && m.Soort == DebiteurmutatieSoort.Vordering);

    /// <summary>De bank heeft de batch van de run uitgekeerd.</summary>
    public static bool IsOntvangen(BoekhoudingStand stand, Guid runId) =>
        Effectief(stand).Any(m => m.IncassorunId == runId && m.Soort == DebiteurmutatieSoort.Ontvangst);

    public static IReadOnlyList<Vorderingstand> Bereken(BoekhoudingStand stand)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var posten = stand.Incassoruns
            .SelectMany(r => r.Posten.Select(p => (Run: r, Post: p)))
            .ToDictionary(x => x.Post.EndToEndId, StringComparer.Ordinal);
        var gepland = stand.Herincassos.Where(h => h.IsOpen).Select(h => h.Vordering).ToHashSet(StringComparer.Ordinal);
        var lijst = new List<Vorderingstand>();
        foreach (var groep in Effectief(stand).GroupBy(m => m.Vordering, StringComparer.Ordinal))
        {
            if (!groep.Any(m => m.Soort == DebiteurmutatieSoort.Vordering) || !posten.TryGetValue(groep.Key, out var bron))
            {
                continue;
            }

            var mutaties = groep.OrderBy(m => m.Datum).ThenBy(m => m.Soort).ToList();
            var saldo = mutaties.Sum(m => m.Saldo);
            var laatste = mutaties[^1];
            var status = saldo <= 0
                ? (mutaties.Exists(m => m.Soort == DebiteurmutatieSoort.Oninbaar) ? Vorderingstatus.Afgeboekt : Vorderingstatus.Betaald)
                : mutaties.Exists(m => m.Soort == DebiteurmutatieSoort.Stornering) ? Vorderingstatus.Gestorneerd : Vorderingstatus.Verwacht;
            var reden = mutaties.LastOrDefault(m => m.Soort == DebiteurmutatieSoort.Stornering)?.Reden;
            lijst.Add(new Vorderingstand(groep.Key, bron.Run, bron.Post, saldo, status, reden, laatste.Datum, gepland.Contains(groep.Key)));
        }

        return [.. lijst.OrderBy(v => v.Run.Maand).ThenBy(v => v.Post.Rekeninghouder, StringComparer.CurrentCulture)];
    }

    /// <summary>
    /// Herkent een banktransactie als contributie (in deze volgorde): de eigen incassobatch, een stornering van een eigen
    /// post, of een handmatige betaling van precies één open post (besluit 102).
    /// </summary>
    internal static (string Omschrijving, IReadOnlyList<Afletterregel> Regels)? Herken(BoekhoudingStand stand, Bankmutatie mutatie)
    {
        var runs = stand.Incassoruns.Where(r => r.Status == IncassorunStatus.Definitief).ToList();
        if (mutatie.IsBij && mutatie.BatchKenmerk is { } kenmerk
            && runs.FirstOrDefault(r => kenmerk == r.Kenmerk + "-1") is { } run
            && run.Totaal == mutatie.Bedrag && !IsOntvangen(stand, run.Id))
        {
            return ($"Incasso contributie {run.Maand}",
                [.. run.Posten.Select(p => new Afletterregel(p.Vordering, p.BetalerId, run.Id, DebiteurmutatieSoort.Ontvangst, -p.Bedrag.Centen, null))]);
        }

        if (!mutatie.IsBij && mutatie.EndToEndId is { } e2e
            && runs.SelectMany(r => r.Posten.Select(p => (Run: r, Post: p))).FirstOrDefault(x => x.Post.EndToEndId == e2e) is { Post: not null } storno
            && storno.Post.Bedrag == mutatie.Bedrag)
        {
            var reden = mutatie.Retourreden is null ? string.Empty : " – " + SepaRetourreden.Omschrijving(mutatie.Retourreden);
            return ($"Stornering contributie {storno.Run.Maand}: {storno.Post.Rekeninghouder}{reden}",
                [new Afletterregel(storno.Post.Vordering, storno.Post.BetalerId, null, DebiteurmutatieSoort.Stornering, mutatie.Bedrag.Centen, mutatie.Retourreden)]);
        }

        if (mutatie.IsBij && mutatie.TegenIban is { } iban)
        {
            var kandidaten = Bereken(stand).Where(v => v.IsOpen && v.Post.Iban.Waarde == iban && v.Saldo == mutatie.Bedrag.Centen).ToList();
            if (kandidaten.Count == 1)
            {
                var v = kandidaten[0];
                return ($"Betaling contributie {v.Run.Maand}: {v.Post.Rekeninghouder}",
                    [new Afletterregel(v.Vordering, v.Post.BetalerId, null, DebiteurmutatieSoort.Betaling, -v.Saldo, null)]);
            }
        }

        return null;
    }
}
