using Microsoft.EntityFrameworkCore;
using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Administraties;

namespace Verenigd.Infrastructure.Database;

internal sealed class BoekhoudingRepository(Func<VerenigdDbContext> maakContext) : IBoekhoudingRepository
{
    public async Task<BoekhoudingStand> LaadAsync(AdministratieSoort administratie, CancellationToken annulering = default)
    {
        await using var context = maakContext();
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
        return await LaadAsync(context, administratie, annulering).ConfigureAwait(false);
    }

    public async Task<T> PasToeAsync<T>(
        AdministratieSoort administratie, Func<BoekhoudingStand, BoekhoudingWijziging<T>> wijziging, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(wijziging);
        await using var context = maakContext();
        await using var transactie = await context.Database.BeginTransactionAsync(annulering).ConfigureAwait(false);
        var stand = await LaadAsync(context, administratie, annulering).ConfigureAwait(false);
        var resultaat = wijziging(stand);
        foreach (var item in resultaat.Verwijderen)
        {
            context.Remove(item);
        }

        foreach (var item in resultaat.Toevoegen)
        {
            context.Add(item);
        }

        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
        await transactie.CommitAsync(annulering).ConfigureAwait(false);
        return resultaat.Waarde;
    }

    private static async Task<BoekhoudingStand> LaadAsync(VerenigdDbContext context, AdministratieSoort administratie, CancellationToken annulering)
    {
        var rubrieken = await context.Rubrieken.Where(r => r.Administratie == administratie).OrderBy(r => r.Volgorde)
            .ToListAsync(annulering).ConfigureAwait(false);
        var rekeningen = await context.Grootboekrekeningen.Include(r => r.Rubriek).Where(r => r.Administratie == administratie)
            .OrderBy(r => r.Nummer).ToListAsync(annulering).ConfigureAwait(false);
        var dagboeken = await context.Dagboeken.Include(d => d.Rekening).Where(d => d.Administratie == administratie)
            .OrderBy(d => d.Soort).ToListAsync(annulering).ConfigureAwait(false);
        var boekjaren = await context.Boekjaren.Where(b => b.Administratie == administratie).OrderBy(b => b.Jaar)
            .ToListAsync(annulering).ConfigureAwait(false);
        var posten = await context.Journaalposten
            .Include(p => p.Dagboek)
            .Include(p => p.Regels).ThenInclude(r => r.Rekening)
            .Where(p => p.Administratie == administratie)
            .AsSplitQuery()
            .ToListAsync(annulering).ConfigureAwait(false);
        return new BoekhoudingStand(administratie, rubrieken, rekeningen, dagboeken, boekjaren,
            [.. posten.OrderBy(p => p.Datum).ThenBy(p => p.Volgnummer ?? int.MaxValue)]);
    }
}
