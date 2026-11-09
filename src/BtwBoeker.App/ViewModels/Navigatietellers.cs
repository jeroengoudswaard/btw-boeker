using BtwBoeker.Application.Inrichting;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.App.ViewModels;

/// <summary>
/// Besluit 220: de aantallen bij de onderdelen in de navigatiebalk, uit dezelfde berekening als de startkaarten. Een
/// module telt de onderdelen op, zodat het aantal ook zichtbaar is als de module is ingeklapt.
/// </summary>
public static class Navigatietellers
{
    private static readonly Dictionary<Startkaartsoort, string> Onderdelen = new()
    {
        [Startkaartsoort.Bank] = "Boekhouding.Bank",
        [Startkaartsoort.Concepten] = "Boekhouding.Journaal",
        [Startkaartsoort.Postvak] = "Boekhouding.Postvak",
    };

    public static IReadOnlyDictionary<string, int> Bepaal(IEnumerable<Startkaart> kaarten, AdministratieSoort? administratie)
    {
        ArgumentNullException.ThrowIfNull(kaarten);
        var tellers = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var kaart in kaarten.Where(k => k.Administratie == administratie && k.Aantal > 0))
        {
            if (!Onderdelen.TryGetValue(kaart.Soort, out var sleutel))
            {
                continue;
            }

            tellers[sleutel] = tellers.GetValueOrDefault(sleutel) + kaart.Aantal;
            var module = sleutel[..sleutel.IndexOf('.', StringComparison.Ordinal)];
            tellers[module] = tellers.GetValueOrDefault(module) + kaart.Aantal;
        }

        return tellers;
    }
}
