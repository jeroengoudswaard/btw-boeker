using Microsoft.EntityFrameworkCore;
using BtwBoeker.Application.Administraties;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.Infrastructure.Database;

internal sealed class AdministratieRepository(Func<BtwBoekerDbContext> maakContext) : IAdministratieRepository
{
    public async Task<IReadOnlyList<Administratie>> HaalAlleAsync(CancellationToken annulering = default)
    {
        await using var context = maakContext();
        // Besluit 26: de app toont één administratie; een andere rij (met gegevens uit een oudere versie) blijft bewaard maar verborgen.
        return await context.Administraties.AsNoTracking().Where(a => a.Soort == AdministratieSoort.Vereniging)
            .ToListAsync(annulering).ConfigureAwait(false);
    }

    public async Task BewaarAsync(Administratie administratie, CancellationToken annulering = default)
    {
        await using var context = maakContext();
        context.Administraties.Update(administratie);
        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
    }
}
