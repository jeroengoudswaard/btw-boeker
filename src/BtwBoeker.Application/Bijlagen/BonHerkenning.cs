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

/// <summary>Besluit 43: een bedrag dat op de bon staat, met de regel waar het vandaan komt.</summary>
/// <param name="IsBijkomend">Rente, een boete of kosten bij te late betaling: niet het bedrag van de rekening zelf.</param>
public sealed record BonKandidaat(Bedrag Bedrag, string Regel, bool IsBijkomend);

/// <summary>
/// Datum en totaalbedrag uit de tekst van een bon of factuur. De datum is de eerste geldige datum
/// (dag-maand-jaar, jjjj-mm-dd of "1 oktober 2026"). Het bedrag komt van de eerste regel met "te betalen", "te voldoen" of
/// "openstaand", anders van de laatste regel met "totaal" (niet subtotaal, excl. of alleen btw; "totaal incl. btw" telt
/// wel), anders is het het grootste bedrag. Regels over rente, boetes, incasso of kosten bij te late betaling tellen
/// nooit mee. Een minteken vóór of achter het bedrag blijft behouden, zoals bij een creditnota.
/// </summary>
public static partial class BonHerkenning
{
    private static readonly string[] Maanden =
        ["jan", "feb", "mrt", "apr", "mei", "jun", "jul", "aug", "sep", "okt", "nov", "dec"];

    public static BonGegevens Herken(string? tekst) => HerkenMetZekerheid(tekst).Gegevens;

    /// <summary>
    /// Besluit 43: zoals <see cref="Herken"/>, plus of het bedrag van een totaalregel komt (<c>true</c>) of het grootste
    /// bedrag is bij gebrek aan zo'n regel (<c>false</c>). Bij twijfel kan de aanroeper een bedrag kiezen dat bij een
    /// boeking past.
    /// </summary>
    internal static (BonGegevens Gegevens, bool IsZeker) HerkenMetZekerheid(string? tekst)
    {
        if (string.IsNullOrWhiteSpace(tekst))
        {
            return (new BonGegevens(null, null), false);
        }

        var (bedrag, zeker) = Totaal(ZonderDatums(tekst));
        return (new BonGegevens(EersteDatum(tekst), bedrag), zeker);
    }

    /// <summary>
    /// Besluit 43: de bedragen op de bon om uit te kiezen, hooguit zes. Eerst het herkende bedrag, dan de andere in de
    /// volgorde van de bon, en als laatste rente, boetes en kosten (gemarkeerd).
    /// </summary>
    public static IReadOnlyList<BonKandidaat> Kandidaten(string? tekst)
    {
        if (string.IsNullOrWhiteSpace(tekst))
        {
            return [];
        }

        var regels = ZonderDatums(tekst).Split('\n');
        var alle = regels
            .SelectMany(r => Bedragen(r).Where(b => b.Centen != 0).Select(b => new BonKandidaat(b, r.Trim(), IsBijkomend(r))))
            .ToList();
        var gekozen = Totaal(string.Join('\n', regels)).Bedrag;
        return [.. alle.Where(k => k.Bedrag == gekozen && !k.IsBijkomend).Take(1)
            .Concat(alle.Where(k => !k.IsBijkomend))
            .Concat(alle.Where(k => k.IsBijkomend))
            .DistinctBy(k => k.Bedrag)
            .Take(6)];
    }

    private static string ZonderDatums(string tekst) =>
        Datumpatroon().Replace(IsoDatum().Replace(TekstDatum().Replace(tekst, " "), " "), " ");

    private static bool IsBijkomend(string regel) => Bijkomend().IsMatch(regel);

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

    private static (Bedrag? Bedrag, bool IsZeker) Totaal(string tekst)
    {
        var regels = tekst.Split('\n').Where(r => !IsBijkomend(r) && Bedragen(r).Count > 0).ToList();
        var teBetalen = regels.FirstOrDefault(r => TeBetalen().IsMatch(r));
        var totaal = regels.LastOrDefault(r => Totaalwoord().IsMatch(r) && !IsUitgesloten(r));
        if ((teBetalen ?? totaal) is { } regel)
        {
            return (Bedragen(regel)[^1], true);
        }

        var alle = regels.SelectMany(Bedragen).ToList();
        return (alle.Count == 0 ? null : alle.MaxBy(b => Math.Abs(b.Centen)), false);
    }

    /// <summary>Subtotaal, excl. en btw-regels zijn niet het totaal; "totaal incl. btw" wel.</summary>
    private static bool IsUitgesloten(string regel) =>
        Uitgesloten().IsMatch(regel) || (Btw().IsMatch(regel) && !regel.Contains("incl", StringComparison.OrdinalIgnoreCase));

    /// <summary>Invoer in het postvak: zoals <see cref="Bedrag.TryParse"/>, maar een minteken vóór of achter mag.</summary>
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

    [GeneratedRegex(@"sub|excl", RegexOptions.IgnoreCase)]
    private static partial Regex Uitgesloten();

    [GeneratedRegex(@"\b(btw|vat)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Btw();

    [GeneratedRegex(@"te betalen|te voldoen|openstaand", RegexOptions.IgnoreCase)]
    private static partial Regex TeBetalen();

    /// <summary>Besluit 43: rente, boetes en kosten bij te late betaling zijn niet het bedrag van de rekening.</summary>
    [GeneratedRegex(@"boete|rente\b|incassokosten|incassobureau|administratiekosten|aanmaning|herinneringskosten|niet tijdig|te late|na (de )?vervaldatum|art\.?\s?119|wettelijke", RegexOptions.IgnoreCase)]
    private static partial Regex Bijkomend();
}
