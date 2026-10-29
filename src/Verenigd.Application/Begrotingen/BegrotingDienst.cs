using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Begrotingen;

namespace Verenigd.Application.Begrotingen;

public sealed record BegrotingInvoer(int RekeningId, Bedrag Bedrag, string? Toelichting);

/// <summary>Begroting opstellen, overnemen, vaststellen en herzien (besluiten 146–150 en 153).</summary>
public sealed class BegrotingDienst(IDatabaseSessie sessie, TimeProvider tijd)
{
    /// <summary>Een nieuwe begroting (versie 1, concept).</summary>
    /// <exception cref="InvalidOperationException">Voor dat jaar bestaat al een begroting.</exception>
    public Task<int> MaakAsync(AdministratieSoort administratie, int jaar, CancellationToken annulering = default) =>
        sessie.Boekhouding.PasToeAsync(administratie, stand =>
        {
            if (stand.Begrotingen.Any(b => b.Jaar == jaar))
            {
                throw new InvalidOperationException($"Er is al een begroting voor {jaar}.");
            }

            var begroting = new Begroting(administratie, jaar, tijd.GetUtcNow().UtcDateTime);
            var wijziging = new BoekhoudingWijziging<int>(0) { WaardeNaOpslaan = () => begroting.Id };
            wijziging.Toevoegen.Add(begroting);
            return wijziging;
        }, annulering);

    /// <summary>Bedragen met de hand bijstellen (besluit 150); een bedrag van nul verwijdert de regel.</summary>
    public Task BewaarAsync(AdministratieSoort administratie, int begrotingId, IReadOnlyList<BegrotingInvoer> bedragen, CancellationToken annulering = default) =>
        Wijzig(administratie, begrotingId, (stand, begroting) =>
        {
            ArgumentNullException.ThrowIfNull(bedragen);
            foreach (var invoer in bedragen)
            {
                begroting.ZetBedrag(stand.Rekeningen.Single(r => r.Id == invoer.RekeningId), invoer.Bedrag, invoer.Toelichting);
            }
        }, annulering);

    /// <summary>Alle bedragen vervangen door die van de bron plus het percentage, afgerond op tientallen (besluit 153).</summary>
    public Task NeemOverAsync(
        AdministratieSoort administratie, int begrotingId, BegrotingBron bron, int bronjaar, decimal percentage, CancellationToken annulering = default) =>
        Wijzig(administratie, begrotingId, (stand, begroting) =>
        {
            foreach (var (rekening, bedrag) in Begrotingsrekenaar.Overnemen(stand, bron, bronjaar, percentage))
            {
                begroting.ZetBedrag(rekening, bedrag, begroting.Regels.FirstOrDefault(r => r.Rekening == rekening)?.Toelichting);
            }
        }, annulering);

    public Task StelVastAsync(AdministratieSoort administratie, int begrotingId, DateOnly datum, string? omschrijving, CancellationToken annulering = default) =>
        Wijzig(administratie, begrotingId, (_, begroting) => begroting.StelVast(datum, omschrijving), annulering);

    /// <summary>Een nieuwe conceptversie van de nieuwste vastgestelde versie.</summary>
    /// <exception cref="InvalidOperationException">Niet de nieuwste versie, of nog niet vastgesteld.</exception>
    public Task<int> HerzieAsync(AdministratieSoort administratie, int begrotingId, CancellationToken annulering = default) =>
        sessie.Boekhouding.PasToeAsync(administratie, stand =>
        {
            var begroting = Zoek(stand, begrotingId);
            if (stand.Begrotingen.Any(b => b.Jaar == begroting.Jaar && b.Versie > begroting.Versie))
            {
                throw new InvalidOperationException("Er is al een nieuwere versie van deze begroting.");
            }

            var herziening = begroting.Herzie(tijd.GetUtcNow().UtcDateTime);
            var wijziging = new BoekhoudingWijziging<int>(0) { WaardeNaOpslaan = () => herziening.Id };
            wijziging.Toevoegen.Add(herziening);
            return wijziging;
        }, annulering);

    private Task<bool> Wijzig(AdministratieSoort administratie, int begrotingId, Action<BoekhoudingStand, Begroting> actie, CancellationToken annulering) =>
        sessie.Boekhouding.PasToeAsync(administratie, stand =>
        {
            actie(stand, Zoek(stand, begrotingId));
            return new BoekhoudingWijziging<bool>(true);
        }, annulering);

    private static Begroting Zoek(BoekhoudingStand stand, int begrotingId) =>
        stand.Begrotingen.FirstOrDefault(b => b.Id == begrotingId) ?? throw new InvalidOperationException("De begroting bestaat niet (meer).");
}
