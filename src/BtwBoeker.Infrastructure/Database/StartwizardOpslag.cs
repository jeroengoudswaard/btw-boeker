using Microsoft.EntityFrameworkCore;
using BtwBoeker.Application.Inrichting;

namespace BtwBoeker.Infrastructure.Database;

/// <summary>Eén rij met de voortgang van de startwizard.</summary>
internal sealed class StartwizardRij
{
    public const int VasteId = 1;

    public int Id { get; set; } = VasteId;

    public Startstap? Stap { get; set; }

    public bool IsAfgerond { get; set; }
}

internal sealed class StartwizardOpslag(Func<BtwBoekerDbContext> maakContext) : IStartwizardOpslag
{
    public async Task<Startwizardvoortgang> LaadAsync(CancellationToken annulering = default)
    {
        await using var context = maakContext();
        var rij = await context.Startwizard.AsNoTracking().SingleOrDefaultAsync(annulering).ConfigureAwait(false);
        return rij is null ? Startwizardvoortgang.Nieuw : new Startwizardvoortgang(rij.Stap, rij.IsAfgerond);
    }

    public async Task BewaarAsync(Startwizardvoortgang voortgang, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(voortgang);
        await using var context = maakContext();
        var rij = await context.Startwizard.SingleOrDefaultAsync(annulering).ConfigureAwait(false);
        if (rij is null)
        {
            rij = new StartwizardRij();
            context.Startwizard.Add(rij);
        }

        rij.Stap = voortgang.Stap;
        rij.IsAfgerond = voortgang.IsAfgerond;
        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
    }
}
