using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BtwBoeker.App.Tests.UI;

/// <summary>
/// Leest de tijden die <c>Meetpunt</c> in het logbestand schrijft ("Duur Navigeren naar Boekhouding: 120 ms") en maakt er
/// een Markdown-tabel van: per handeling de eerste keer, het gemiddelde van de keren daarna en het hoogste.
/// </summary>
internal static partial class Snelheidsrapport
{
    [GeneratedRegex(@"\] Duur ""?(?<handeling>.+?)""?: (?<ms>\d+) ms\s*$")]
    private static partial Regex Duurregel();

    public static Dictionary<string, List<long>> Lees(IEnumerable<string> regels)
    {
        var metingen = new Dictionary<string, List<long>>(StringComparer.Ordinal);
        foreach (var regel in regels)
        {
            var treffer = Duurregel().Match(regel);
            if (!treffer.Success)
            {
                continue;
            }

            var handeling = treffer.Groups["handeling"].Value;
            if (!metingen.TryGetValue(handeling, out var lijst))
            {
                metingen[handeling] = lijst = [];
            }

            lijst.Add(long.Parse(treffer.Groups["ms"].Value, CultureInfo.InvariantCulture));
        }

        return metingen;
    }

    public static string Tabel(IReadOnlyDictionary<string, List<long>> metingen)
    {
        var tekst = new StringBuilder();
        tekst.AppendLine("| Handeling | Eerste keer (ms) | Daarna gemiddeld (ms) | Hoogste (ms) | Aantal |");
        tekst.AppendLine("|---|---:|---:|---:|---:|");
        foreach (var (handeling, tijden) in metingen.OrderByDescending(m => m.Value[0]).ThenBy(m => m.Key, StringComparer.Ordinal))
        {
            var daarna = tijden.Count > 1 ? Math.Round(tijden.Skip(1).Average()).ToString(CultureInfo.InvariantCulture) : "–";
            tekst.AppendLine(CultureInfo.InvariantCulture, $"| {handeling} | {tijden[0]} | {daarna} | {tijden.Max()} | {tijden.Count} |");
        }

        return tekst.ToString();
    }

    /// <summary>Het hele rapport: omvang van de demodatabase, opbouwtijd en de tabel uit de logbestanden.</summary>
    public static string Maak(IEnumerable<string> logbestanden, int posten, int transacties, TimeSpan opbouw)
    {
        var regels = logbestanden.SelectMany(b => File.ReadAllLines(b)).ToList();
        var tekst = new StringBuilder();
        tekst.AppendLine(CultureInfo.InvariantCulture, $"# Snelheidsmeting BTW-Boeker ({DateTime.Now:yyyy-MM-dd HH:mm})");
        tekst.AppendLine();
        tekst.AppendLine(CultureInfo.InvariantCulture, $"- Demodatabase: vijf jaar verhuur, {posten} geboekte posten, {transacties} banktransacties.");
        tekst.AppendLine(CultureInfo.InvariantCulture, $"- Opbouw van de demodatabase (buiten de app): {opbouw.TotalSeconds:0.0} s.");
        tekst.AppendLine(CultureInfo.InvariantCulture, $"- Processorkernen: {Environment.ProcessorCount}.");
        tekst.AppendLine("- Twee rondes langs alle pagina's; \"daarna\" is het gemiddelde zonder de eerste keer.");
        tekst.AppendLine();
        tekst.Append(Tabel(Lees(regels)));
        return tekst.ToString();
    }
}
