using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Application.Debiteuren;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Domain.Financien.Debiteuren;
using Verenigd.Domain.Financien.Facturen;
using Verenigd.Domain.Financien.Relaties;

namespace Verenigd.Application.Facturen;

public sealed record RelatieInvoer(Guid? Id, RelatieGegevens Gegevens, bool IsActief);

public sealed record FactuurregelInvoerId(string Omschrijving, decimal Aantal, Bedrag Prijs, int RekeningId);

public sealed record FactuurInvoer(Guid? Id, Guid RelatieId, DateOnly Datum, int Termijn, IReadOnlyList<FactuurregelInvoerId> Regels);

public sealed record FactuurinstellingenInvoer(
    string? Naam, string? Straat, string? Postcode, string? Plaats, string? Kvk, string? Iban, string? Email, string? Telefoon, int Termijn,
    string? Voettekst);

/// <summary>
/// Relaties en verkoopfacturen (stap 4c-2, besluiten 66 en 105–112): concepten bewaren, definitief maken (boekt
/// 1310 Debiteuren overig aan de opbrengst in dagboek VRK), crediteren en betalingen afletteren.
/// </summary>
public sealed class FactuurDienst(IDatabaseSessie sessie, TimeProvider tijd)
{
    private IBoekhoudingRepository Repository => sessie.Boekhouding;

    public Task<BoekhoudingStand> LaadAsync(AdministratieSoort administratie, CancellationToken annulering = default) =>
        Repository.LaadAsync(administratie, annulering);

    /// <exception cref="ArgumentException">Ongeldige gegevens (zie <see cref="Relatie.Wijzig"/>).</exception>
    public Task<Guid> BewaarRelatieAsync(RelatieInvoer invoer, CancellationToken annulering = default) =>
        Repository.PasToeAsync(AdministratieSoort.Vereniging, stand =>
        {
            ArgumentNullException.ThrowIfNull(invoer);
            var wijziging = new BoekhoudingWijziging<Guid>(Guid.Empty);
            Relatie relatie;
            if (invoer.Id is { } id)
            {
                relatie = stand.Relaties.SingleOrDefault(r => r.Id == id) ?? throw new InvalidOperationException("De relatie bestaat niet meer.");
                relatie.Wijzig(invoer.Gegevens);
            }
            else
            {
                relatie = new Relatie(invoer.Gegevens);
                wijziging.Toevoegen.Add(relatie);
            }

            if (invoer.IsActief)
            {
                relatie.Activeer();
            }
            else
            {
                relatie.Deactiveer();
            }

            return new BoekhoudingWijziging<Guid>(relatie.Id).Met(wijziging);
        }, annulering);

    public Task<Guid> BewaarConceptAsync(AdministratieSoort administratie, FactuurInvoer invoer, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            ArgumentNullException.ThrowIfNull(invoer);
            var relatie = Relatie(stand, invoer.RelatieId);
            var regels = invoer.Regels.Select(r => new FactuurregelInvoer(r.Omschrijving, r.Aantal, r.Prijs,
                stand.Rekeningen.SingleOrDefault(g => g.Id == r.RekeningId) ?? throw new InvalidOperationException("Een rekening bestaat niet."))).ToList();
            var wijziging = new BoekhoudingWijziging<Guid>(Guid.Empty);
            Verkoopfactuur factuur;
            if (invoer.Id is { } id)
            {
                factuur = Factuur(stand, id);
                factuur.Wijzig(relatie, invoer.Datum, invoer.Termijn, regels);
            }
            else
            {
                factuur = new Verkoopfactuur(administratie, relatie, invoer.Datum, invoer.Termijn);
                factuur.Wijzig(relatie, invoer.Datum, invoer.Termijn, regels);
                wijziging.Toevoegen.Add(factuur);
            }

            return new BoekhoudingWijziging<Guid>(factuur.Id).Met(wijziging);
        }, annulering);

    public Task VerwijderConceptAsync(AdministratieSoort administratie, Guid factuurId, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            var factuur = Factuur(stand, factuurId);
            if (factuur.IsDefinitief)
            {
                throw new InvalidOperationException("Een definitieve factuur kan niet worden verwijderd; maak een creditfactuur.");
            }

            var wijziging = new BoekhoudingWijziging<bool>(true);
            wijziging.Verwijderen.Add(factuur);
            return wijziging;
        }, annulering);

    /// <summary>Geeft het factuurnummer, bijvoorbeeld <c>V2026-001</c>.</summary>
    /// <exception cref="InvalidOperationException">De factuur klopt niet, het boekjaar is niet open of de relatie is inactief.</exception>
    public Task<string> MaakDefinitiefAsync(AdministratieSoort administratie, Guid factuurId, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            var factuur = Factuur(stand, factuurId);
            var relatie = Relatie(stand, factuur.RelatieId);
            if (stand.Boekjaar(factuur.Factuurdatum.Year) is not { IsOpen: true })
            {
                throw new InvalidOperationException($"Boekjaar {factuur.Factuurdatum.Year} is niet open.");
            }

            if (factuur.Controleer() is { Count: > 0 } fouten)
            {
                throw new InvalidOperationException(string.Join(" ", fouten));
            }

            var origineel = factuur.CreditVan is { } creditVan ? Factuur(stand, creditVan) : null;
            if (origineel is not null && stand.Verkoopfacturen.Any(f => f.CreditVan == origineel.Id && f.IsDefinitief))
            {
                throw new InvalidOperationException($"Factuur {origineel.Nummer} is al gecrediteerd.");
            }

            var toevoegen = new List<object>();
            var debiteuren = Debiteurenrekeningen.Overige(stand, administratie, toevoegen);
            var dagboek = stand.Dagboeken.FirstOrDefault(d => d.Soort == DagboekSoort.Verkoop);
            if (dagboek is null)
            {
                dagboek = new Dagboek(administratie, DagboekSoort.Verkoop, "VRK", "Verkoop", null);
                toevoegen.Add(dagboek);
            }

            var volgnummer = stand.Verkoopfacturen
                .Where(f => f.Administratie == administratie && f.Jaar == factuur.Factuurdatum.Year)
                .Select(f => f.Volgnummer ?? 0).DefaultIfEmpty(0).Max() + 1;
            var totaal = factuur.Totaal.Centen;
            var regels = new List<RegelInvoer> { Regel(debiteuren, totaal) };
            regels.AddRange(factuur.Regels.GroupBy(r => r.Rekening).OrderBy(g => g.Key.Nummer).Select(g => Regel(g.Key, -g.Sum(r => r.Bedrag.Centen))));
            var nummer = $"{Verkoopfactuur.Voorvoegsel(administratie)}{factuur.Factuurdatum.Year}-{volgnummer:D3}";
            var omschrijving = $"{(factuur.IsCreditfactuur ? "Creditfactuur" : "Factuur")} {nummer}: {relatie.Naam}";
            var post = new Journaalpost(administratie, dagboek, factuur.Factuurdatum, omschrijving.Length > Journaalpost.MaximaleOmschrijvingLengte ? omschrijving[..Journaalpost.MaximaleOmschrijvingLengte] : omschrijving);
            post.Wijzig(dagboek, post.Datum, post.Omschrijving, regels.Where(r => r.Debet != Bedrag.Nul || r.Credit != Bedrag.Nul));
            post.Boek(BoekhoudingDienst.VolgendNummer(stand with { Dagboeken = [.. stand.Dagboeken, dagboek] }, post), tijd.GetUtcNow().UtcDateTime);
            factuur.MaakDefinitief(volgnummer, relatie, post.Id);
            toevoegen.Add(post);
            toevoegen.Add(origineel is null
                ? new Debiteurmutatie(administratie, nummer, relatie.Id, null, DebiteurmutatieSoort.Vordering, factuur.Factuurdatum, totaal, post.Id, null)
                : new Debiteurmutatie(administratie, origineel.Nummer!, relatie.Id, null, DebiteurmutatieSoort.Creditering, factuur.Factuurdatum, totaal, post.Id, null));
            var wijziging = new BoekhoudingWijziging<string>(nummer);
            wijziging.Toevoegen.AddRange(toevoegen);
            return wijziging;
        }, annulering);

    /// <summary>Maakt een concept-creditfactuur voor een definitieve factuur; geeft het Id.</summary>
    public Task<Guid> MaakCreditfactuurAsync(AdministratieSoort administratie, Guid factuurId, DateOnly datum, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            var factuur = Factuur(stand, factuurId);
            if (stand.Verkoopfacturen.Any(f => f.CreditVan == factuur.Id))
            {
                throw new InvalidOperationException($"Voor factuur {factuur.Nummer} bestaat al een creditfactuur.");
            }

            var credit = factuur.MaakCreditfactuur(datum);
            var wijziging = new BoekhoudingWijziging<Guid>(credit.Id);
            wijziging.Toevoegen.Add(credit);
            return wijziging;
        }, annulering);

    /// <summary>Een ontvangst uit de werkvoorraad afletteren met een of meer open facturen; de som moet precies kloppen.</summary>
    public Task<Guid> LetterAfAsync(AdministratieSoort administratie, Guid mutatieId, IReadOnlyList<Guid> factuurIds, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            ArgumentNullException.ThrowIfNull(factuurIds);
            var mutatie = stand.Bankmutaties.SingleOrDefault(m => m.Id == mutatieId) ?? throw new InvalidOperationException("De banktransactie bestaat niet meer.");
            if (mutatie.PostId is not null || !mutatie.IsBij)
            {
                throw new InvalidOperationException("Alleen een ontvangst uit de werkvoorraad kan worden afgeletterd.");
            }

            var open = Factuuroverzicht.Bereken(stand, DateOnly.MinValue).Where(f => f.IsOpen && factuurIds.Contains(f.Factuur.Id)).ToList();
            if (open.Count != factuurIds.Distinct().Count())
            {
                throw new InvalidOperationException("Een gekozen factuur staat niet (meer) open.");
            }

            var som = open.Sum(f => f.Saldo);
            if (som != mutatie.Bedrag.Centen)
            {
                throw new InvalidOperationException($"De gekozen facturen ({new Bedrag(som)}) zijn samen niet gelijk aan het ontvangen bedrag ({mutatie.Bedrag}).");
            }

            var toevoegen = new List<object>();
            var debiteuren = Debiteurenrekeningen.Overige(stand, administratie, toevoegen);
            var omschrijving = "Betaling " + string.Join(", ", open.Select(f => f.Factuur.Nummer));
            var post = mutatie.MaakPost(debiteuren, omschrijving.Length > Journaalpost.MaximaleOmschrijvingLengte ? omschrijving[..Journaalpost.MaximaleOmschrijvingLengte] : omschrijving);
            mutatie.Koppel(post);
            toevoegen.Add(post);
            toevoegen.AddRange(open.Select(f => new Debiteurmutatie(
                administratie, f.Factuur.Nummer!, f.Factuur.RelatieId, null, DebiteurmutatieSoort.Betaling, mutatie.Boekdatum, -f.Saldo, post.Id, null)));
            var wijziging = new BoekhoudingWijziging<Guid>(post.Id);
            wijziging.Toevoegen.AddRange(toevoegen);
            return wijziging;
        }, annulering);

    /// <param name="logo">PNG of JPG; <c>null</c> laat het logo ongewijzigd, een lege reeks wist het.</param>
    public Task BewaarInstellingenAsync(
        AdministratieSoort administratie, FactuurinstellingenInvoer invoer, byte[]? logo = null, CancellationToken annulering = default) =>
        Repository.PasToeAsync(administratie, stand =>
        {
            ArgumentNullException.ThrowIfNull(invoer);
            var wijziging = new BoekhoudingWijziging<bool>(true);
            var instellingen = stand.Factuurinstellingen;
            if (instellingen is null)
            {
                instellingen = new Factuurinstellingen(administratie);
                wijziging.Toevoegen.Add(instellingen);
            }

            instellingen.Wijzig(invoer.Naam, invoer.Straat, invoer.Postcode, invoer.Plaats, invoer.Kvk, invoer.Iban, invoer.Email, invoer.Telefoon,
                invoer.Termijn, invoer.Voettekst);
            if (logo is not null)
            {
                instellingen.ZetLogo(logo.Length == 0 ? null : logo);
            }

            return wijziging;
        }, annulering);

    private static RegelInvoer Regel(Grootboekrekening rekening, long saldo) =>
        new(rekening, null, saldo > 0 ? new Bedrag(saldo) : Bedrag.Nul, saldo < 0 ? new Bedrag(-saldo) : Bedrag.Nul);

    private static Relatie Relatie(BoekhoudingStand stand, Guid id) =>
        stand.Relaties.SingleOrDefault(r => r.Id == id) ?? throw new InvalidOperationException("De relatie bestaat niet.");

    private static Verkoopfactuur Factuur(BoekhoudingStand stand, Guid id) =>
        stand.Verkoopfacturen.SingleOrDefault(f => f.Id == id) ?? throw new InvalidOperationException("De factuur bestaat niet meer.");
}

internal static class WijzigingExtensies
{
    /// <summary>Neemt de toe te voegen en te verwijderen objecten van <paramref name="bron"/> over.</summary>
    public static BoekhoudingWijziging<T> Met<T, TBron>(this BoekhoudingWijziging<T> doel, BoekhoudingWijziging<TBron> bron)
    {
        doel.Toevoegen.AddRange(bron.Toevoegen);
        doel.Verwijderen.AddRange(bron.Verwijderen);
        return doel;
    }
}
