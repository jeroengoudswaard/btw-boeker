using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Verenigd.Domain.Financien;

namespace Verenigd.Application.Incasso;

/// <summary>
/// Schrijft een SEPA-incassobestand pain.008.001.08 (besluiten 50 en 57): CORE, altijd RCUR, één batch per run.
/// De uitvoer is voor dezelfde run altijd byte voor byte gelijk.
/// </summary>
public static class Pain008Schrijver
{
    private const string Schemanaam = "pain.008.001.08.xsd";
    private static readonly XNamespace Ns = "urn:iso:std:iso:20022:tech:xsd:pain.008.001.08";
    private static readonly Lazy<XmlSchemaSet> Schema = new(LaadSchema);

    public static byte[] Schrijf(Incassorun run, Incassoinstellingen instellingen)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(instellingen);
        if (run.AantalPosten == 0)
        {
            throw new InvalidOperationException("Een incassobestand zonder posten kan niet worden gemaakt.");
        }

        var aantal = run.AantalPosten.ToString(CultureInfo.InvariantCulture);
        var totaal = run.Totaal.SepaNotatie;
        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Ns + "Document",
                new XElement(Ns + "CstmrDrctDbtInitn",
                    new XElement(Ns + "GrpHdr",
                        E("MsgId", run.Kenmerk),
                        E("CreDtTm", run.DefinitiefUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)),
                        E("NbOfTxs", aantal),
                        E("CtrlSum", totaal),
                        E("InitgPty", E("Nm", instellingen.Naam))),
                    new XElement(Ns + "PmtInf",
                        E("PmtInfId", run.Kenmerk + "-1"),
                        E("PmtMtd", "DD"),
                        E("NbOfTxs", aantal),
                        E("CtrlSum", totaal),
                        E("PmtTpInf",
                            E("SvcLvl", E("Cd", "SEPA")),
                            E("LclInstrm", E("Cd", "CORE")),
                            E("SeqTp", "RCUR")),
                        E("ReqdColltnDt", Datum(run.Uitvoerdatum)),
                        E("Cdtr", E("Nm", instellingen.Naam)),
                        E("CdtrAcct", E("Id", E("IBAN", instellingen.Iban.Waarde))),
                        E("CdtrAgt", NietOpgegevenBank()),
                        E("ChrgBr", "SLEV"),
                        E("CdtrSchmeId", E("Id", E("PrvtId", E("Othr",
                            E("Id", instellingen.IncassantId.Waarde),
                            E("SchmeNm", E("Prtry", "SEPA")))))),
                        run.Posten.Select(Transactie)))));

        using var stroom = new MemoryStream();
        var instellingenXml = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, NewLineChars = "\n" };
        using (var schrijver = XmlWriter.Create(stroom, instellingenXml))
        {
            document.Save(schrijver);
        }

        return stroom.ToArray();
    }

    /// <summary>Controleert het bestand tegen het officiële ISO 20022-schema. Leeg als het bestand geldig is.</summary>
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

    /// <summary>SHA-256 als hexadecimale tekst (64 tekens).</summary>
    public static string Hash(byte[] xml) => Convert.ToHexStringLower(SHA256.HashData(xml));

    private static XElement Transactie(Incassopost post) =>
        E("DrctDbtTxInf",
            E("PmtId", E("EndToEndId", post.EndToEndId)),
            new XElement(Ns + "InstdAmt", new XAttribute("Ccy", "EUR"), post.Bedrag.SepaNotatie),
            E("DrctDbtTx", E("MndtRltdInf",
                E("MndtId", post.Mandaatkenmerk),
                E("DtOfSgntr", Datum(post.OndertekendOp)))),
            E("DbtrAgt", NietOpgegevenBank()),
            E("Dbtr", E("Nm", post.Rekeninghouder)),
            E("DbtrAcct", E("Id", E("IBAN", post.Iban.Waarde))),
            E("RmtInf", E("Ustrd", post.Omschrijving)));

    /// <summary>Binnen de SEPA-zone is de BIC niet verplicht.</summary>
    private static XElement NietOpgegevenBank() => E("FinInstnId", E("Othr", E("Id", "NOTPROVIDED")));

    private static XElement E(string naam, params object[] inhoud) => new(Ns + naam, inhoud);

    private static string Datum(DateOnly datum) => datum.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static XmlSchemaSet LaadSchema()
    {
        using var stroom = typeof(Pain008Schrijver).Assembly.GetManifestResourceStream(Schemanaam)
            ?? throw new InvalidOperationException($"Het schema {Schemanaam} ontbreekt in de applicatie.");
        using var lezer = XmlReader.Create(stroom);
        var set = new XmlSchemaSet();
        set.Add(null, lezer);
        set.Compile();
        return set;
    }
}
