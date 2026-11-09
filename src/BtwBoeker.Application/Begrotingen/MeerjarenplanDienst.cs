using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Database;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Begrotingen;

namespace BtwBoeker.Application.Begrotingen;

/// <param name="Bedragen">Eén bedrag per jaar van het plan, vanaf het startjaar.</param>
public sealed record MeerjarenInvoer(int RekeningId, IReadOnlyList<Bedrag> Bedragen, string? Toelichting);

/// <summary>Meerjarenplan opstellen, vaststellen en herzien (besluiten 159–167).</summary>
public sealed class MeerjarenplanDienst(IDatabaseSessie sessie, TimeProvider tijd)
{
    /// <exception cref="InvalidOperationException">Er is al een meerjarenplan met dat startjaar.</exception>
    public Task<int> MaakAsync(AdministratieSoort administratie, int startjaar, CancellationToken annulering = default) =>
        sessie.Boekhouding.PasToeAsync(administratie, stand =>
        {
            if (stand.Meerjarenplannen.Any(p => p.Startjaar == startjaar))
            {
                throw new InvalidOperationException($"Er is al een meerjarenplan vanaf {startjaar}.");
            }

            var plan = new Meerjarenplan(administratie, startjaar, tijd.GetUtcNow().UtcDateTime);
            var wijziging = new BoekhoudingWijziging<int>(0) { WaardeNaOpslaan = () => plan.Id };
            wijziging.Toevoegen.Add(plan);
            return wijziging;
        }, annulering);

    /// <summary>Per rekening alle bedragen en de toelichting vervangen.</summary>
    /// <exception cref="ArgumentException">Niet precies één bedrag per jaar.</exception>
    public Task BewaarAsync(AdministratieSoort administratie, int planId, IReadOnlyList<MeerjarenInvoer> regels, CancellationToken annulering = default) =>
        Wijzig(administratie, planId, (stand, plan) =>
        {
            ArgumentNullException.ThrowIfNull(regels);
            foreach (var invoer in regels)
            {
                if (invoer.Bedragen.Count != plan.AantalJaren)
                {
                    throw new ArgumentException($"Geef precies {plan.AantalJaren} bedragen, één per jaar.", nameof(regels));
                }

                var rekening = stand.Rekeningen.Single(r => r.Id == invoer.RekeningId);
                foreach (var (jaar, bedrag) in plan.Jaren.Zip(invoer.Bedragen))
                {
                    plan.ZetBedrag(rekening, jaar, bedrag);
                }

                plan.ZetToelichting(rekening, invoer.Toelichting);
            }
        }, annulering);

    public Task StelVastAsync(AdministratieSoort administratie, int planId, DateOnly datum, string? omschrijving, CancellationToken annulering = default) =>
        Wijzig(administratie, planId, (_, plan) => plan.StelVast(datum, omschrijving), annulering);

    /// <exception cref="InvalidOperationException">Niet de nieuwste versie, of nog niet vastgesteld.</exception>
    public Task<int> HerzieAsync(AdministratieSoort administratie, int planId, CancellationToken annulering = default) =>
        sessie.Boekhouding.PasToeAsync(administratie, stand =>
        {
            var plan = Zoek(stand, planId);
            if (stand.Meerjarenplannen.Any(p => p.Startjaar == plan.Startjaar && p.Versie > plan.Versie))
            {
                throw new InvalidOperationException("Er is al een nieuwere versie van dit meerjarenplan.");
            }

            var herziening = plan.Herzie(tijd.GetUtcNow().UtcDateTime);
            var wijziging = new BoekhoudingWijziging<int>(0) { WaardeNaOpslaan = () => herziening.Id };
            wijziging.Toevoegen.Add(herziening);
            return wijziging;
        }, annulering);

    private Task<bool> Wijzig(AdministratieSoort administratie, int planId, Action<BoekhoudingStand, Meerjarenplan> actie, CancellationToken annulering) =>
        sessie.Boekhouding.PasToeAsync(administratie, stand =>
        {
            actie(stand, Zoek(stand, planId));
            return new BoekhoudingWijziging<bool>(true);
        }, annulering);

    private static Meerjarenplan Zoek(BoekhoudingStand stand, int planId) =>
        stand.Meerjarenplannen.FirstOrDefault(p => p.Id == planId) ?? throw new InvalidOperationException("Het meerjarenplan bestaat niet (meer).");
}
