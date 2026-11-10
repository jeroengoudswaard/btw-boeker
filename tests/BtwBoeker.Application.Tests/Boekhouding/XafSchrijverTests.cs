using System.Xml.Linq;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;
using BtwBoeker.Domain.Financien.Relaties;

namespace BtwBoeker.Application.Tests.Boekhouding;

/// <summary>De auditfile financieel (XAF 3.2) van één administratie en één boekjaar.</summary>
public class XafSchrijverTests
{
    private static readonly XNamespace X = "http://www.auditfiles.nl/XAF/3.2";
    private static readonly DateOnly Vandaag = new(2026, 11, 4);

    private static XafResultaat Schrijf(BoekhoudingTestdata data, int jaar = 2026) =>
        XafSchrijver.Schrijf(new XafInvoer(data.Stand, jaar, "BTW-Boeker voorbeeld", Vandaag, "0.1.0"));

    private static XElement Lees(XafResultaat resultaat) => XDocument.Parse(System.Text.Encoding.UTF8.GetString(resultaat.Inhoud)).Root!;

    private static string Tekst(XElement e, string naam) => e.Element(X + naam)!.Value;

    [Fact]
    public void Het_bestand_is_geldig_volgens_het_schema_en_heeft_de_juiste_kop()
    {
        var data = new BoekhoudingTestdata();
        data.Geboekt(new DateOnly(2026, 3, 5), "Energie maart", (data.Energie, 4500, 0), (data.Bank, 0, 4500));

        var resultaat = Schrijf(data);

        Assert.Empty(XafSchrijver.Valideer(resultaat.Inhoud));
        var kop = Lees(resultaat).Element(X + "header")!;
        Assert.Equal("2026", Tekst(kop, "fiscalYear"));
        Assert.Equal("2026-01-01", Tekst(kop, "startDate"));
        Assert.Equal("2026-12-31", Tekst(kop, "endDate"));
        Assert.Equal("EUR", Tekst(kop, "curCode"));
        Assert.Equal("2026-11-04", Tekst(kop, "dateCreated"));
        Assert.Equal("BTW-Boeker", Tekst(kop, "softwareDesc"));
        Assert.Equal("0.1.0", Tekst(kop, "softwareVersion"));
        Assert.Equal("BTW-Boeker voorbeeld", Tekst(Lees(resultaat).Element(X + "company")!, "companyName"));
    }

    [Fact]
    public void Alleen_geboekte_posten_van_het_boekjaar_met_kloppende_totalen()
    {
        var data = new BoekhoudingTestdata();
        data.Boekjaren.Add(new Boekjaar(BoekhoudingTestdata.Ver, 2027));
        data.Geboekt(new DateOnly(2026, 3, 5), "Energie maart", (data.Energie, 4500, 0), (data.Bank, 0, 4500));
        data.Geboekt(new DateOnly(2026, 4, 1), "Verhuur", (data.Bank, 12000, 0), (data.Welpen, 0, 12000));
        data.Geboekt(new DateOnly(2027, 1, 3), "Volgend jaar", (data.Energie, 100, 0), (data.Bank, 0, 100));
        var concept = new Journaalpost(BoekhoudingTestdata.Ver, data.Memoriaal, new DateOnly(2026, 5, 1), "Nog niet geboekt");
        concept.Wijzig(data.Memoriaal, concept.Datum, concept.Omschrijving,
            [new RegelInvoer(data.Energie, null, new Bedrag(700), Bedrag.Nul), new RegelInvoer(data.Bank, null, Bedrag.Nul, new Bedrag(700))]);
        data.Posten.Add(concept);

        var resultaat = Schrijf(data);

        Assert.Equal(2, resultaat.AantalPosten);
        Assert.Equal(1, resultaat.OvergeslagenConcepten);
        var transacties = Lees(resultaat).Element(X + "company")!.Element(X + "transactions")!;
        Assert.Equal("4", Tekst(transacties, "linesCount"));
        Assert.Equal("165.00", Tekst(transacties, "totalDebit"));
        Assert.Equal("165.00", Tekst(transacties, "totalCredit"));
        var omschrijvingen = transacties.Descendants(X + "transaction").Select(t => Tekst(t, "desc")).ToList();
        Assert.Equal(["Energie maart", "Verhuur"], omschrijvingen);
        var maart = transacties.Descendants(X + "transaction").First();
        Assert.Equal("3", Tekst(maart, "periodNumber"));
        Assert.Equal("2026-03-05", Tekst(maart, "trDt"));
        Assert.Equal("45.00", Tekst(maart, "amnt"));
        var regel = maart.Elements(X + "trLine").First();
        Assert.Equal("4115", Tekst(regel, "accID"));
        Assert.Equal("D", Tekst(regel, "amntTp"));
        Assert.Equal(Tekst(maart, "nr"), Tekst(regel, "docRef"));
    }

    [Fact]
    public void De_openingsbalans_staat_apart_en_niet_bij_de_transacties()
    {
        var data = new BoekhoudingTestdata();
        data.Geboekt(new DateOnly(2026, 1, 1), "Openingsbalans 2026", PostSoort.Openingsbalans, (data.Bank, 250000, 0), (data.Reserve, 0, 250000));
        data.Geboekt(new DateOnly(2026, 2, 1), "Energie", (data.Energie, 1000, 0), (data.Bank, 0, 1000));

        var resultaat = Schrijf(data);

        Assert.Empty(XafSchrijver.Valideer(resultaat.Inhoud));
        var bedrijf = Lees(resultaat).Element(X + "company")!;
        var opening = bedrijf.Element(X + "openingBalance")!;
        Assert.Equal("2026-01-01", Tekst(opening, "opBalDate"));
        Assert.Equal("2", Tekst(opening, "linesCount"));
        Assert.Equal("2500.00", Tekst(opening, "totalDebit"));
        Assert.Equal("2500.00", Tekst(opening, "totalCredit"));
        Assert.Contains(opening.Elements(X + "obLine"), l => Tekst(l, "accID") == "600" && Tekst(l, "amntTp") == "C");
        Assert.Single(bedrijf.Descendants(X + "transaction"));
    }

    [Fact]
    public void Rekeningschema_dagboeken_en_perioden()
    {
        var data = new BoekhoudingTestdata();
        data.Bankdagboek("BNK", "NL91ABNA0417164300");

        var bedrijf = Lees(Schrijf(data)).Element(X + "company")!;

        var rekeningen = bedrijf.Element(X + "generalLedger")!.Elements(X + "ledgerAccount").ToList();
        Assert.Equal(data.Rekeningen.Count, rekeningen.Count);
        var bank = rekeningen.Single(r => Tekst(r, "accID") == "1100");
        Assert.Equal("Bank", Tekst(bank, "accDesc"));
        Assert.Equal("B", Tekst(bank, "accTp"));
        Assert.Equal("Kas Bank", Tekst(bank, "leadDescription"));
        Assert.Equal("P", Tekst(rekeningen.Single(r => Tekst(r, "accID") == "4115"), "accTp"));

        var dagboeken = bedrijf.Element(X + "transactions")!.Elements(X + "journal").ToList();
        Assert.Equal("M", Tekst(dagboeken.Single(d => Tekst(d, "jrnID") == "MEM"), "jrnTp"));
        var kas = dagboeken.Single(d => Tekst(d, "jrnID") == "KAS");
        Assert.Equal("C", Tekst(kas, "jrnTp"));
        Assert.Equal("1000", Tekst(kas, "offsetAccID"));
        var bankboek = dagboeken.Single(d => Tekst(d, "jrnID") == "BNK");
        Assert.Equal("B", Tekst(bankboek, "jrnTp"));
        Assert.Equal("NL91ABNA0417164300", Tekst(bankboek, "bankAccNr"));

        var perioden = bedrijf.Element(X + "periods")!.Elements(X + "period").ToList();
        Assert.Equal(12, perioden.Count);
        Assert.Equal("2026-02-28", Tekst(perioden[1], "endDatePeriod"));
    }

    [Fact]
    public void Relaties_met_iban_worden_meegenomen()
    {
        var data = new BoekhoudingTestdata();
        var huurder = new Relatie(new RelatieGegevens("Huurder BV", "J. Jansen", "Dorpsstraat 1", "1234 AB", "Ergens", "huur@example.org", "0612345678",
            "NL91ABNA0417164300", IsDebiteur: true, IsCrediteur: false, Notitie: null));
        var leverancier = new Relatie(new RelatieGegevens("Bouwmarkt", null, null, null, null, null, null, null, IsDebiteur: false, IsCrediteur: true, null));
        var beide = new Relatie(new RelatieGegevens("Buurvereniging", null, null, null, null, null, null, null, IsDebiteur: true, IsCrediteur: true, null));
        data.Relaties.AddRange([huurder, leverancier, beide]);
        var resultaat = Schrijf(data);

        Assert.Empty(XafSchrijver.Valideer(resultaat.Inhoud));
        var relaties = Lees(resultaat).Element(X + "company")!.Element(X + "customersSuppliers")!.Elements(X + "customerSupplier").ToList();
        Assert.Equal(3, relaties.Count);
        var r = relaties.Single(e => Tekst(e, "custSupName") == "Huurder BV");
        Assert.Equal("C", Tekst(r, "custSupTp"));
        Assert.Equal("J. Jansen", Tekst(r, "contact"));
        Assert.Equal("huur@example.org", Tekst(r, "eMail"));
        Assert.Equal("Ergens", Tekst(r.Element(X + "streetAddress")!, "city"));
        Assert.Equal("NL91ABNA0417164300", Tekst(r.Element(X + "bankAccount")!, "bankAccNr"));
        Assert.Equal("S", Tekst(relaties.Single(e => Tekst(e, "custSupName") == "Bouwmarkt"), "custSupTp"));
        Assert.Equal("B", Tekst(relaties.Single(e => Tekst(e, "custSupName") == "Buurvereniging"), "custSupTp"));
        Assert.All(relaties, e => Assert.True(Tekst(e, "custSupID").Length <= 35));
    }

    [Fact]
    public void Lange_teksten_worden_ingekort_tot_wat_het_schema_toestaat()
    {
        var data = new BoekhoudingTestdata();
        data.Relaties.Add(new Relatie(new RelatieGegevens(new string('A', 100), new string('C', 80), null, "1234 AB EXTRA LANG", null, null,
            "+31 6 1234 5678 9012 3456 7890 1234", null, true, false, null)));

        var resultaat = Schrijf(data);

        Assert.Empty(XafSchrijver.Valideer(resultaat.Inhoud));
        Assert.Equal(50, Lees(resultaat).Descendants(X + "custSupName").Single().Value.Length);
    }

    [Fact]
    public void De_uitvoer_is_voor_dezelfde_invoer_steeds_gelijk()
    {
        var data = new BoekhoudingTestdata();
        data.Geboekt(new DateOnly(2026, 3, 5), "Energie", (data.Energie, 4500, 0), (data.Bank, 0, 4500));

        Assert.Equal(Schrijf(data).Inhoud, Schrijf(data).Inhoud);
    }
}
