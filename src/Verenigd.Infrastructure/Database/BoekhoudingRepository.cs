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

    public async Task<byte[]?> LaadBijlageInhoudAsync(Guid bijlageId, CancellationToken annulering = default)
    {
        await using var context = maakContext();
        return await context.BijlageInhoud.AsNoTracking().Where(i => i.BijlageId == bijlageId).Select(i => i.Gegevens)
            .SingleOrDefaultAsync(annulering).ConfigureAwait(false);
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
        return resultaat.Uitkomst();
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
        var afschriften = await context.Bankafschriften.Include(a => a.Dagboek).Where(a => a.Administratie == administratie)
            .OrderBy(a => a.Datum).ToListAsync(annulering).ConfigureAwait(false);
        var mutaties = await context.Bankmutaties.Include(m => m.Dagboek).Where(m => m.Administratie == administratie)
            .OrderBy(m => m.Boekdatum).ToListAsync(annulering).ConfigureAwait(false);
        var regels = await context.Boekingsregels.Include(r => r.Rekening).Where(r => r.Administratie == administratie)
            .OrderBy(r => r.Volgorde).ThenBy(r => r.Id).ToListAsync(annulering).ConfigureAwait(false);
        var debiteurmutaties = await context.Debiteurmutaties.Where(m => m.Administratie == administratie)
            .OrderBy(m => m.Datum).ToListAsync(annulering).ConfigureAwait(false);
        // Contributie hoort alleen bij de Vereniging (besluit 16).
        var runs = administratie == AdministratieSoort.Vereniging
            ? await context.Incassoruns.Include(r => r.Posten).ThenInclude(p => p.Regels).AsSplitQuery().ToListAsync(annulering).ConfigureAwait(false)
            : [];
        var herincassos = administratie == AdministratieSoort.Vereniging
            ? await context.Herincassos.ToListAsync(annulering).ConfigureAwait(false)
            : [];
        // Relaties zijn gedeeld door beide administraties (besluit 105).
        var relaties = await context.Relaties.OrderBy(r => r.Naam).ToListAsync(annulering).ConfigureAwait(false);
        var facturen = await context.Verkoopfacturen.Include(f => f.Regels).ThenInclude(r => r.Rekening)
            .Where(f => f.Administratie == administratie).AsSplitQuery().ToListAsync(annulering).ConfigureAwait(false);
        var factuurinstellingen = await context.Factuurinstellingen.SingleOrDefaultAsync(i => i.Administratie == administratie, annulering)
            .ConfigureAwait(false);
        var historischeRegels = await context.HistorischeRegels.Include(r => r.Rekening).Where(r => r.Administratie == administratie)
            .OrderBy(r => r.Jaar).ThenBy(r => r.Datum).ThenBy(r => r.Volgorde).ToListAsync(annulering).ConfigureAwait(false);
        var historischeTotalen = await context.HistorischeTotalen.Include(t => t.Rekening).Where(t => t.Administratie == administratie)
            .ToListAsync(annulering).ConfigureAwait(false);
        // Zonder inhoud: die wordt pas geladen bij bekijken of exporteren.
        var bijlagen = await context.Bijlagen.Where(b => b.Administratie == administratie).OrderBy(b => b.ToegevoegdUtc)
            .ToListAsync(annulering).ConfigureAwait(false);
        var begrotingen = await context.Begrotingen.Include(b => b.Regels).ThenInclude(r => r.Rekening)
            .Where(b => b.Administratie == administratie).OrderBy(b => b.Jaar).ThenBy(b => b.Versie)
            .AsSplitQuery().ToListAsync(annulering).ConfigureAwait(false);
        var meerjarenplannen = await context.Meerjarenplannen.Include(p => p.Posten).ThenInclude(p => p.Rekening)
            .Include(p => p.Posten).ThenInclude(p => p.Bedragen)
            .Where(p => p.Administratie == administratie).OrderBy(p => p.Startjaar).ThenBy(p => p.Versie)
            .AsSplitQuery().ToListAsync(annulering).ConfigureAwait(false);
        return new BoekhoudingStand(administratie, rubrieken, rekeningen, dagboeken, boekjaren,
            [.. posten.OrderBy(p => p.Datum).ThenBy(p => p.Volgnummer ?? int.MaxValue)])
        {
            Bankafschriften = afschriften,
            Bankmutaties = mutaties,
            Boekingsregels = regels,
            Debiteurmutaties = debiteurmutaties,
            Incassoruns = runs,
            Herincassos = herincassos,
            Relaties = relaties,
            Verkoopfacturen = facturen,
            Factuurinstellingen = factuurinstellingen,
            HistorischeRegels = historischeRegels,
            HistorischeTotalen = historischeTotalen,
            Bijlagen = bijlagen,
            Begrotingen = begrotingen,
            Meerjarenplannen = meerjarenplannen,
        };
    }
}
