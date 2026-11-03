using Microsoft.EntityFrameworkCore;
using Verenigd.Application.Email;
using Verenigd.Domain.Communicatie;

namespace Verenigd.Infrastructure.Database;

internal sealed class EmailOpslag(Func<VerenigdDbContext> maakContext) : IEmailOpslag
{
    /// <summary>Genoeg voor het overzicht en de daglimiet (hooguit ±500 per dag).</summary>
    private const int LogboekLimiet = 2000;

    public async Task<EmailStand> LaadAsync(CancellationToken annulering = default)
    {
        await using var context = maakContext();
        var sjablonen = await context.Emailsjablonen.AsNoTracking().OrderBy(s => s.Soort).ThenBy(s => s.Naam).ToListAsync(annulering).ConfigureAwait(false);
        var logboek = await context.VerzondenBerichten.AsNoTracking().OrderByDescending(b => b.Id).Take(LogboekLimiet).ToListAsync(annulering).ConfigureAwait(false);
        var koppelingen = await context.Emailinstellingen.AsNoTracking().ToListAsync(annulering).ConfigureAwait(false);
        return new EmailStand(sjablonen, logboek, koppelingen);
    }

    public async Task BewaarSjabloonAsync(Emailsjabloon sjabloon, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(sjabloon);
        await using var context = maakContext();
        if (sjabloon.Id == 0)
        {
            context.Emailsjablonen.Add(sjabloon);
        }
        else
        {
            context.Emailsjablonen.Update(sjabloon);
        }

        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
    }

    public async Task VerwijderSjabloonAsync(int id, CancellationToken annulering = default)
    {
        await using var context = maakContext();
        await context.Emailsjablonen.Where(s => s.Id == id && !s.IsStandaard).ExecuteDeleteAsync(annulering).ConfigureAwait(false);
        context.Wijzigingen?.Verhoog(); // ExecuteDelete gaat buiten SaveChanges om
    }

    public async Task BewaarInstellingenAsync(Emailinstellingen instellingen, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(instellingen);
        await using var context = maakContext();
        if (await context.Emailinstellingen.AnyAsync(i => i.Id == instellingen.Id, annulering).ConfigureAwait(false))
        {
            context.Emailinstellingen.Update(instellingen);
        }
        else
        {
            context.Emailinstellingen.Add(instellingen);
        }

        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
    }

    public async Task LogAsync(VerzondenBericht bericht, CancellationToken annulering = default)
    {
        await using var context = maakContext();
        context.VerzondenBerichten.Add(bericht);
        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
    }
}
