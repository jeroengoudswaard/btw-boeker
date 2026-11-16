using BtwBoeker.Application.Inrichting;

namespace BtwBoeker.App.ViewModels;

/// <summary>
/// De aantallen bij de onderdelen in de navigatiebalk, uit dezelfde berekening als de startkaarten. Besluit 33: één
/// teller per regel, alleen bij het onderdeel; er is geen laag Boekhouding meer die optelt.
/// </summary>
public static class Navigatietellers
{
    private static readonly Dictionary<Startkaartsoort, string> Onderdelen = new()
    {
        [Startkaartsoort.Bank] = "Boekhouding.Bank",
        [Startkaartsoort.Concepten] = "Boekhouding.Journaal",
        [Startkaartsoort.Postvak] = "Boekhouding.Postvak",
    };

    public static IReadOnlyDictionary<string, int> Bepaal(IEnumerable<Startkaart> kaarten)
    {
        ArgumentNullException.ThrowIfNull(kaarten);
        var tellers = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var kaart in kaarten.Where(k => k.Aantal > 0))
        {
            if (!Onderdelen.TryGetValue(kaart.Soort, out var sleutel))
            {
                continue;
            }

            tellers[sleutel] = tellers.GetValueOrDefault(sleutel) + kaart.Aantal;
        }

        return tellers;
    }
}
