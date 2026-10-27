using System.Globalization;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Bank;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Bank;

/// <summary>Een nieuwe eigen bankrekening: de grootboekrekening en de code van het bankdagboek (besluit 76).</summary>
public sealed record NieuweBankrekening(int RekeningId, string Code, string Naam);

public sealed record BankimportResultaat(int Nieuw, int Herkend, int Werkvoorraad);

public sealed record ConceptenGeboekt(int Aantal, IReadOnlyList<string> Fouten);

public sealed record BoekingsregelInvoer(int? Id, string Naam, int RekeningId, BoekingsregelVoorwaarden Voorwaarden, string? Omschrijving, bool IsActief);

/// <summary>
/// Bankimport (CAMT.053) en verwerking: herkende transacties worden conceptposten (besluit 74), de rest blijft in de
/// werkvoorraad (besluit 75). De import begint met een back-up en wordt in één transactie opgeslagen.
/// </summary>
public sealed class BankDienst(IDatabaseSessie sessie, TimeProvider tijd)
{
    private IBoekhoudingRepository Repository => sessie.Boekhouding;

    /// <exception cref="InvalidOperationException">Een nieuwe IBAN zonder gekozen grootboekrekening, of een ongeldige keuze.</exception>
    public async Task<BankimportResultaat> ImporteerAsync(
        AdministratieSoort administratie, BankimportVoorstel voorstel, IReadOnlyDictionary<string, NieuweBankrekening> nieuweRekeningen,
        CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(voorstel);
        ArgumentNullException.ThrowIfNull(nieuweRekeningen);
        foreach (var rekening in voorstel.Rekeningen.Where(r => r.Dagboek is null && !nieuweRekeningen.ContainsKey(r.Iban.Waarde)))
        {
            throw new InvalidOperationException($"Kies een grootboekrekening voor de nieuwe bankrekening {rekening.Iban}.");
        }

        if (voorstel.AantalNieuw == 0 && voorstel.Rekeningen.All(r => r.Afschriften.Count == 0))
        {
            return new BankimportResultaat(0, 0, 0);
        }

        await sessie.MaakBackupAsync(BackupPad("bankimport"), annulering).ConfigureAwait(false);
        return await Repository.PasToeAsync(administratie, stand =>
        {
            var dagboeken = stand.Dagboeken.ToList();
            var bekend = stand.Bankmutaties.Select(m => m.Sleutel).ToHashSet(StringComparer.Ordinal);
            var toevoegen = new List<object>();
            var mutaties = new List<Bankmutatie>();
            foreach (var import in voorstel.Rekeningen)
            {
                var dagboek = dagboeken.FirstOrDefault(d => d.Soort == DagboekSoort.Bank && d.Iban?.Waarde == import.Iban.Waarde);
                if (dagboek is null)
                {
                    var keuze = nieuweRekeningen[import.Iban.Waarde];
                    var rekening = stand.Rekeningen.SingleOrDefault(r => r.Id == keuze.RekeningId)
                        ?? throw new InvalidOperationException("De gekozen grootboekrekening bestaat niet.");
                    if (rekening.Soort != RekeningSoort.Balans)
                    {
                        throw new InvalidOperationException($"Rekening {rekening} is geen balansrekening en kan niet bij een bankrekening horen.");
                    }

                    if (dagboeken.Exists(d => string.Equals(d.Code, keuze.Code.Trim(), StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new InvalidOperationException($"Dagboekcode {keuze.Code.Trim().ToUpperInvariant()} bestaat al.");
                    }

                    dagboek = new Dagboek(administratie, DagboekSoort.Bank, keuze.Code, keuze.Naam, rekening, import.Iban);
                    dagboeken.Add(dagboek);
                    toevoegen.Add(dagboek);
                }

                var opgeslagen = stand.Bankafschriften.Where(a => ReferenceEquals(a.Dagboek, dagboek) || (dagboek.Id != 0 && a.Dagboek.Id == dagboek.Id)).ToList();
                foreach (var afschrift in import.Afschriften.Where(a => !opgeslagen.Exists(o => o.Datum == a.Datum && o.Nummer == a.Nummer)))
                {
                    toevoegen.Add(new Bankafschrift(administratie, dagboek, afschrift.Nummer, afschrift.Datum, afschrift.Beginsaldo, afschrift.Eindsaldo));
                }

                foreach (var nieuw in import.Nieuw.Where(n => bekend.Add(n.Sleutel) && stand.Boekjaar(n.Transactie.Boekdatum.Year) is { IsOpen: true }))
                {
                    var t = nieuw.Transactie;
                    mutaties.Add(new Bankmutatie(administratie, dagboek, nieuw.Sleutel, t.Boekdatum, t.Bedrag, t.IsBij, t.Code,
                        t.TegenIban, t.Tegennaam, t.Omschrijving, t.EndToEndId, t.MandaatId, t.BatchAantal));
                }
            }

            var naImport = stand with { Dagboeken = dagboeken };
            var herkend = 0;
            foreach (var mutatie in mutaties)
            {
                toevoegen.Add(mutatie);
                if (MaakConcept(naImport, mutatie) is { } post)
                {
                    toevoegen.Add(post);
                    herkend++;
                }
            }

            var wijziging = new BoekhoudingWijziging<BankimportResultaat>(new BankimportResultaat(mutaties.Count, herkend, mutaties.Count - herkend));
            wijziging.Toevoegen.AddRange(toevoegen);
            return wijziging;
        }, annulering).ConfigureAwait(false);
    }

    /// <summary>Een transactie uit de werkvoorraad op een rekening zetten: er ontstaat een conceptpost.</summary>
    public Task<Guid> WijsToeAsync(AdministratieSoort administratie, Guid mutatieId, int rekeningId, string? omschrijving, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            var mutatie = Mutatie(stand, mutatieId);
            if (mutatie.PostId is not null)
            {
                throw new InvalidOperationException("Deze transactie is al verwerkt.");
            }

            var rekening = stand.Rekeningen.SingleOrDefault(r => r.Id == rekeningId) ?? throw new InvalidOperationException("De rekening bestaat niet.");
            if (!rekening.IsActief)
            {
                throw new InvalidOperationException($"Rekening {rekening} is inactief.");
            }

            var post = mutatie.MaakPost(rekening, omschrijving);
            mutatie.Koppel(post);
            var wijziging = new BoekhoudingWijziging<Guid>(post.Id);
            wijziging.Toevoegen.Add(post);
            return wijziging;
        }, annulering);

    /// <summary>Boekt de conceptposten van bankdagboeken (Ctrl+B), in volgorde van datum. Posten die niet kloppen blijven concept.</summary>
    public Task<ConceptenGeboekt> BoekConceptenAsync(AdministratieSoort administratie, int? dagboekId, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            var fouten = new List<string>();
            var aantal = 0;
            var concepten = stand.Bankmutaties
                .Where(m => dagboekId is null || m.Dagboek.Id == dagboekId)
                .Select(m => (Mutatie: m, Post: m.PostId is { } id ? stand.Posten.FirstOrDefault(p => p.Id == id) : null))
                .Where(x => x.Post is { IsGeboekt: false })
                .OrderBy(x => x.Post!.Datum).ThenBy(x => x.Mutatie.Sleutel, StringComparer.Ordinal)
                .ToList();
            foreach (var (mutatie, post) in concepten)
            {
                var probleem = stand.Boekjaar(post!.Datum.Year) is not { IsOpen: true } ? $"boekjaar {post.Datum.Year.ToString(CultureInfo.InvariantCulture)} is niet open"
                    : mutatie.ControleerBankregel(post) ?? (post.Controleer() is { Count: > 0 } f ? string.Join(" ", f) : null);
                if (probleem is not null)
                {
                    fouten.Add($"{Weergave(post.Datum)} {post.Omschrijving}: {probleem}");
                    continue;
                }

                post.Boek(BoekhoudingDienst.VolgendNummer(stand, post), tijd.GetUtcNow().UtcDateTime);
                aantal++;
            }

            return new BoekhoudingWijziging<ConceptenGeboekt>(new ConceptenGeboekt(aantal, fouten));
        }, annulering);

    /// <summary>Past de boekingsregels toe op de werkvoorraad. Geeft het aantal nieuwe concepten.</summary>
    public Task<int> PasRegelsToeAsync(AdministratieSoort administratie, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            var posten = stand.Bankmutaties
                .Where(m => m.PostId is null && stand.Boekjaar(m.Boekdatum.Year) is { IsOpen: true })
                .Select(m => MaakConcept(stand, m))
                .OfType<Journaalpost>()
                .ToList();
            var wijziging = new BoekhoudingWijziging<int>(posten.Count);
            wijziging.Toevoegen.AddRange(posten);
            return wijziging;
        }, annulering);

    public Task<int> BewaarRegelAsync(AdministratieSoort administratie, BoekingsregelInvoer invoer, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            ArgumentNullException.ThrowIfNull(invoer);
            var rekening = stand.Rekeningen.SingleOrDefault(r => r.Id == invoer.RekeningId) ?? throw new InvalidOperationException("De rekening bestaat niet.");
            Boekingsregel regel;
            var toevoegen = new List<object>();
            if (invoer.Id is { } id)
            {
                regel = Regel(stand, id);
                regel.Wijzig(invoer.Naam, rekening, invoer.Voorwaarden, invoer.Omschrijving);
            }
            else
            {
                var volgorde = stand.Boekingsregels.Select(r => r.Volgorde).DefaultIfEmpty(0).Max() + 1;
                regel = new Boekingsregel(administratie, invoer.Naam, rekening, invoer.Voorwaarden, invoer.Omschrijving, volgorde);
                toevoegen.Add(regel);
            }

            if (invoer.IsActief)
            {
                regel.Activeer();
            }
            else
            {
                regel.Deactiveer();
            }

            var wijziging = new BoekhoudingWijziging<int>(regel.Id) { WaardeNaOpslaan = () => regel.Id };
            wijziging.Toevoegen.AddRange(toevoegen);
            return wijziging;
        }, annulering);

    public Task VerwijderRegelAsync(AdministratieSoort administratie, int regelId, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            var wijziging = new BoekhoudingWijziging<bool>(true);
            wijziging.Verwijderen.Add(Regel(stand, regelId));
            return wijziging;
        }, annulering);

    /// <summary>Een regel één plaats omhoog (<paramref name="stap"/> = -1) of omlaag (+1) zetten.</summary>
    public Task VerplaatsRegelAsync(AdministratieSoort administratie, int regelId, int stap, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            var regels = stand.Boekingsregels.OrderBy(r => r.Volgorde).ThenBy(r => r.Id).ToList();
            var index = regels.FindIndex(r => r.Id == regelId);
            if (index < 0)
            {
                throw new InvalidOperationException("De boekingsregel bestaat niet meer.");
            }

            var doel = Math.Clamp(index + Math.Sign(stap), 0, regels.Count - 1);
            var regel = regels[index];
            regels.RemoveAt(index);
            regels.Insert(doel, regel);
            for (var i = 0; i < regels.Count; i++)
            {
                regels[i].ZetVolgorde(i + 1);
            }

            return new BoekhoudingWijziging<bool>(true);
        }, annulering);

    private static Journaalpost? MaakConcept(BoekhoudingStand stand, Bankmutatie mutatie)
    {
        if (Bankherkenning.Bepaal(stand, mutatie) is not { } herkenning)
        {
            return null;
        }

        var post = mutatie.MaakPost(herkenning.Rekening, herkenning.Omschrijving);
        mutatie.Koppel(post);
        return post;
    }

    private static Bankmutatie Mutatie(BoekhoudingStand stand, Guid id) =>
        stand.Bankmutaties.SingleOrDefault(m => m.Id == id) ?? throw new InvalidOperationException("De banktransactie bestaat niet meer.");

    private static Boekingsregel Regel(BoekhoudingStand stand, int id) =>
        stand.Boekingsregels.SingleOrDefault(r => r.Id == id) ?? throw new InvalidOperationException("De boekingsregel bestaat niet meer.");

    private static string Weergave(DateOnly datum) => datum.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    private string BackupPad(string reden) =>
        Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(sessie.Pad))!,
            $"{Path.GetFileNameWithoutExtension(sessie.Pad)}-voor-{reden}-{tijd.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.vbak");
}
