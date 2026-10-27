using System.Globalization;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Domain.Financien.Debiteuren;

namespace Verenigd.Application.Debiteuren;

/// <summary>De vaste rekeningen voor debiteuren en crediteuren (besluit 90).</summary>
public static class Debiteurenrekeningen
{
    public const int Contributie = 1300;
    public const int Overig = 1310;
    public const int Crediteuren = 1600;
    public const string Rubriek = "Vorderingen en schulden";

    /// <summary>Maakt 1300, 1310 en 1600 (en de rubriek) aan als ze nog niet bestaan; geeft 1300.</summary>
    internal static Grootboekrekening Zorg(BoekhoudingStand stand, AdministratieSoort administratie, List<object> toevoegen)
    {
        var rubriek = stand.Rubrieken.FirstOrDefault(r => string.Equals(r.Naam, Rubriek, StringComparison.OrdinalIgnoreCase));
        Grootboekrekening Rekening(int nummer, string naam)
        {
            if (stand.Rekeningen.FirstOrDefault(r => r.Nummer == nummer) is { } bestaand)
            {
                return bestaand;
            }

            if (rubriek is null)
            {
                rubriek = new Rubriek(administratie, Rubriek, stand.Rubrieken.Select(r => r.Volgorde).DefaultIfEmpty(0).Max() + 1);
                toevoegen.Add(rubriek);
            }

            var rekening = new Grootboekrekening(administratie, nummer, naam, RekeningSoort.Balans, rubriek);
            toevoegen.Add(rekening);
            return rekening;
        }

        var contributie = Rekening(Contributie, "Debiteuren contributie");
        Rekening(Overig, "Debiteuren overig");
        Rekening(Crediteuren, "Crediteuren");
        if (contributie.Soort != RekeningSoort.Balans)
        {
            throw new InvalidOperationException($"Rekening {contributie} moet een balansrekening zijn.");
        }

        return contributie;
    }
}

/// <summary>
/// Contributie in de boekhouding (stap 4c-1, besluiten 63 en 89–103): de incassorun boeken, storneringen opvolgen
/// (herincasso of oninbaar) en betalingen handmatig afletteren. Leest de contributiekant alleen; schrijft in de boekhouding,
/// behalve bij een herincasso.
/// </summary>
public sealed class ContributieboekingDienst(IDatabaseSessie sessie, TimeProvider tijd)
{
    private const AdministratieSoort Vereniging = AdministratieSoort.Vereniging; // besluit 16

    private DateTime NuUtc => tijd.GetUtcNow().UtcDateTime;

    /// <summary>De categorieën in de run zonder opbrengstrekening; leeg als de run kan worden geboekt.</summary>
    public async Task<IReadOnlyList<string>> OntbrekendeOpbrengstrekeningenAsync(Guid runId, CancellationToken annulering = default)
    {
        var contributie = await sessie.Contributie.LaadAsync(annulering).ConfigureAwait(false);
        var stand = await sessie.Boekhouding.LaadAsync(Vereniging, annulering).ConfigureAwait(false);
        return Ontbrekend(Run(contributie.Runs, runId), contributie.Categorieen, stand);
    }

    /// <summary>Besluit 99: boekt de run op de uitvoerdatum (besluit 91). Geeft het postnummer, of <c>null</c> als er niets te boeken is.</summary>
    /// <exception cref="InvalidOperationException">Al geboekt, geannuleerd, boekjaar niet open of een opbrengstrekening ontbreekt.</exception>
    public async Task<string?> BoekRunAsync(Guid runId, CancellationToken annulering = default)
    {
        var contributie = await sessie.Contributie.LaadAsync(annulering).ConfigureAwait(false);
        var run = Run(contributie.Runs, runId);
        return await sessie.Boekhouding.PasToeAsync(Vereniging, stand =>
        {
            if (run.Status != IncassorunStatus.Definitief)
            {
                throw new InvalidOperationException("Een geannuleerde run wordt niet geboekt.");
            }

            if (Contributievorderingen.IsGeboekt(stand, run.Id))
            {
                throw new InvalidOperationException($"De run van {run.Maand} is al geboekt.");
            }

            if (Ontbrekend(run, contributie.Categorieen, stand) is { Count: > 0 } ontbrekend)
            {
                throw new InvalidOperationException($"Kies eerst een opbrengstrekening voor {string.Join(", ", ontbrekend)} (Contributie › Tarieven).");
            }

            var toevoegen = new List<object>();
            var debiteuren = Debiteurenrekeningen.Zorg(stand, Vereniging, toevoegen);
            var gewoon = run.Posten.Where(p => !p.IsHerincasso).ToList();
            if (gewoon.Count == 0)
            {
                return Wijziging<string?>(null, toevoegen);
            }

            var rekeningen = Rekeningen(stand.Rekeningen.Concat(toevoegen.OfType<Grootboekrekening>()), contributie.Categorieen);
            var opbrengst = gewoon.SelectMany(p => p.Regels)
                .GroupBy(r => rekeningen[r.Categorie])
                .OrderBy(g => g.Key.Nummer)
                .Select(g => new RegelInvoer(g.Key, null, Bedrag.Nul, g.Select(r => r.Bedrag).Som()));
            var totaal = gewoon.Select(p => p.Bedrag).Som();
            var post = Memoriaalpost(stand, run.Uitvoerdatum, $"Contributie {run.Maand} (incasso {run.Kenmerk})",
                [new RegelInvoer(debiteuren, null, totaal, Bedrag.Nul), .. opbrengst]);
            toevoegen.Add(post);
            toevoegen.AddRange(gewoon.Select(p => new Debiteurmutatie(
                Vereniging, p.Vordering, p.BetalerId, run.Id, DebiteurmutatieSoort.Vordering, run.Uitvoerdatum, p.Bedrag.Centen, post.Id, null)));
            return Wijziging<string?>(post.Nummer, toevoegen);
        }, annulering).ConfigureAwait(false);
    }

    /// <summary>Annuleert een run (besluit 60); een geboekte run wordt tegengeboekt. Niet meer als de bank de batch heeft uitgekeerd.</summary>
    public async Task AnnuleerRunAsync(Guid runId, CancellationToken annulering = default)
    {
        var contributie = await sessie.Contributie.LaadAsync(annulering).ConfigureAwait(false);
        var run = Run(contributie.Runs, runId);
        await sessie.Boekhouding.PasToeAsync(Vereniging, stand =>
        {
            if (Contributievorderingen.IsOntvangen(stand, run.Id))
            {
                throw new InvalidOperationException("De bank heeft deze run al uitgevoerd; hij kan niet meer worden geannuleerd.");
            }

            var wijziging = new BoekhoudingWijziging<bool>(true);
            var runposten = Contributievorderingen.Effectief(stand)
                .Where(m => m.IncassorunId == run.Id && m.Soort == DebiteurmutatieSoort.Vordering)
                .Select(m => m.PostId).Distinct().ToList();
            foreach (var postId in runposten)
            {
                var post = stand.Posten.Single(p => p.Id == postId);
                var datum = stand.Boekjaar(post.Datum.Year) is { IsOpen: true } ? post.Datum : DateOnly.FromDateTime(tijd.GetLocalNow().DateTime);
                var tegen = post.MaakTegenboeking(datum);
                tegen.Boek(BoekhoudingDienst.VolgendNummer(stand with { Posten = [.. stand.Posten, .. wijziging.Toevoegen.OfType<Journaalpost>()] }, tegen), NuUtc);
                wijziging.Toevoegen.Add(tegen);
            }

            return wijziging;
        }, annulering).ConfigureAwait(false);
        await sessie.Contributie.AnnuleerRunAsync(run.Id, NuUtc, annulering).ConfigureAwait(false);
        if (run.Status == IncassorunStatus.Definitief)
        {
            run.Annuleer(NuUtc);
        }
    }

    /// <summary>Besluit 92: de gestorneerde post gaat mee in de volgende run.</summary>
    public async Task VraagHerincassoAanAsync(string vordering, CancellationToken annulering = default)
    {
        var stand = await sessie.Boekhouding.LaadAsync(Vereniging, annulering).ConfigureAwait(false);
        var v = Contributievorderingen.Bereken(stand).SingleOrDefault(x => x.Vordering == vordering)
            ?? throw new InvalidOperationException("De vordering bestaat niet.");
        if (v.Status != Vorderingstatus.Gestorneerd)
        {
            throw new InvalidOperationException("Alleen een gestorneerde post kan opnieuw worden geïncasseerd.");
        }

        if (v.HerincassoGepland)
        {
            throw new InvalidOperationException("Deze post gaat al mee in de volgende run.");
        }

        if (v.Saldo != v.Bedrag.Centen)
        {
            throw new InvalidOperationException("Er is al een deel betaald; letter de rest handmatig af.");
        }

        await sessie.Contributie.VraagHerincassoAanAsync(new Herincasso(vordering, NuUtc), annulering).ConfigureAwait(false);
    }

    /// <summary>Besluit 101: boekt het openstaande bedrag af op <paramref name="kostenrekeningId"/>. Geeft het postnummer.</summary>
    public Task<string> BoekOninbaarAsync(string vordering, int kostenrekeningId, CancellationToken annulering = default) =>
        sessie.Boekhouding.PasToeAsync(Vereniging, stand =>
        {
            var v = Contributievorderingen.Bereken(stand).SingleOrDefault(x => x.Vordering == vordering)
                ?? throw new InvalidOperationException("De vordering bestaat niet.");
            if (v.Saldo <= 0)
            {
                throw new InvalidOperationException("Deze post staat niet meer open.");
            }

            var kosten = stand.Rekeningen.SingleOrDefault(r => r.Id == kostenrekeningId) ?? throw new InvalidOperationException("De rekening bestaat niet.");
            var toevoegen = new List<object>();
            var debiteuren = Debiteurenrekeningen.Zorg(stand, Vereniging, toevoegen);
            var vandaag = DateOnly.FromDateTime(tijd.GetLocalNow().DateTime);
            var bedrag = new Bedrag(v.Saldo);
            var post = Memoriaalpost(stand, vandaag, $"Oninbaar: contributie {v.Run.Maand}, {v.Post.Rekeninghouder}",
                [new RegelInvoer(kosten, null, bedrag, Bedrag.Nul), new RegelInvoer(debiteuren, null, Bedrag.Nul, bedrag)]);
            toevoegen.Add(post);
            toevoegen.Add(new Debiteurmutatie(Vereniging, v.Vordering, v.Post.BetalerId, null, DebiteurmutatieSoort.Oninbaar, vandaag, -v.Saldo, post.Id, null));
            return Wijziging(post.Nummer, toevoegen);
        }, annulering);

    /// <summary>Een ontvangst uit de werkvoorraad afletteren met een of meer open posten; de som moet precies kloppen.</summary>
    public Task<Guid> LetterAfAsync(Guid mutatieId, IReadOnlyList<string> vorderingen, CancellationToken annulering = default) =>
        sessie.Boekhouding.PasToeAsync(Vereniging, stand =>
        {
            ArgumentNullException.ThrowIfNull(vorderingen);
            var mutatie = stand.Bankmutaties.SingleOrDefault(m => m.Id == mutatieId) ?? throw new InvalidOperationException("De banktransactie bestaat niet meer.");
            if (mutatie.PostId is not null || !mutatie.IsBij)
            {
                throw new InvalidOperationException("Alleen een ontvangst uit de werkvoorraad kan worden afgeletterd.");
            }

            var open = Contributievorderingen.Bereken(stand).Where(v => v.Saldo > 0 && vorderingen.Contains(v.Vordering)).ToList();
            if (open.Count != vorderingen.Distinct().Count())
            {
                throw new InvalidOperationException("Een gekozen post staat niet (meer) open.");
            }

            var som = open.Sum(v => v.Saldo);
            if (som != mutatie.Bedrag.Centen)
            {
                throw new InvalidOperationException($"De gekozen posten ({new Bedrag(som)}) zijn samen niet gelijk aan het ontvangen bedrag ({mutatie.Bedrag}).");
            }

            var toevoegen = new List<object>();
            var debiteuren = Debiteurenrekeningen.Zorg(stand, Vereniging, toevoegen);
            var omschrijving = "Betaling contributie: " + string.Join(", ", open.Select(v => $"{v.Run.Maand} {v.Post.Rekeninghouder}").Distinct());
            var post = mutatie.MaakPost(debiteuren, omschrijving.Length > Journaalpost.MaximaleOmschrijvingLengte ? omschrijving[..Journaalpost.MaximaleOmschrijvingLengte] : omschrijving);
            mutatie.Koppel(post);
            toevoegen.Add(post);
            toevoegen.AddRange(open.Select(v => new Debiteurmutatie(
                Vereniging, v.Vordering, v.Post.BetalerId, null, DebiteurmutatieSoort.Betaling, mutatie.Boekdatum, -v.Saldo, post.Id, null)));
            return Wijziging(post.Id, toevoegen);
        }, annulering);

    private static Incassorun Run(IReadOnlyList<Incassorun> runs, Guid id) =>
        runs.SingleOrDefault(r => r.Id == id) ?? throw new InvalidOperationException("De run bestaat niet.");

    private static List<string> Ontbrekend(Incassorun run, IReadOnlyList<Contributiecategorie> categorieen, BoekhoudingStand stand)
    {
        var rekeningen = Rekeningen(stand.Rekeningen, categorieen);
        return [.. run.Posten.Where(p => !p.IsHerincasso).SelectMany(p => p.Regels).Select(r => r.Categorie)
            .Distinct(StringComparer.Ordinal).Where(c => !rekeningen.ContainsKey(c)).Order(StringComparer.CurrentCulture)];
    }

    /// <summary>Categorienaam → opbrengstrekening; alleen bestaande, actieve rekeningen.</summary>
    private static Dictionary<string, Grootboekrekening> Rekeningen(IEnumerable<Grootboekrekening> rekeningen, IReadOnlyList<Contributiecategorie> categorieen)
    {
        var lijst = rekeningen.ToList();
        var uitkomst = new Dictionary<string, Grootboekrekening>(StringComparer.Ordinal);
        foreach (var categorie in categorieen)
        {
            if (categorie.OpbrengstrekeningId is { } id && lijst.FirstOrDefault(r => r.Id == id && r.IsActief) is { } rekening)
            {
                uitkomst[categorie.Naam] = rekening;
            }
        }

        return uitkomst;
    }

    private Journaalpost Memoriaalpost(BoekhoudingStand stand, DateOnly datum, string omschrijving, IReadOnlyList<RegelInvoer> regels)
    {
        if (stand.Boekjaar(datum.Year) is not { IsOpen: true })
        {
            throw new InvalidOperationException($"Boekjaar {datum.Year.ToString(CultureInfo.InvariantCulture)} is niet open.");
        }

        var memoriaal = stand.Dagboeken.FirstOrDefault(d => d.Soort == DagboekSoort.Memoriaal)
            ?? throw new InvalidOperationException("Er is geen memoriaal; richt eerst de boekhouding in.");
        var post = new Journaalpost(Vereniging, memoriaal, datum, omschrijving);
        post.Wijzig(memoriaal, datum, omschrijving, regels);
        post.Boek(BoekhoudingDienst.VolgendNummer(stand, post), NuUtc);
        return post;
    }

    private static BoekhoudingWijziging<T> Wijziging<T>(T waarde, List<object> toevoegen)
    {
        var wijziging = new BoekhoudingWijziging<T>(waarde);
        wijziging.Toevoegen.AddRange(toevoegen);
        return wijziging;
    }
}
