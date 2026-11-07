using Microsoft.EntityFrameworkCore;
using Verenigd.Application.Import;
using Verenigd.Infrastructure.Database.Leden;

namespace Verenigd.Infrastructure.Database;

/// <summary>
/// Leest en schrijft alles wat de SOL3-import raakt: ledengegevens én betalers. Dit is de enige plek die beide
/// kanten tegelijk aanspreekt; de koppeling blijft het lidnummer.
/// </summary>
internal sealed class LedenImportOpslag(Func<VerenigdDbContext> maakContext) : ILedenImportOpslag
{
    public async Task<ImportStand> LaadAsync(CancellationToken annulering = default)
    {
        await using var context = maakContext();
        return await LaadAsync(QueryTrackingBehavior.NoTrackingWithIdentityResolution, context, annulering).ConfigureAwait(false);
    }

    public async Task PasToeAsync(Func<ImportStand, ImportWijzigingen> wijziging, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(wijziging);
        await using var context = maakContext();
        await using var transactie = await context.Database.BeginTransactionAsync(annulering).ConfigureAwait(false);

        var stand = await LaadAsync(QueryTrackingBehavior.TrackAll, context, annulering).ConfigureAwait(false);
        var nieuw = wijziging(stand);
        context.Groepen.AddRange(nieuw.NieuweGroepen);
        context.Personen.AddRange(nieuw.NieuwePersonen);
        context.Betalers.AddRange(nieuw.NieuweBetalers);
        context.BetalerLeden.AddRange(nieuw.NieuweKoppelingen);

        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
        await transactie.CommitAsync(annulering).ConfigureAwait(false);
        context.Wijzigingen?.Verhoog(); // pas nu zichtbaar voor andere verbindingen (besluit 277)
    }

    private static async Task<ImportStand> LaadAsync(QueryTrackingBehavior gedrag, VerenigdDbContext context, CancellationToken annulering)
    {
        context.ChangeTracker.QueryTrackingBehavior = gedrag;
        var groepen = await context.Groepen.ToListAsync(annulering).ConfigureAwait(false);
        var personen = await LedenRepository.MetDetails(context.Personen).ToListAsync(annulering).ConfigureAwait(false);
        var betalers = await context.Betalers.ToListAsync(annulering).ConfigureAwait(false);
        var koppelingen = await context.BetalerLeden.ToListAsync(annulering).ConfigureAwait(false);
        return new ImportStand(personen, groepen, betalers, koppelingen);
    }
}
