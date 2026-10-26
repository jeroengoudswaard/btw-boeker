using Microsoft.EntityFrameworkCore;
using Verenigd.Application.Administraties;
using Verenigd.Domain.Administraties;

namespace Verenigd.Infrastructure.Database;

internal sealed class AdministratieRepository(Func<VerenigdDbContext> maakContext) : IAdministratieRepository
{
    public async Task<IReadOnlyList<Administratie>> HaalAlleAsync(CancellationToken annulering = default)
    {
        await using var context = maakContext();
        return await context.Administraties.AsNoTracking().OrderBy(a => a.Soort).ToListAsync(annulering).ConfigureAwait(false);
    }

    public async Task BewaarAsync(Administratie administratie, CancellationToken annulering = default)
    {
        await using var context = maakContext();
        context.Administraties.Update(administratie);
        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
    }
}
