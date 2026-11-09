using BtwBoeker.Domain.Financien;

namespace BtwBoeker.Application.Btw;

public enum BtwBoekingSoort
{
    Omzet,
    Voorbelasting,
    PrivegebruikCorrectie,
}

public sealed record BtwBoeking(DateOnly Boekdatum, BtwBoekingSoort Soort, int? Percentage, Bedrag Grondslag, Bedrag Btw);

public sealed record BtwTariefBedragen(
    int Percentage,
    Bedrag OmzetGrondslag,
    Bedrag OmzetBtw,
    Bedrag VoorbelastingGrondslag,
    Bedrag Voorbelasting);

public sealed record MaandBtwRapport(int Jaar, int Maand, IReadOnlyList<BtwTariefBedragen> Tarieven, Bedrag PrivegebruikCorrectie)
{
    public Bedrag OmzetGrondslag => Tarieven.Select(t => t.OmzetGrondslag).Som();

    public Bedrag OmzetBtw => Tarieven.Select(t => t.OmzetBtw).Som();

    public Bedrag Voorbelasting => Tarieven.Select(t => t.Voorbelasting).Som();

    public Bedrag TeBetalen => new(OmzetBtw.Centen + PrivegebruikCorrectie.Centen - Voorbelasting.Centen);
}

public static class BtwRapportRekenaar
{
    public static MaandBtwRapport Bereken(int jaar, int maand, IEnumerable<BtwBoeking> boekingen)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(jaar, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maand, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maand, 12);
        ArgumentNullException.ThrowIfNull(boekingen);

        var maandboekingen = boekingen
            .Where(b => b.Boekdatum.Year == jaar && b.Boekdatum.Month == maand)
            .ToList();
        var tarieven = maandboekingen
            .Where(b => b.Soort is BtwBoekingSoort.Omzet or BtwBoekingSoort.Voorbelasting)
            .GroupBy(b => b.Percentage ?? 0)
            .OrderBy(g => g.Key)
            .Select(g => new BtwTariefBedragen(
                g.Key,
                Som(g, BtwBoekingSoort.Omzet, b => b.Grondslag),
                Som(g, BtwBoekingSoort.Omzet, b => b.Btw),
                Som(g, BtwBoekingSoort.Voorbelasting, b => b.Grondslag),
                Som(g, BtwBoekingSoort.Voorbelasting, b => b.Btw)))
            .ToList();
        var correctie = maandboekingen
            .Where(b => b.Soort == BtwBoekingSoort.PrivegebruikCorrectie)
            .Select(b => b.Btw)
            .Som();

        return new MaandBtwRapport(jaar, maand, tarieven, correctie);
    }

    private static Bedrag Som(
        IEnumerable<BtwBoeking> boekingen,
        BtwBoekingSoort soort,
        Func<BtwBoeking, Bedrag> selector) =>
        boekingen.Where(b => b.Soort == soort).Select(selector).Som();
}
