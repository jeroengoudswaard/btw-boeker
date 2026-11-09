using System.Globalization;
using System.Text.RegularExpressions;
using BtwBoeker.Domain.Financien;

namespace BtwBoeker.Application.Bijlagen;

/// <summary>Leest de tekst van een PDF; <c>null</c> als dat niet lukt (bijvoorbeeld een scan zonder tekst).</summary>
public interface IPdfTekstLezer
{
    string? LeesTekst(byte[] pdf);
}

public sealed record BonGegevens(DateOnly? Datum, Bedrag? Bedrag);

/// <summary>
/// Datum en totaalbedrag uit de tekst van een bon of factuur (besluit 96). De datum is de eerste geldige datum
/// (dag-maand-jaar, jjjj-mm-dd of "1 oktober 2026"); het bedrag komt van de regel "te betalen", anders de laatste
/// regel met "totaal" (niet subtotaal, excl. of btw), anders het bedrag dat het grootst is zonder minteken. Een minteken
/// vóór of achter het bedrag blijft behouden, zoals bij een creditnota (besluit 142).
/// </summary>
public static partial class BonHerkenning
{
    private static readonly string[] Maanden =
        ["jan", "feb", "mrt", "apr", "mei", "jun", "jul", "aug", "sep", "okt", "nov", "dec"];

    public static BonGegevens Herken(string? tekst)
    {
        if (string.IsNullOrWhiteSpace(tekst))
        {
            return new BonGegevens(null, null);
        }

        var datum = EersteDatum(tekst);
        var zonderDatums = Datumpatroon().Replace(IsoDatum().Replace(TekstDatum().Replace(tekst, " "), " "), " ");
        return new BonGegevens(datum, Totaal(zonderDatums));
    }

    private static DateOnly? EersteDatum(string tekst)
    {
        var kandidaten = new List<(int Positie, DateOnly Datum)>();
        foreach (Match m in Datumpatroon().Matches(tekst))
        {
            var jaar = int.Parse(m.Groups["j"].Value, CultureInfo.InvariantCulture);
            Voeg(m.Index, jaar < 100 ? 2000 + jaar : jaar, Getal(m, "m"), Getal(m, "d"));
        }

        foreach (Match m in IsoDatum().Matches(tekst))
        {
            Voeg(m.Index, Getal(m, "j"), Getal(m, "m"), Getal(m, "d"));
        }

        foreach (Match m in TekstDatum().Matches(tekst))
        {
            var maand = Array.FindIndex(Maanden, n => m.Groups["m"].Value.StartsWith(n, StringComparison.OrdinalIgnoreCase)) + 1;
            Voeg(m.Index, Getal(m, "j"), maand == 0 && m.Groups["m"].Value.StartsWith("maa", StringComparison.OrdinalIgnoreCase) ? 3 : maand, Getal(m, "d"));
        }

        return kandidaten.Count == 0 ? null : kandidaten.MinBy(k => k.Positie).Datum;

        void Voeg(int positie, int jaar, int maand, int dag)
        {
            if (jaar is >= 2000 and <= 2099 && maand is >= 1 and <= 12 && dag >= 1 && dag <= DateTime.DaysInMonth(jaar, maand))
            {
                kandidaten.Add((positie, new DateOnly(jaar, maand, dag)));
            }
        }
    }

    private static Bedrag? Totaal(string tekst)
    {
        var regels = tekst.Split('\n');
        var teBetalen = regels.FirstOrDefault(r => r.Contains("te betalen", StringComparison.OrdinalIgnoreCase) && Bedragen(r).Count > 0);
        var totaal = regels.LastOrDefault(r =>
            Totaalwoord().IsMatch(r) && !Uitgesloten().IsMatch(r) && Bedragen(r).Count > 0);
        if ((teBetalen ?? totaal) is { } regel)
        {
            return Bedragen(regel)[^1];
        }

        var alle = Bedragen(tekst);
        return alle.Count == 0 ? null : alle.MaxBy(b => Math.Abs(b.Centen));
    }

    /// <summary>Invoer in het postvak: zoals <see cref="Bedrag.TryParse"/>, maar een minteken vóór of achter mag (besluit 142).</summary>
    public static bool TryParseBedrag(string? invoer, out Bedrag bedrag)
    {
        var tekst = (invoer ?? string.Empty).Trim();
        var negatief = tekst.StartsWith('-') || tekst.EndsWith('-') || tekst.StartsWith("€ -", StringComparison.Ordinal) || tekst.StartsWith("€-", StringComparison.Ordinal);
        tekst = tekst.Replace("-", string.Empty, StringComparison.Ordinal).Trim();
        if (!Bedrag.TryParse(tekst, out bedrag))
        {
            bedrag = Bedrag.Nul;
            return false;
        }

        bedrag = negatief ? new Bedrag(-bedrag.Centen) : bedrag;
        return true;
    }

    private static List<Bedrag> Bedragen(string tekst)
    {
        var komma = Kommabedrag().Matches(tekst).Select(m => Maak(m, m.Groups["e"].Value.Replace(".", string.Empty, StringComparison.Ordinal))).ToList();
        return komma.Count > 0
            ? komma
            : Puntbedrag().Matches(tekst).Select(m => Maak(m, m.Groups["e"].Value)).ToList();

        static Bedrag Maak(Match m, string euro)
        {
            var centen = (long.Parse(euro, CultureInfo.InvariantCulture) * 100) + int.Parse(m.Groups["c"].Value, CultureInfo.InvariantCulture);
            return new(m.Groups["min"].Success || m.Groups["na"].Success ? -centen : centen);
        }
    }

    private static int Getal(Match m, string groep) => int.Parse(m.Groups[groep].Value, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"(?<!\d)(?<d>\d{1,2})[-/.](?<m>\d{1,2})[-/.](?<j>\d{4}|\d{2})(?!\d)")]
    private static partial Regex Datumpatroon();

    [GeneratedRegex(@"(?<!\d)(?<j>\d{4})-(?<m>\d{2})-(?<d>\d{2})(?!\d)")]
    private static partial Regex IsoDatum();

    [GeneratedRegex(@"(?<!\d)(?<d>\d{1,2})\s+(?<m>jan(uari)?|feb(ruari)?|maart|mrt|apr(il)?|mei|jun(i)?|jul(i)?|aug(ustus)?|sep(t(ember)?)?|okt(ober)?|nov(ember)?|dec(ember)?)\.?\s+(?<j>\d{4})(?!\d)", RegexOptions.IgnoreCase)]
    private static partial Regex TekstDatum();

    /// <summary>Optioneel minteken vóór (<c>-15,00</c>, <c>- 15,00</c>, <c>€ -15,00</c>, <c>-€ 15,00</c>) of direct achter (<c>15,00-</c>).</summary>
    private const string Voor = @"(?:(?<![\w.,-])(?<min>-)\s?(?:€\s?)?|(?<![\w.,-])€\s?(?<min>-)\s?)?";

    [GeneratedRegex(Voor + @"(?<![\d.,])(?<e>\d{1,3}(?:\.\d{3})+|\d+),(?<c>\d{2})(?![\d,])(?<na>-(?!\d))?")]
    private static partial Regex Kommabedrag();

    [GeneratedRegex(Voor + @"(?<![\d.,])(?<e>\d+)\.(?<c>\d{2})(?![\d.])(?<na>-(?!\d))?")]
    private static partial Regex Puntbedrag();

    [GeneratedRegex(@"\b(totaal|total|bedrag)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Totaalwoord();

    [GeneratedRegex(@"sub|excl|btw|vat", RegexOptions.IgnoreCase)]
    private static partial Regex Uitgesloten();
}
