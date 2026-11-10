using BtwBoeker.Application.Database;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Boekhouding;

/// <summary>Eén regel van de indeling zoals de editor of een sjabloon hem opgeeft; rubrieken op Id.</summary>
public sealed record IndelingsregelGegevens(JaarrekeningAfdeling Afdeling, IndelingsregelSoort Soort, string Tekst, IReadOnlyList<int> RubriekIds);

/// <summary>Eén regel uit een sjabloonbestand; rubrieken op naam.</summary>
public sealed record SjabloonIndelingsregel(JaarrekeningRapport Rapport, JaarrekeningAfdeling Afdeling, IndelingsregelSoort Soort, string Tekst, IReadOnlyList<string> Rubrieken);

/// <summary>Een eigen jaarrekeningindeling bewaren of terugzetten naar de automatische.</summary>
public sealed class JaarrekeningDienst(IDatabaseSessie sessie)
{
    /// <summary>Vervangt de indeling van één rapport; een lege lijst zet hem terug naar de automatische indeling.</summary>
    /// <exception cref="ArgumentException">Een regel klopt niet; de melding zegt welke.</exception>
    public Task BewaarAsync(
        AdministratieSoort administratie, JaarrekeningRapport rapport, IReadOnlyList<IndelingsregelGegevens> regels, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(regels);
        return sessie.Boekhouding.PasToeAsync(administratie, stand =>
        {
            var invoer = regels.Select(r => new IndelingsregelInvoer(r.Afdeling, r.Soort, r.Tekst,
                [.. r.RubriekIds.Select(id => stand.Rubrieken.FirstOrDefault(x => x.Id == id)
                    ?? throw new ArgumentException("Een gekozen rubriek bestaat niet (meer).", nameof(regels)))])).ToList();
            return Vervang(stand, administratie, rapport, invoer);
        }, annulering);
    }

    /// <summary>
    /// Neemt de indeling uit een sjabloon over na het inrichten. Onbekende rubrieken worden overgeslagen, een regel zonder
    /// rubrieken vervalt; per rapport alleen als er nog geen eigen indeling is.
    /// </summary>
    public Task PasSjabloonToeAsync(AdministratieSoort administratie, IReadOnlyList<SjabloonIndelingsregel> regels, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(regels);
        return sessie.Boekhouding.PasToeAsync(administratie, stand =>
        {
            var wijziging = new BoekhoudingWijziging<bool>(true);
            foreach (var rapport in regels.Select(r => r.Rapport).Distinct())
            {
                if (stand.Jaarrekeningindelingen.Any(i => i.Rapport == rapport && i.Regels.Count > 0))
                {
                    continue;
                }

                var invoer = regels.Where(r => r.Rapport == rapport)
                    .Select(r => new IndelingsregelInvoer(r.Afdeling, r.Soort, r.Tekst,
                        [.. r.Rubrieken.Select(n => stand.Rubrieken.FirstOrDefault(x => string.Equals(x.Naam, n, StringComparison.Ordinal))).OfType<Rubriek>().Distinct()]))
                    .Where(r => r.Soort != IndelingsregelSoort.Regel || r.Rubrieken.Count > 0)
                    .ToList();
                wijziging.Toevoegen.AddRange(Vervang(stand, administratie, rapport, invoer).Toevoegen);
            }

            return wijziging;
        }, annulering);
    }

    /// <summary>De opgeslagen indelingen als sjabloonregels, voor het exporteren van een sjabloonbestand.</summary>
    public static IReadOnlyList<SjabloonIndelingsregel> NaarSjabloon(BoekhoudingStand stand)
    {
        ArgumentNullException.ThrowIfNull(stand);
        return [.. stand.Jaarrekeningindelingen.OrderBy(i => i.Rapport).SelectMany(i => i.Regels
            .Select(r => new SjabloonIndelingsregel(i.Rapport, r.Afdeling, r.Soort, r.Tekst, [.. r.Rubrieken.Select(x => x.Naam)])))];
    }

    private static BoekhoudingWijziging<bool> Vervang(
        BoekhoudingStand stand, AdministratieSoort administratie, JaarrekeningRapport rapport, IReadOnlyList<IndelingsregelInvoer> regels)
    {
        var wijziging = new BoekhoudingWijziging<bool>(true);
        var indeling = stand.Jaarrekeningindelingen.FirstOrDefault(i => i.Rapport == rapport);
        if (indeling is null)
        {
            indeling = new Jaarrekeningindeling(administratie, rapport);
            wijziging.Toevoegen.Add(indeling);
        }

        indeling.Vervang(regels);
        return wijziging;
    }
}
