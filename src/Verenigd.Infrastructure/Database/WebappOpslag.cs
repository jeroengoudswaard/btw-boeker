using Microsoft.EntityFrameworkCore;
using Verenigd.Application.Webapp;
using Verenigd.Domain.Webapp;

namespace Verenigd.Infrastructure.Database;

internal sealed class WebappOpslag(Func<VerenigdDbContext> maakContext) : IWebappOpslag
{
    public async Task<Webappinstellingen> LaadAsync(CancellationToken annulering = default)
    {
        await using var context = maakContext();
        return await context.Webappinstellingen.AsNoTracking().SingleOrDefaultAsync(annulering).ConfigureAwait(false) ?? new Webappinstellingen();
    }

    public async Task BewaarAsync(Webappinstellingen instellingen, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(instellingen);
        await using var context = maakContext();
        if (await context.Webappinstellingen.AnyAsync(i => i.Id == instellingen.Id, annulering).ConfigureAwait(false))
        {
            context.Webappinstellingen.Update(instellingen);
        }
        else
        {
            context.Webappinstellingen.Add(instellingen);
        }

        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
    }
}
