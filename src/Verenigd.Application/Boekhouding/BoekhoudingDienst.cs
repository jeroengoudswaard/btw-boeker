using System.Globalization;
using Verenigd.Application.Database;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Boekhouding;

/// <summary>
/// Boekhouding per administratie: posten (concept, boeken, tegenboeken), rekeningschema, jaarafsluiting en de eenmalige
/// overname uit Excel. Ingrijpende stappen (import, afsluiten) beginnen met een back-up.
/// </summary>
public sealed class BoekhoudingDienst(IDatabaseSessie sessie, TimeProvider tijd)
{
    private IBoekhoudingRepository Repository => sessie.Boekhouding;

    private DateTime NuUtc => tijd.GetUtcNow().UtcDateTime;

    public Task<BoekhoudingStand> LaadAsync(AdministratieSoort administratie, CancellationToken annulering = default) =>
        Repository.LaadAsync(administratie, annulering);

    /// <summary>Maakt Memoriaal en het lopende boekjaar aan voor een lege administratie.</summary>
    public Task RichtInAsync(AdministratieSoort administratie, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            var wijziging = new BoekhoudingWijziging<bool>(true);
            if (!stand.Dagboeken.Any(d => d.Soort == DagboekSoort.Memoriaal))
            {
                wijziging.Toevoegen.Add(new Dagboek(administratie, DagboekSoort.Memoriaal, "MEM", "Memoriaal", null));
            }

            var jaar = tijd.GetLocalNow().Year;
            if (stand.Boekjaar(jaar) is null)
            {
                wijziging.Toevoegen.Add(new Boekjaar(administratie, jaar));
            }

            return wijziging;
        }, annulering);

    /// <summary>Opent een (nieuw) boekjaar, bijvoorbeeld om alvast in het volgende jaar te boeken.</summary>
    public Task OpenBoekjaarAsync(AdministratieSoort administratie, int jaar, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            if (stand.Boekjaar(jaar) is not null)
            {
                throw new InvalidOperationException($"Boekjaar {VoorbladInlezer.Jaar(jaar)} bestaat al.");
            }

            var wijziging = new BoekhoudingWijziging<bool>(true);
            wijziging.Toevoegen.Add(new Boekjaar(administratie, jaar));
            return wijziging;
        }, annulering);

    public Task<Guid> BewaarConceptAsync(AdministratieSoort administratie, PostInvoer invoer, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            var (post, nieuw) = MaakOfWijzig(stand, administratie, invoer);
            var wijziging = new BoekhoudingWijziging<Guid>(post.Id);
            if (nieuw)
            {
                wijziging.Toevoegen.Add(post);
            }

            return wijziging;
        }, annulering);

    /// <summary>Slaat de post op en boekt hem. Geeft het nummer, bijvoorbeeld <c>MEM-2026-0001</c>.</summary>
    /// <exception cref="InvalidOperationException">De post klopt niet of het boekjaar is niet open.</exception>
    public Task<string> BoekAsync(AdministratieSoort administratie, PostInvoer invoer, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            var (post, nieuw) = MaakOfWijzig(stand, administratie, invoer);
            post.Boek(VolgendNummer(stand, post), NuUtc);
            var wijziging = new BoekhoudingWijziging<string>(post.Nummer);
            if (nieuw)
            {
                wijziging.Toevoegen.Add(post);
            }

            return wijziging;
        }, annulering);

    public Task VerwijderConceptAsync(AdministratieSoort administratie, Guid postId, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            var post = Post(stand, postId);
            if (post.IsGeboekt)
            {
                throw new InvalidOperationException("Een geboekte post kan niet worden verwijderd; maak een tegenboeking.");
            }

            // De banktransactie gaat terug naar de werkvoorraad (besluit 75); een aflettering vervalt.
            stand.MutatieVan(post.Id)?.Ontkoppel();
            var wijziging = new BoekhoudingWijziging<bool>(true);
            wijziging.Verwijderen.AddRange(stand.Debiteurmutaties.Where(m => m.PostId == post.Id));
            wijziging.Verwijderen.Add(post);
            return wijziging;
        }, annulering);

    /// <summary>Boekt het spiegelbeeld van een geboekte post op <paramref name="datum"/>.</summary>
    public Task<string> TegenboekAsync(AdministratieSoort administratie, Guid postId, DateOnly datum, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            ControleerOpenJaar(stand, datum);
            var tegen = Post(stand, postId).MaakTegenboeking(datum);
            stand.MutatieVan(postId)?.Ontkoppel();
            tegen.Boek(VolgendNummer(stand, tegen), NuUtc);
            var wijziging = new BoekhoudingWijziging<string>(tegen.Nummer);
            wijziging.Toevoegen.Add(tegen);
            return wijziging;
        }, annulering);

    /// <exception cref="ArgumentException">Nummer bestaat al of ongeldige invoer.</exception>
    /// <exception cref="InvalidOperationException">Soort wijzigen terwijl er al geboekt is op de rekening.</exception>
    public Task<int> BewaarRekeningAsync(AdministratieSoort administratie, RekeningInvoer invoer, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            ArgumentNullException.ThrowIfNull(invoer);
            if (stand.Rekeningen.Any(r => r.Nummer == invoer.Nummer && r.Id != invoer.Id))
            {
                throw new ArgumentException($"Rekeningnummer {invoer.Nummer} bestaat al.", nameof(invoer));
            }

            var rubriek = invoer.RubriekId is { } rid ? stand.Rubrieken.Single(r => r.Id == rid) : null;
            Grootboekrekening rekening = null!;
            var wijziging = new BoekhoudingWijziging<int>(0) { WaardeNaOpslaan = () => rekening.Id };
            if (invoer.Id is { } id)
            {
                rekening = stand.Rekeningen.Single(r => r.Id == id);
                if (rekening.Nummer != invoer.Nummer)
                {
                    throw new ArgumentException("Het nummer van een bestaande rekening kan niet worden gewijzigd.", nameof(invoer));
                }

                if (rekening.Soort != invoer.Soort && HeeftBoekingen(stand, rekening))
                {
                    throw new InvalidOperationException($"Op rekening {rekening} is al geboekt; de soort kan niet meer worden gewijzigd.");
                }

                rekening.Wijzig(invoer.Naam, rubriek, invoer.Soort);
            }
            else
            {
                rekening = new Grootboekrekening(administratie, invoer.Nummer, invoer.Naam, invoer.Soort, rubriek);
                wijziging.Toevoegen.Add(rekening);
            }

            if (invoer.Rol != RekeningRol.Geen)
            {
                foreach (var ander in stand.Rekeningen.Where(r => r.Rol == invoer.Rol && !ReferenceEquals(r, rekening)))
                {
                    ander.WijzigRol(RekeningRol.Geen);
                }
            }

            rekening.WijzigRol(invoer.Rol);
            if (invoer.IsActief)
            {
                rekening.Activeer();
            }
            else
            {
                rekening.Deactiveer();
            }

            if (invoer.Rol == RekeningRol.Kas && !stand.Dagboeken.Any(d => d.Soort == DagboekSoort.Kas))
            {
                wijziging.Toevoegen.Add(new Dagboek(administratie, DagboekSoort.Kas, "KAS", "Kas", rekening));
            }

            return wijziging;
        }, annulering);

    public Task BewaarRubriekAsync(AdministratieSoort administratie, RubriekInvoer invoer, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            ArgumentNullException.ThrowIfNull(invoer);
            var wijziging = new BoekhoudingWijziging<bool>(true);
            if (invoer.Id is { } id)
            {
                stand.Rubrieken.Single(r => r.Id == id).Wijzig(invoer.Naam, invoer.Volgorde);
            }
            else
            {
                wijziging.Toevoegen.Add(new Rubriek(administratie, invoer.Naam, invoer.Volgorde));
            }

            return wijziging;
        }, annulering);

    /// <summary>
    /// Sluit het boekjaar af: resultaat naar de reserve (afsluitpost), jaar vergrendelen en de openingsbalans van het
    /// volgende jaar maken. Geeft het resultaat (positief = overschot).
    /// </summary>
    /// <exception cref="InvalidOperationException">Er zijn nog concepten, er is geen reserverekening of het jaar is niet open.</exception>
    public async Task<Bedrag> SluitBoekjaarAsync(AdministratieSoort administratie, int jaar, CancellationToken annulering = default)
    {
        var vooraf = await Repository.LaadAsync(administratie, annulering).ConfigureAwait(false);
        ControleerAfsluiten(vooraf, jaar);
        await sessie.MaakBackupAsync(BackupPad("afsluiting-" + administratie.ToString().ToLowerInvariant() + "-" + VoorbladInlezer.Jaar(jaar)), annulering)
            .ConfigureAwait(false);

        return await Repository.PasToeAsync(administratie, stand =>
        {
            ControleerAfsluiten(stand, jaar);
            var reserve = stand.Rekeningen.Single(r => r.Rol == RekeningRol.Reserve);
            var memoriaal = Memoriaal(stand);
            var saldi = Grootboek.Saldibalans(stand, jaar).Regels.Where(r => r.Rekening.Soort == RekeningSoort.Resultaat && r.Saldo != 0).ToList();
            var resultaat = -saldi.Sum(r => r.Saldo);
            var wijziging = new BoekhoudingWijziging<Bedrag>(new Bedrag(resultaat));

            var toegevoegdePosten = new List<Journaalpost>();
            if (saldi.Count > 0)
            {
                var afsluiting = new Journaalpost(administratie, memoriaal, new DateOnly(jaar, 12, 31), $"Afsluiting boekjaar {VoorbladInlezer.Jaar(jaar)}", PostSoort.Afsluiting);
                var regels = saldi.Select(s => new RegelInvoer(s.Rekening, null, s.Saldo < 0 ? new Bedrag(-s.Saldo) : Bedrag.Nul, s.Saldo > 0 ? new Bedrag(s.Saldo) : Bedrag.Nul)).ToList();
                if (resultaat != 0)
                {
                    regels.Add(new RegelInvoer(reserve, "Resultaat " + VoorbladInlezer.Jaar(jaar), resultaat < 0 ? new Bedrag(-resultaat) : Bedrag.Nul, resultaat > 0 ? new Bedrag(resultaat) : Bedrag.Nul));
                }

                afsluiting.Wijzig(memoriaal, afsluiting.Datum, afsluiting.Omschrijving, regels);
                afsluiting.Boek(VolgendNummer(stand, afsluiting), NuUtc);
                toegevoegdePosten.Add(afsluiting);
                wijziging.Toevoegen.Add(afsluiting);
            }

            stand.Boekjaar(jaar)!.Sluit(NuUtc);
            if (stand.Boekjaar(jaar + 1) is null)
            {
                wijziging.Toevoegen.Add(new Boekjaar(administratie, jaar + 1));
            }

            var naAfsluiting = stand with { Posten = [.. stand.Posten, .. toegevoegdePosten] };
            var balans = Grootboek.Saldibalans(naAfsluiting, jaar).Regels.Where(r => r.Rekening.Soort == RekeningSoort.Balans && r.Saldo != 0).ToList();
            if (balans.Count > 0)
            {
                var opening = new Journaalpost(administratie, memoriaal, new DateOnly(jaar + 1, 1, 1), $"Openingsbalans {VoorbladInlezer.Jaar(jaar + 1)}", PostSoort.Openingsbalans);
                opening.Wijzig(memoriaal, opening.Datum, opening.Omschrijving,
                    balans.Select(b => new RegelInvoer(b.Rekening, null, b.Saldo > 0 ? new Bedrag(b.Saldo) : Bedrag.Nul, b.Saldo < 0 ? new Bedrag(-b.Saldo) : Bedrag.Nul)));
                opening.Boek(VolgendNummer(naAfsluiting, opening), NuUtc);
                wijziging.Toevoegen.Add(opening);
            }

            return wijziging;
        }, annulering).ConfigureAwait(false);
    }

    /// <summary>Heropent een afgesloten jaar: verwijdert de afsluitpost en de openingsbalans van het volgende jaar.</summary>
    public Task HeropenBoekjaarAsync(AdministratieSoort administratie, int jaar, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            var boekjaar = stand.Boekjaar(jaar) ?? throw new InvalidOperationException($"Boekjaar {VoorbladInlezer.Jaar(jaar)} bestaat niet.");
            if (boekjaar.IsOpen)
            {
                throw new InvalidOperationException($"Boekjaar {VoorbladInlezer.Jaar(jaar)} is niet afgesloten.");
            }

            if (stand.Boekjaar(jaar + 1) is { IsOpen: false })
            {
                throw new InvalidOperationException($"Heropen eerst boekjaar {VoorbladInlezer.Jaar(jaar + 1)}.");
            }

            var wijziging = new BoekhoudingWijziging<bool>(true);
            wijziging.Verwijderen.AddRange(stand.Posten.Where(p =>
                (p.Soort == PostSoort.Afsluiting && p.Datum.Year == jaar) || (p.Soort == PostSoort.Openingsbalans && p.Datum.Year == jaar + 1)));
            boekjaar.Heropen();
            return wijziging;
        }, annulering);

    /// <summary>
    /// Eenmalige overname uit Excel (besluit 62); alleen in een lege administratie. Een verschil in de beginbalans wordt
    /// geweigerd, tenzij de gebruiker bewust <paramref name="verschil"/> kiest (besluit 73).
    /// </summary>
    /// <exception cref="InvalidOperationException">Al ingericht, of niet in evenwicht zonder (geldige) verschilrekening.</exception>
    public async Task ImporteerAsync(
        AdministratieSoort administratie, VoorbladVoorstel voorstel, VerschilKeuze? verschil = null, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(voorstel);
        if (!voorstel.IsInEvenwicht && verschil is null)
        {
            throw new InvalidOperationException("De beginbalans is niet in evenwicht; er is niets geïmporteerd.");
        }

        if (verschil is not null
            && voorstel.Rekeningen.FirstOrDefault(r => r.Nummer == verschil.Nummer && !r.Overslaan) is { Soort: not RekeningSoort.Balans })
        {
            throw new InvalidOperationException("Het verschil kan alleen op een balansrekening (tussenrekening) worden geboekt.");
        }

        var vooraf = await Repository.LaadAsync(administratie, annulering).ConfigureAwait(false);
        if (vooraf.IsIngericht || vooraf.Posten.Count > 0)
        {
            throw new InvalidOperationException("Deze administratie heeft al een rekeningschema; de overname kan maar één keer.");
        }

        await sessie.MaakBackupAsync(BackupPad("boekhouding-import"), annulering).ConfigureAwait(false);
        await Repository.PasToeAsync(administratie, stand =>
        {
            if (stand.IsIngericht)
            {
                throw new InvalidOperationException("Deze administratie heeft al een rekeningschema.");
            }

            var wijziging = new BoekhoudingWijziging<bool>(true);
            var rubrieken = new Dictionary<string, Rubriek>(StringComparer.Ordinal);
            foreach (var naam in voorstel.Rekeningen.Where(r => !r.Overslaan && r.Rubriek is not null).Select(r => r.Rubriek!).Distinct())
            {
                var rubriek = new Rubriek(administratie, naam, rubrieken.Count + 1);
                rubrieken[naam] = rubriek;
                wijziging.Toevoegen.Add(rubriek);
            }

            var rekeningen = new List<(VoorbladRekening Bron, Grootboekrekening Rekening)>();
            foreach (var bron in voorstel.Rekeningen.Where(r => !r.Overslaan))
            {
                var rekening = new Grootboekrekening(administratie, bron.Nummer, bron.Naam, bron.Soort,
                    bron.Rubriek is null ? null : rubrieken[bron.Rubriek], bron.Rol);
                rekeningen.Add((bron, rekening));
                wijziging.Toevoegen.Add(rekening);
            }

            var memoriaal = stand.Dagboeken.FirstOrDefault(d => d.Soort == DagboekSoort.Memoriaal)
                ?? new Dagboek(administratie, DagboekSoort.Memoriaal, "MEM", "Memoriaal", null);
            if (!stand.Dagboeken.Contains(memoriaal))
            {
                wijziging.Toevoegen.Add(memoriaal);
            }

            if (rekeningen.Find(r => r.Rekening.Rol == RekeningRol.Kas) is { Rekening: { } kas } && !stand.Dagboeken.Any(d => d.Soort == DagboekSoort.Kas))
            {
                wijziging.Toevoegen.Add(new Dagboek(administratie, DagboekSoort.Kas, "KAS", "Kas", kas));
            }

            if (voorstel.MetVerkoopboek && !stand.Dagboeken.Any(d => d.Soort == DagboekSoort.Verkoop))
            {
                wijziging.Toevoegen.Add(new Dagboek(administratie, DagboekSoort.Verkoop, "VRK", "Verkoop", null));
            }

            if (stand.Boekjaar(voorstel.Beginjaar) is null)
            {
                wijziging.Toevoegen.Add(new Boekjaar(administratie, voorstel.Beginjaar));
            }

            Grootboekrekening? verschilrekening = null;
            if (!voorstel.IsInEvenwicht)
            {
                verschilrekening = rekeningen.Find(r => r.Rekening.Nummer == verschil!.Nummer).Rekening;
                if (verschilrekening is null)
                {
                    verschilrekening = new Grootboekrekening(administratie, verschil!.Nummer, verschil.Naam, RekeningSoort.Balans, null);
                    wijziging.Toevoegen.Add(verschilrekening);
                }
            }

            var saldi = rekeningen.Where(r => r.Bron.Debet != r.Bron.Credit).ToList();
            if (saldi.Count > 0)
            {
                var opening = new Journaalpost(administratie, memoriaal, new DateOnly(voorstel.Beginjaar, 1, 1),
                    $"Openingsbalans {VoorbladInlezer.Jaar(voorstel.Beginjaar)} ({voorstel.Herkomst})", PostSoort.Openingsbalans);
                var regels = saldi.Select(s =>
                {
                    var saldo = s.Bron.Debet.Centen - s.Bron.Credit.Centen;
                    return new RegelInvoer(s.Rekening, null, saldo > 0 ? new Bedrag(saldo) : Bedrag.Nul, saldo < 0 ? new Bedrag(-saldo) : Bedrag.Nul);
                }).ToList();
                if (verschilrekening is not null)
                {
                    var verschilbedrag = voorstel.TotaalDebet.Centen - voorstel.TotaalCredit.Centen;
                    regels.Add(new RegelInvoer(verschilrekening, $"Onverklaard verschil in de beginbalans, {voorstel.Herkomst} (te onderzoeken)",
                        verschilbedrag < 0 ? new Bedrag(-verschilbedrag) : Bedrag.Nul, verschilbedrag > 0 ? new Bedrag(verschilbedrag) : Bedrag.Nul));
                }

                opening.Wijzig(memoriaal, opening.Datum, opening.Omschrijving, regels);
                opening.Boek(1, NuUtc);
                wijziging.Toevoegen.Add(opening);
            }

            return wijziging;
        }, annulering).ConfigureAwait(false);
    }

    private static (Journaalpost Post, bool Nieuw) MaakOfWijzig(BoekhoudingStand stand, AdministratieSoort administratie, PostInvoer invoer)
    {
        ArgumentNullException.ThrowIfNull(invoer);
        ControleerOpenJaar(stand, invoer.Datum);
        var dagboek = stand.Dagboeken.SingleOrDefault(d => d.Id == invoer.DagboekId) ?? throw new InvalidOperationException("Het dagboek bestaat niet.");
        var regels = invoer.Regels.Select(r => new RegelInvoer(
            stand.Rekeningen.SingleOrDefault(g => g.Id == r.RekeningId) ?? throw new InvalidOperationException("Een rekening bestaat niet."),
            r.Omschrijving, r.Debet, r.Credit)).ToList();

        if (invoer.Id is { } id)
        {
            var bestaand = Post(stand, id);
            if (stand.MutatieVan(id) is { } mutatie)
            {
                if (!ReferenceEquals(dagboek, mutatie.Dagboek) && dagboek.Id != mutatie.Dagboek.Id)
                {
                    throw new InvalidOperationException($"Deze post hoort bij een banktransactie en blijft in dagboek {mutatie.Dagboek.Naam}.");
                }

                // De bankregel staat vast; eerst op een proefpost controleren, zodat de post zelf ongewijzigd blijft.
                var proef = new Journaalpost(administratie, dagboek, invoer.Datum, invoer.Omschrijving);
                proef.Wijzig(dagboek, invoer.Datum, invoer.Omschrijving, regels);
                if (mutatie.ControleerBankregel(proef) is { } fout)
                {
                    throw new InvalidOperationException(fout);
                }
            }

            bestaand.Wijzig(dagboek, invoer.Datum, invoer.Omschrijving, regels);
            return (bestaand, false);
        }

        if (dagboek.Soort == DagboekSoort.Bank)
        {
            throw new InvalidOperationException("Posten in een bankdagboek ontstaan uit het bankafschrift; gebruik het subtabblad Bank.");
        }

        var post = new Journaalpost(administratie, dagboek, invoer.Datum, invoer.Omschrijving);
        post.Wijzig(dagboek, invoer.Datum, invoer.Omschrijving, regels);
        return (post, true);
    }

    private static Journaalpost Post(BoekhoudingStand stand, Guid id) =>
        stand.Posten.SingleOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("De post bestaat niet meer.");

    internal static int VolgendNummer(BoekhoudingStand stand, Journaalpost post) =>
        stand.Posten
            .Where(p => p.IsGeboekt && p.Datum.Year == post.Datum.Year && (ReferenceEquals(p.Dagboek, post.Dagboek) || (p.Dagboek.Id != 0 && p.Dagboek.Id == post.Dagboek.Id)))
            .Select(p => p.Volgnummer ?? 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

    private static void ControleerOpenJaar(BoekhoudingStand stand, DateOnly datum)
    {
        var jaar = stand.Boekjaar(datum.Year);
        if (jaar is null)
        {
            throw new InvalidOperationException($"Boekjaar {VoorbladInlezer.Jaar(datum.Year)} bestaat niet. Open het eerst bij Boekjaren.");
        }

        if (!jaar.IsOpen)
        {
            throw new InvalidOperationException($"Boekjaar {VoorbladInlezer.Jaar(datum.Year)} is afgesloten.");
        }
    }

    private static void ControleerAfsluiten(BoekhoudingStand stand, int jaar)
    {
        var boekjaar = stand.Boekjaar(jaar) ?? throw new InvalidOperationException($"Boekjaar {VoorbladInlezer.Jaar(jaar)} bestaat niet.");
        if (!boekjaar.IsOpen)
        {
            throw new InvalidOperationException($"Boekjaar {VoorbladInlezer.Jaar(jaar)} is al afgesloten.");
        }

        if (stand.Boekjaren.Any(b => b.Jaar < jaar && b.IsOpen))
        {
            throw new InvalidOperationException("Sluit eerst de eerdere boekjaren af.");
        }

        var concepten = stand.Posten.Count(p => !p.IsGeboekt && p.Datum.Year == jaar);
        if (concepten > 0)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.CurrentCulture,
                $"Er {(concepten == 1 ? "staat nog 1 conceptpost" : $"staan nog {concepten} conceptposten")} in {jaar}. Boek of verwijder ze eerst."));
        }

        if (!stand.Rekeningen.Any(r => r.Rol == RekeningRol.Reserve))
        {
            throw new InvalidOperationException("Wijs eerst een reserverekening aan (bijv. 600 Algemene reserve) bij Rekeningschema.");
        }

        _ = Memoriaal(stand);
    }

    private static Dagboek Memoriaal(BoekhoudingStand stand) =>
        stand.Dagboeken.FirstOrDefault(d => d.Soort == DagboekSoort.Memoriaal) ?? throw new InvalidOperationException("Het dagboek Memoriaal ontbreekt.");

    private static bool HeeftBoekingen(BoekhoudingStand stand, Grootboekrekening rekening) =>
        stand.Posten.Any(p => p.IsGeboekt && p.Regels.Any(r => Grootboek.ZelfdeRekening(r.Rekening, rekening)));

    private string BackupPad(string reden) =>
        Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(sessie.Pad))!,
            $"{Path.GetFileNameWithoutExtension(sessie.Pad)}-voor-{reden}-{tijd.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.vbak");
}
