using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Domain.Financien.Relaties;

namespace Verenigd.Application.Boekhouding;

/// <summary>Alles wat in één auditfile komt: één administratie, één boekjaar (besluiten 197 en 255–259).</summary>
/// <param name="Stand">De boekhouding van de administratie (rekeningen, dagboeken, posten, relaties, facturen, debiteurmutaties).</param>
/// <param name="Betalers">De betalers van de contributie (alleen bij de Vereniging), als debiteuren met IBAN (besluit 256).</param>
public sealed record XafInvoer(
    BoekhoudingStand Stand, int Jaar, string Bedrijfsnaam, IReadOnlyList<Betaler> Betalers, DateOnly Aangemaakt, string Softwareversie);

/// <param name="Inhoud">Het XML-bestand (UTF-8), geldig volgens XAF 3.2.</param>
/// <param name="AantalPosten">Geboekte posten in de transacties (zonder de openingsbalans).</param>
/// <param name="OvergeslagenConcepten">Conceptposten van het boekjaar die niet zijn meegenomen (besluit 259).</param>
public sealed record XafResultaat(byte[] Inhoud, int AantalPosten, int OvergeslagenConcepten);

/// <summary>
/// Schrijft een XML Auditfile Financieel 3.2 (XAF), zodat een groep kan overstappen naar een ander pakket of de administratie
/// aan een accountant kan geven (besluit 197). Alleen geboekte posten; de openingsbalans apart; relaties en betalers als
/// debiteuren en crediteuren met IBAN (besluit 256); omschrijvingen zoals geboekt (besluit 257). Elk bestand wordt vóór
/// het teruggeven gecontroleerd tegen het officiële schema; een ongeldig bestand komt er nooit uit.
/// </summary>
public static class XafSchrijver
{
    private const string Schemanaam = "XmlAuditfileFinancieel3.2.xsd";
    private static readonly XNamespace Ns = "http://www.auditfiles.nl/XAF/3.2";
    private static readonly Lazy<XmlSchemaSet> Schema = new(LaadSchema);

    /// <summary>Het kenmerk van een relatie in de auditfile (een Guid zonder streepjes past in de 35 tekens).</summary>
    public static string RelatieId(Guid id) => "R" + id.ToString("N", CultureInfo.InvariantCulture);

    /// <summary>Het kenmerk van een betaler in de auditfile.</summary>
    public static string BetalerId(Guid id) => "B" + id.ToString("N", CultureInfo.InvariantCulture);

    public static XafResultaat Schrijf(XafInvoer invoer)
    {
        ArgumentNullException.ThrowIfNull(invoer);
        var stand = invoer.Stand;
        var boekjaar = stand.Boekjaren.FirstOrDefault(b => b.Jaar == invoer.Jaar)
            ?? throw new InvalidOperationException($"Het boekjaar {invoer.Jaar} bestaat niet in deze administratie.");
        if (boekjaar.IsHistorisch)
        {
            throw new InvalidOperationException(
                $"Boekjaar {invoer.Jaar} is een historisch boekjaar uit Excel zonder echte journaalposten en kan niet als auditfile worden geëxporteerd.");
        }

        var vanHetJaar = stand.Posten.Where(p => p.Jaar == invoer.Jaar).ToList();
        var geboekt = vanHetJaar.Where(p => p.IsGeboekt).ToList();
        var opening = geboekt.Where(p => p.Soort == PostSoort.Openingsbalans).ToList();
        var transacties = geboekt.Where(p => p.Soort != PostSoort.Openingsbalans).OrderBy(p => p.Datum).ThenBy(p => p.Volgnummer).ToList();
        var relatieBijPost = RelatiesBijPost(stand);

        var bedrijf = new XElement(Ns + "company",
            Optioneel("companyIdent", stand.Factuurinstellingen?.Kvk, 35),
            E("companyName", Kort(invoer.Bedrijfsnaam, 999)),
            E("taxRegistrationCountry", "NL"),
            E("taxRegIdent", string.Empty),
            Adres("streetAddress", stand.Factuurinstellingen?.Straat, stand.Factuurinstellingen?.Postcode, stand.Factuurinstellingen?.Plaats),
            Relaties(stand.Relaties, invoer.Betalers),
            Grootboek(stand.Rekeningen),
            Perioden(invoer.Jaar),
            Openingsbalans(invoer.Jaar, opening),
            Transacties(stand.Dagboeken, transacties, relatieBijPost));

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Ns + "auditfile",
                new XElement(Ns + "header",
                    E("fiscalYear", invoer.Jaar.ToString(CultureInfo.InvariantCulture)),
                    E("startDate", Datum(new DateOnly(invoer.Jaar, 1, 1))),
                    E("endDate", Datum(new DateOnly(invoer.Jaar, 12, 31))),
                    E("curCode", "EUR"),
                    E("dateCreated", Datum(invoer.Aangemaakt)),
                    E("softwareDesc", "Verenigd"),
                    E("softwareVersion", Kort(invoer.Softwareversie, 20))),
                bedrijf));

        var inhoud = NaarBytes(document);
        var fouten = Valideer(inhoud);
        if (fouten.Count > 0)
        {
            throw new InvalidOperationException("De auditfile voldoet niet aan XAF 3.2: " + string.Join(" ", fouten));
        }

        return new XafResultaat(inhoud, transacties.Count, vanHetJaar.Count(p => !p.IsGeboekt));
    }

    /// <summary>Controleert een bestand tegen het schema van XAF 3.2; een lege lijst betekent geldig.</summary>
    public static IReadOnlyList<string> Valideer(byte[] xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var fouten = new List<string>();
        try
        {
            using var stroom = new MemoryStream(xml);
            var document = XDocument.Load(stroom);
            document.Validate(Schema.Value, (_, e) => fouten.Add(e.Message));
        }
        catch (XmlException ex)
        {
            fouten.Add(ex.Message);
        }

        return fouten;
    }

    // ---- Onderdelen -------------------------------------------------------------------------

    private static XElement? Relaties(IReadOnlyList<Relatie> relaties, IReadOnlyList<Betaler> betalers)
    {
        if (relaties.Count == 0 && betalers.Count == 0)
        {
            return null;
        }

        return new XElement(Ns + "customersSuppliers",
            relaties.OrderBy(r => r.Naam, StringComparer.CurrentCulture).Select(r => new XElement(Ns + "customerSupplier",
                E("custSupID", RelatieId(r.Id)),
                E("custSupName", Kort(r.Naam, 50)),
                Optioneel("contact", r.Contactpersoon, 50),
                Optioneel("telephone", r.Telefoon, 30),
                Optioneel("eMail", r.Email, 999),
                E("custSupTp", r.IsDebiteur && r.IsCrediteur ? "B" : r.IsDebiteur ? "C" : r.IsCrediteur ? "S" : "O"),
                Adres("streetAddress", r.Straat, r.Postcode, r.Plaats),
                Rekening(r.Iban))),
            betalers.OrderBy(b => b.Rekeninghouder ?? b.Iban.Waarde, StringComparer.CurrentCulture).Select(b => new XElement(Ns + "customerSupplier",
                E("custSupID", BetalerId(b.Id)),
                Optioneel("custSupName", b.Rekeninghouder, 50),
                E("custSupTp", "C"),
                Rekening(b.Iban.Waarde))));
    }

    private static XElement? Rekening(string? iban) =>
        string.IsNullOrWhiteSpace(iban) ? null : new XElement(Ns + "bankAccount", E("bankAccNr", Kort(iban.Replace(" ", string.Empty, StringComparison.Ordinal), 35)));

    private static XElement Grootboek(IReadOnlyList<Grootboekrekening> rekeningen) =>
        new(Ns + "generalLedger",
            rekeningen.OrderBy(r => r.Nummer).Select(r => new XElement(Ns + "ledgerAccount",
                E("accID", Nummer(r)),
                E("accDesc", Kort(r.Naam, 999)),
                E("accTp", r.Soort == RekeningSoort.Balans ? "B" : "P"),
                r.Rubriek is null ? null : E("leadDescription", Kort(r.Rubriek.Naam, 999)))));

    private static XElement Perioden(int jaar) =>
        new(Ns + "periods",
            Enumerable.Range(1, 12).Select(maand => new XElement(Ns + "period",
                E("periodNumber", maand.ToString(CultureInfo.InvariantCulture)),
                E("periodDesc", new DateOnly(jaar, maand, 1).ToString("MMMM yyyy", CultureInfo.GetCultureInfo("nl-NL"))),
                E("startDatePeriod", Datum(new DateOnly(jaar, maand, 1))),
                E("endDatePeriod", Datum(new DateOnly(jaar, maand, DateTime.DaysInMonth(jaar, maand)))))));

    /// <summary>De openingsbalans per rekening, opgeteld over de openingsposten van het jaar.</summary>
    private static XElement? Openingsbalans(int jaar, List<Journaalpost> opening)
    {
        if (opening.Count == 0)
        {
            return null;
        }

        var saldi = opening.SelectMany(p => p.Regels)
            .GroupBy(r => r.Rekening.Nummer)
            .Select(g => (Rekening: g.First().Rekening, Saldo: g.Sum(r => r.Saldo)))
            .Where(s => s.Saldo != 0)
            .OrderBy(s => s.Rekening.Nummer)
            .ToList();
        return new XElement(Ns + "openingBalance",
            E("opBalDate", Datum(new DateOnly(jaar, 1, 1))),
            E("opBalDesc", $"Openingsbalans {jaar.ToString(CultureInfo.InvariantCulture)}"),
            E("linesCount", saldi.Count.ToString(CultureInfo.InvariantCulture)),
            E("totalDebit", Bedrag(saldi.Where(s => s.Saldo > 0).Sum(s => s.Saldo))),
            E("totalCredit", Bedrag(-saldi.Where(s => s.Saldo < 0).Sum(s => s.Saldo))),
            saldi.Select((s, i) => new XElement(Ns + "obLine",
                E("nr", (i + 1).ToString(CultureInfo.InvariantCulture)),
                E("accID", Nummer(s.Rekening)),
                E("amnt", Bedrag(Math.Abs(s.Saldo))),
                E("amntTp", s.Saldo > 0 ? "D" : "C"))));
    }

    private static XElement Transacties(IReadOnlyList<Dagboek> dagboeken, IReadOnlyList<Journaalpost> posten, IReadOnlyDictionary<Guid, string> relatieBijPost)
    {
        var regels = posten.SelectMany(p => p.Regels).ToList();
        var geldrekeningen = dagboeken.Where(d => d.Rekening is not null).Select(d => d.Rekening!.Nummer).ToHashSet();
        return new XElement(Ns + "transactions",
            E("linesCount", regels.Count.ToString(CultureInfo.InvariantCulture)),
            E("totalDebit", Bedrag(regels.Sum(r => r.Debet.Centen))),
            E("totalCredit", Bedrag(regels.Sum(r => r.Credit.Centen))),
            dagboeken.OrderBy(d => d.Code, StringComparer.Ordinal).Select(d => new XElement(Ns + "journal",
                E("jrnID", d.Code),
                E("desc", d.Naam),
                E("jrnTp", d.Soort switch
                {
                    DagboekSoort.Kas => "C",
                    DagboekSoort.Bank => "B",
                    DagboekSoort.Inkoop => "P",
                    DagboekSoort.Verkoop => "S",
                    _ => "M",
                }),
                d.Rekening is null ? null : E("offsetAccID", Nummer(d.Rekening)),
                d.Iban is { } iban ? E("bankAccNr", iban.Waarde) : null,
                posten.Where(p => p.Dagboek.Code == d.Code).Select(p => Transactie(p, relatieBijPost.GetValueOrDefault(p.Id), geldrekeningen)))));
    }

    private static XElement Transactie(Journaalpost post, string? relatie, HashSet<int> geldrekeningen) =>
        new(Ns + "transaction",
            E("nr", post.Nummer),
            E("desc", post.Omschrijving),
            E("periodNumber", post.Datum.Month.ToString(CultureInfo.InvariantCulture)),
            E("trDt", Datum(post.Datum)),
            E("amnt", Bedrag(post.TotaalDebet.Centen)),
            E("amntTp", "D"),
            post.Regels.Select((r, i) => new XElement(Ns + "trLine",
                E("nr", (i + 1).ToString(CultureInfo.InvariantCulture)),
                E("accID", Nummer(r.Rekening)),
                E("docRef", post.Nummer),
                E("effDate", Datum(post.Datum)),
                E("desc", r.Omschrijving ?? post.Omschrijving),
                E("amnt", Bedrag(r.Debet.Centen > 0 ? r.Debet.Centen : r.Credit.Centen)),
                E("amntTp", r.Debet.Centen > 0 ? "D" : "C"),
                relatie is not null && IsDebiteurregel(r, geldrekeningen) ? E("custSupID", relatie) : null)));

    /// <summary>
    /// De regel die de vordering of schuld draagt: een balansrekening die geen geldrekening (bank of kas) van een dagboek is
    /// (bijv. 1310 bij een factuur of een bankontvangst, 1300 bij een stornering).
    /// </summary>
    private static bool IsDebiteurregel(Journaalregel regel, HashSet<int> geldrekeningen) =>
        regel.Rekening.Soort == RekeningSoort.Balans && !geldrekeningen.Contains(regel.Rekening.Nummer);

    /// <summary>
    /// Per post de relatie of betaler waar hij bij hoort: een verkoopfactuur (relatie), of debiteurmutaties van precies één
    /// betaler. Een incassorun boekt het totaal van alle betalers op één regel en krijgt daarom geen relatie.
    /// </summary>
    private static Dictionary<Guid, string> RelatiesBijPost(BoekhoudingStand stand)
    {
        var bijPost = new Dictionary<Guid, string>();
        foreach (var factuur in stand.Verkoopfacturen.Where(f => f.PostId is not null))
        {
            bijPost[factuur.PostId!.Value] = RelatieId(factuur.RelatieId);
        }

        foreach (var groep in stand.Debiteurmutaties.GroupBy(m => m.PostId))
        {
            var betalers = groep.Select(m => m.BetalerId).Distinct().ToList();
            if (betalers.Count == 1)
            {
                bijPost.TryAdd(groep.Key, BetalerId(betalers[0]));
            }
        }

        return bijPost;
    }

    // ---- Hulpjes ----------------------------------------------------------------------------

    private static XElement? Adres(string naam, string? straat, string? postcode, string? plaats) =>
        string.IsNullOrWhiteSpace(straat) && string.IsNullOrWhiteSpace(postcode) && string.IsNullOrWhiteSpace(plaats)
            ? null
            : new XElement(Ns + naam,
                Optioneel("streetname", straat, 999),
                Optioneel("city", plaats, 50),
                Optioneel("postalCode", postcode, 10));

    private static XElement? Optioneel(string naam, string? waarde, int maximum) =>
        string.IsNullOrWhiteSpace(waarde) ? null : E(naam, Kort(waarde.Trim(), maximum));

    private static string Kort(string waarde, int maximum) => waarde.Length <= maximum ? waarde : waarde[..maximum];

    private static string Nummer(Grootboekrekening rekening) => rekening.Nummer.ToString(CultureInfo.InvariantCulture);

    private static string Bedrag(long centen) => (centen / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    private static string Datum(DateOnly datum) => datum.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static XElement E(string naam, params object?[] inhoud) => new(Ns + naam, inhoud);

    private static byte[] NaarBytes(XDocument document)
    {
        using var stroom = new MemoryStream();
        using (var schrijver = XmlWriter.Create(stroom, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
        {
            document.Save(schrijver);
        }

        return stroom.ToArray();
    }

    private static XmlSchemaSet LaadSchema()
    {
        using var stroom = typeof(XafSchrijver).Assembly.GetManifestResourceStream(Schemanaam)
            ?? throw new InvalidOperationException($"Het schema {Schemanaam} ontbreekt in de applicatie.");
        using var lezer = XmlReader.Create(stroom);
        var set = new XmlSchemaSet();
        set.Add(null, lezer);
        set.Compile();
        return set;
    }
}
