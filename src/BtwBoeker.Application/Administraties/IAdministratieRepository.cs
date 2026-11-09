using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.Application.Administraties;

public interface IAdministratieRepository
{
    Task<IReadOnlyList<Administratie>> HaalAlleAsync(CancellationToken annulering = default);

    Task BewaarAsync(Administratie administratie, CancellationToken annulering = default);
}
