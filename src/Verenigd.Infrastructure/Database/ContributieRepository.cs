using Microsoft.EntityFrameworkCore;
using Verenigd.Application.Incasso;
using Verenigd.Domain.Financien;
using Verenigd.Infrastructure.Database.Leden;

namespace Verenigd.Infrastructure.Database;

/// <summary>
/// Opslag van contributie en incasso. Leest voor de berekening ook de ledengegevens (alleen-lezen); schrijft uitsluitend
/// financiële tabellen.
/// </summary>
internal sealed class ContributieRepository(Func<VerenigdDbContext> maakContext) : IContributieRepository
{
    public async Task<IncassoStand> LaadAsync(CancellationToken annulering = default)
    {
        await using var context = maakContext();
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
        return await LaadAsync(context, annulering).ConfigureAwait(false);
    }

    public async Task VoegCategorieenToeAsync(IReadOnlyList<Contributiecategorie> categorieen, CancellationToken annulering = default)
    {
        await using var context = maakContext();
        context.Contributiecategorieen.AddRange(categorieen);
        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
    }

    public async Task BewaarTarievenAsync(int boekjaar, IReadOnlyDictionary<int, Bedrag> bedragen, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(bedragen);
        await using var context = maakContext();
        var bestaand = await context.Tarieven.Include(t => t.Categorie).Where(t => t.Boekjaar == boekjaar).ToListAsync(annulering).ConfigureAwait(false);
        var categorieen = await context.Contributiecategorieen.ToListAsync(annulering).ConfigureAwait(false);
        foreach (var (categorieId, bedrag) in bedragen)
        {
            if (bestaand.Find(t => t.Categorie.Id == categorieId) is { } tarief)
            {
                tarief.WijzigBedrag(bedrag);
            }
            else
            {
                var categorie = categorieen.Find(c => c.Id == categorieId) ?? throw new KeyNotFoundException("De categorie bestaat niet.");
                context.Tarieven.Add(new Tarief(categorie, boekjaar, bedrag));
            }
        }

        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
    }

    public async Task BewaarLidCategorieenAsync(IReadOnlyList<LidCategorieInvoer> invoer, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(invoer);
        await using var context = maakContext();
        var categorieen = await context.Contributiecategorieen.ToListAsync(annulering).ConfigureAwait(false);
        var bestaand = await context.LidCategorieen.ToListAsync(annulering).ConfigureAwait(false);
        foreach (var regel in invoer)
        {
            var categorie = categorieen.Find(c => c.Id == regel.CategorieId) ?? throw new KeyNotFoundException("De categorie bestaat niet.");
            context.LidCategorieen.RemoveRange(bestaand.Where(b => b.Lidnummer == regel.Lidnummer && b.Ingangsdatum == regel.Ingangsdatum));
            context.LidCategorieen.Add(new LidCategorie(regel.Lidnummer, categorie, regel.Ingangsdatum));
        }

        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
    }

    public async Task<Guid> BewaarBetalerAsync(BetalerInvoer invoer, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(invoer);
        await using var context = maakContext();
        var betalers = await context.Betalers.ToListAsync(annulering).ConfigureAwait(false);
        var fouten = invoer.Fouten(betalers);
        if (fouten.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", fouten), nameof(invoer));
        }

        var iban = Iban.Maak(invoer.Iban);
        var betaler = invoer.Id is { } id
            ? betalers.Find(b => b.Id == id) ?? throw new KeyNotFoundException("De betaler bestaat niet meer.")
            : context.Betalers.Add(new Betaler(iban, invoer.Rekeninghouder)).Entity;
        betaler.WijzigIban(iban);
        betaler.WijzigRekeninghouder(invoer.Rekeninghouder);
        if (string.IsNullOrWhiteSpace(invoer.Mandaatkenmerk))
        {
            betaler.VerwijderMachtiging();
        }
        else
        {
            betaler.RegistreerMachtiging(invoer.Mandaatkenmerk, invoer.OndertekendOp!.Value);
            if (invoer.IngetrokkenOp is { } ingetrokken)
            {
                betaler.TrekMachtigingIn(ingetrokken);
            }
        }

        var koppelingen = await context.BetalerLeden.ToListAsync(annulering).ConfigureAwait(false);
        context.BetalerLeden.RemoveRange(koppelingen.Where(k => k.BetalerId == betaler.Id && !invoer.Leden.Contains(k.Lidnummer)));
        foreach (var lidnummer in invoer.Leden.Distinct())
        {
            if (koppelingen.Find(k => k.Lidnummer == lidnummer) is { } koppeling)
            {
                koppeling.KoppelAan(betaler.Id);
            }
            else
            {
                context.BetalerLeden.Add(new Domain.Financien.BetalerLid(lidnummer, betaler.Id));
            }
        }

        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
        return betaler.Id;
    }

    public async Task BewaarInstellingenAsync(IncassoinstellingenInvoer invoer, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(invoer);
        if (!IncassantId.TryMaak(invoer.IncassantId, out var incassantId))
        {
            throw new ArgumentException("Het incassant-ID is ongeldig (controleer het controlegetal).", nameof(invoer));
        }

        if (!Iban.TryMaak(invoer.Iban, out var iban) || !iban.ControlegetalGeldig)
        {
            throw new ArgumentException("Het IBAN van de Vereniging is ongeldig.", nameof(invoer));
        }

        await using var context = maakContext();
        var bestaand = await context.Incassoinstellingen.SingleOrDefaultAsync(annulering).ConfigureAwait(false);
        if (bestaand is null)
        {
            var nieuw = new Incassoinstellingen(incassantId, invoer.Naam, iban, invoer.Incassodag, invoer.EersteIncassomaand);
            nieuw.Wijzig(incassantId, invoer.Naam, iban, invoer.Incassodag, invoer.EersteIncassomaand, invoer.Omschrijvingssjabloon);
            context.Incassoinstellingen.Add(nieuw);
        }
        else
        {
            bestaand.Wijzig(incassantId, invoer.Naam, iban, invoer.Incassodag, invoer.EersteIncassomaand, invoer.Omschrijvingssjabloon);
        }

        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
    }

    public async Task BewaarRunAsync(Func<IncassoStand, Incassorun> maakRun, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(maakRun);
        await using var context = maakContext();
        await using var transactie = await context.Database.BeginTransactionAsync(annulering).ConfigureAwait(false);
        var stand = await LaadAsync(context, annulering).ConfigureAwait(false);
        context.Incassoruns.Add(maakRun(stand));
        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
        await transactie.CommitAsync(annulering).ConfigureAwait(false);
    }

    public async Task LegBestandVastAsync(Guid runId, string hash, CancellationToken annulering = default)
    {
        await using var context = maakContext();
        var run = await context.Incassoruns.SingleAsync(r => r.Id == runId, annulering).ConfigureAwait(false);
        run.LegBestandVast(hash);
        await context.SaveChangesAsync(annulering).ConfigureAwait(false);
    }

    public async Task AnnuleerRunAsync(Guid runId, DateTime nuUtc, CancellationToken annulering = default)
    {
        await using var context = maakContext();
        var run = await context.Incassoruns.Include(r => r.Posten).ThenInclude(p => p.Regels)
            .SingleAsync(r => r.Id == runId, annulering).ConfigureAwait(false);
        if (run.Status == IncassorunStatus.Definitief)
        {
            run.Annuleer(nuUtc);
            await context.SaveChangesAsync(annulering).ConfigureAwait(false);
        }
    }

    private static async Task<IncassoStand> LaadAsync(VerenigdDbContext context, CancellationToken annulering)
    {
        var personen = await LedenRepository.MetDetails(context.Personen).ToListAsync(annulering).ConfigureAwait(false);
        var categorieen = await context.Contributiecategorieen.OrderBy(c => c.Naam).ToListAsync(annulering).ConfigureAwait(false);
        var tarieven = await context.Tarieven.Include(t => t.Categorie).ToListAsync(annulering).ConfigureAwait(false);
        var lidCategorieen = await context.LidCategorieen.Include(c => c.Categorie).ToListAsync(annulering).ConfigureAwait(false);
        var betalers = await context.Betalers.ToListAsync(annulering).ConfigureAwait(false);
        var koppelingen = await context.BetalerLeden.ToListAsync(annulering).ConfigureAwait(false);
        var runs = await context.Incassoruns.Include(r => r.Posten).ThenInclude(p => p.Regels).AsSplitQuery()
            .ToListAsync(annulering).ConfigureAwait(false);
        var instellingen = await context.Incassoinstellingen.SingleOrDefaultAsync(annulering).ConfigureAwait(false);
        return new IncassoStand(
            personen, categorieen, tarieven, lidCategorieen, betalers, koppelingen,
            [.. runs.OrderByDescending(r => r.Maand).ThenByDescending(r => r.DefinitiefUtc)], instellingen);
    }
}
