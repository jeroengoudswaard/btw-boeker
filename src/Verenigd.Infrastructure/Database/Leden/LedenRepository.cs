using Microsoft.EntityFrameworkCore;
using Verenigd.Application.Leden;
using Verenigd.Domain.Leden;

namespace Verenigd.Infrastructure.Database.Leden;

internal sealed class LedenRepository(Func<VerenigdDbContext> maakContext) : ILedenRepository
{
    public async Task<IReadOnlyList<Persoon>> HaalAlleAsync(CancellationToken annulering = default)
    {
        await using var context = maakContext();
        return await MetDetails(context.Personen.AsNoTrackingWithIdentityResolution())
            .OrderBy(p => p.Achternaam).ThenBy(p => p.Voornaam).ThenBy(p => p.Lidnummer)
            .ToListAsync(annulering).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Groep>> HaalGroepenAsync(CancellationToken annulering = default)
    {
        await using var context = maakContext();
        return await context.Groepen.AsNoTracking().OrderBy(g => g.Naam).ToListAsync(annulering).ConfigureAwait(false);
    }

    public async Task BewaarGroepsoortenAsync(IReadOnlyDictionary<string, GroepSoort> soortPerGroep, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(soortPerGroep);
        await using var context = maakContext();
        var namen = soortPerGroep.Keys.ToList();
        foreach (var groep in await context.Groepen.Where(g => namen.Contains(g.Naam)).ToListAsync(annulering).ConfigureAwait(false))
        {
            groep.WijzigSoort(soortPerGroep[groep.Naam]);
        }

        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
    }

    public async Task BewaarNotitiesAsync(Guid persoonId, string? notities, DateTime nuUtc, CancellationToken annulering = default)
    {
        await using var context = maakContext();
        var persoon = await context.Personen.Include(p => p.Herkomst).SingleOrDefaultAsync(p => p.Id == persoonId, annulering).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("De persoon bestaat niet meer.");
        persoon.WijzigNotities(notities, nuUtc);
        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
    }

    public async Task NeemVoorstelOverAsync(Guid persoonId, string veld, string? waarde, DateTime nuUtc, CancellationToken annulering = default)
    {
        await using var context = maakContext();
        var persoon = await context.Personen.Include(p => p.Voogden).Include(p => p.Herkomst).AsSplitQuery()
            .SingleOrDefaultAsync(p => p.Id == persoonId, annulering).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Het lid bestaat niet meer in Verenigd.");
        persoon.NeemVoorstelOver(veld, waarde, nuUtc);
        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
    }

    internal static IQueryable<Persoon> MetDetails(IQueryable<Persoon> personen) =>
        personen
            .Include(p => p.Functies).ThenInclude(f => f.Groep)
            .Include(p => p.Voogden)
            .Include(p => p.Herkomst)
            .AsSplitQuery();
}
