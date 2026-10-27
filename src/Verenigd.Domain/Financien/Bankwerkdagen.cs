namespace Verenigd.Domain.Financien;

/// <summary>
/// TARGET-kalender van het Europese betalingsverkeer: geen werkdag op zaterdag, zondag, 1 januari, Goede Vrijdag,
/// Tweede Paasdag, 1 mei, 25 en 26 december (besluit 59).
/// </summary>
public static class Bankwerkdagen
{
    public static bool IsWerkdag(DateOnly datum)
    {
        if (datum.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        var pasen = Pasen(datum.Year);
        return (datum.Month, datum.Day) is not ((1, 1) or (5, 1) or (12, 25) or (12, 26))
            && datum != pasen.AddDays(-2)
            && datum != pasen.AddDays(1);
    }

    /// <summary>De datum zelf als die een werkdag is, anders de eerstvolgende werkdag.</summary>
    public static DateOnly OpOfNa(DateOnly datum)
    {
        while (!IsWerkdag(datum))
        {
            datum = datum.AddDays(1);
        }

        return datum;
    }

    /// <summary>Telt <paramref name="aantal"/> werkdagen op bij <paramref name="datum"/>.</summary>
    public static DateOnly TelOp(DateOnly datum, int aantal)
    {
        for (var i = 0; i < aantal; i++)
        {
            datum = OpOfNa(datum.AddDays(1));
        }

        return datum;
    }

    /// <summary>Paaszondag (Gregoriaans, algoritme van Meeus/Jones/Butcher).</summary>
    public static DateOnly Pasen(int jaar)
    {
        var a = jaar % 19;
        var b = jaar / 100;
        var c = jaar % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var maand = (h + l - (7 * m) + 114) / 31;
        var dag = ((h + l - (7 * m) + 114) % 31) + 1;
        return new DateOnly(jaar, maand, dag);
    }
}
