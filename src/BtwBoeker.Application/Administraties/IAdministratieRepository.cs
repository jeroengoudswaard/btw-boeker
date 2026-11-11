using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.Application.Administraties;

public interface IAdministratieRepository
{
    /// <summary>De administratie van de app (besluit 26: er is er een).</summary>
    Task<Administratie> HaalAsync(CancellationToken annulering = default);

    Task BewaarAsync(Administratie administratie, CancellationToken annulering = default);
}
