using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Database;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Boekhouding;
using BtwBoeker.Domain.Financien.Relaties;

namespace BtwBoeker.Application.Relaties;

public sealed record RelatieInvoer(Guid? Id, RelatieGegevens Gegevens, bool IsActief);

public sealed class RelatieDienst(IDatabaseSessie sessie)
{
    private IBoekhoudingRepository Repository => sessie.Boekhouding;

    public Task<Guid> BewaarAsync(RelatieInvoer invoer, CancellationToken annulering = default) =>
        Repository.PasToeAsync(AdministratieSoort.Vereniging, stand =>
        {
            ArgumentNullException.ThrowIfNull(invoer);
            var toevoegen = new List<object>();
            Relatie relatie;
            if (invoer.Id is { } id)
            {
                relatie = stand.Relaties.SingleOrDefault(r => r.Id == id) ?? throw new InvalidOperationException("De relatie bestaat niet meer.");
                relatie.Wijzig(invoer.Gegevens);
            }
            else
            {
                relatie = new Relatie(invoer.Gegevens);
                toevoegen.Add(relatie);
            }

            if (invoer.IsActief)
            {
                relatie.Activeer();
            }
            else
            {
                relatie.Deactiveer();
            }

            var wijziging = new BoekhoudingWijziging<Guid>(relatie.Id);
            wijziging.Toevoegen.AddRange(toevoegen);
            return wijziging;
        }, annulering);
}
