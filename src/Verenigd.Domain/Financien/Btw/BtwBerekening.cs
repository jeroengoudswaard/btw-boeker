namespace Verenigd.Domain.Financien.Btw;

public readonly record struct BtwTarief(int Percentage);

public sealed record BtwBedragen(Bedrag Grondslag, Bedrag Btw, Bedrag Totaal);

public static class BtwTarieven
{
    public static BtwTarief AfzonderlijkeFaciliteit { get; } = new(9);

    public static BtwTarief Logies(DateOnly verblijfsdatum) =>
        verblijfsdatum < new DateOnly(2026, 1, 1) ? new BtwTarief(9) : new BtwTarief(21);
}

public static class BtwBerekening
{
    public static BtwBedragen Bereken(Bedrag bedrag, int percentage, bool bedragInclusiefBtw)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bedrag.Centen);
        ArgumentOutOfRangeException.ThrowIfNegative(percentage);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percentage, 100);

        var grondslag = bedragInclusiefBtw
            ? new Bedrag(Afronden((decimal)bedrag.Centen * 100 / (100 + percentage)))
            : bedrag;
        var btw = bedragInclusiefBtw
            ? new Bedrag(bedrag.Centen - grondslag.Centen)
            : new Bedrag(Afronden((decimal)grondslag.Centen * percentage / 100));
        var totaal = bedragInclusiefBtw ? bedrag : grondslag + btw;

        return new BtwBedragen(grondslag, btw, totaal);
    }

    private static long Afronden(decimal centen) =>
        checked((long)Math.Round(centen, MidpointRounding.AwayFromZero));
}
