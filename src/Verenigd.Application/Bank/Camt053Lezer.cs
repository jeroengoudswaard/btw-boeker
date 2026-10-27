using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Bank;

namespace Verenigd.Application.Bank;

/// <summary>Eén transactie (<c>Ntry</c>) uit een afschrift.</summary>
public sealed record CamtTransactie(
    string? Bankreferentie, string? Regelreferentie, DateOnly Boekdatum, Bedrag Bedrag, bool IsBij, BankTransactiecode Code,
    string? TegenIban, string? Tegennaam, string? Omschrijving, string? EndToEndId, string? MandaatId, int? BatchAantal,
    string? BatchKenmerk = null, string? Retourreden = null);

/// <summary>Eén (dag)afschrift (<c>Stmt</c>); saldi in centen, positief is een tegoed.</summary>
public sealed record CamtAfschrift(Iban Iban, string Nummer, DateOnly Datum, long Beginsaldo, long Eindsaldo, IReadOnlyList<CamtTransactie> Transacties);

public sealed record CamtBestand(string Versie, IReadOnlyList<CamtAfschrift> Afschriften, IReadOnlyList<string> Meldingen);

/// <summary>
/// Leest CAMT.053-bankafschriften (versie 001.02 en 001.08) van Rabobank en ING (besluit 6). Elementen worden op naam
/// gezocht, zodat beide versies met dezelfde code werken. DTD's en externe entiteiten zijn uitgeschakeld.
/// </summary>
public static class Camt053Lezer
{
    private const string Voorvoegsel = "urn:iso:std:iso:20022:tech:xsd:camt.053.001.";

    /// <exception cref="InvalidOperationException">Het bestand is geen (leesbaar) CAMT.053-afschrift.</exception>
    public static CamtBestand Lees(Stream stroom)
    {
        ArgumentNullException.ThrowIfNull(stroom);
        XDocument document;
        try
        {
            using var lezer = XmlReader.Create(stroom, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            document = XDocument.Load(lezer);
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException("Het bestand is geen leesbaar CAMT.053-bankafschrift (XML).", ex);
        }

        var ns = document.Root?.Name.Namespace.NamespaceName ?? string.Empty;
        if (!ns.StartsWith(Voorvoegsel, StringComparison.Ordinal) || document.Root!.Name.LocalName != "Document")
        {
            throw new InvalidOperationException("Het bestand is geen CAMT.053-bankafschrift.");
        }

        var versie = ns[Voorvoegsel.Length..];
        var meldingen = new List<string>();
        var afschriften = new List<CamtAfschrift>();
        try
        {
            foreach (var stmt in Alle(document.Root, "BkToCstmrStmt").SelectMany(b => Kinderen(b, "Stmt")))
            {
                afschriften.Add(LeesAfschrift(stmt, meldingen));
            }
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"Het CAMT.053-bankafschrift is onvolledig of onjuist: {ex.Message}", ex);
        }

        return new CamtBestand(versie, afschriften, meldingen);
    }

    private static CamtAfschrift LeesAfschrift(XElement stmt, List<string> meldingen)
    {
        var acct = Kind(stmt, "Acct") ?? throw new FormatException("rekening (Acct) ontbreekt");
        if (!Iban.TryMaak(Tekst(acct, "Id", "IBAN"), out var iban))
        {
            throw new FormatException("IBAN van de rekening ontbreekt");
        }

        if (Tekst(acct, "Ccy") is { } valuta && valuta != "EUR")
        {
            throw new FormatException($"alleen rekeningen in euro worden ondersteund (gevonden: {valuta})");
        }

        var saldi = Kinderen(stmt, "Bal").Select(b => (Type: Tekst(b, "Tp", "CdOrPrtry", "Cd"), Bedrag: Saldo(b), Datum: Datum(Kind(b, "Dt")))).ToList();
        var eind = saldi.FirstOrDefault(s => s.Type == "CLBD");
        var begin = saldi.FirstOrDefault(s => s.Type == "OPBD");
        if (begin.Type is null)
        {
            begin = saldi.FirstOrDefault(s => s.Type == "PRCD");
        }

        if (eind.Type is null || begin.Type is null)
        {
            throw new FormatException("begin- of eindsaldo (OPBD/CLBD) ontbreekt");
        }

        var datum = eind.Datum ?? throw new FormatException("datum van het eindsaldo ontbreekt");
        var nummer = Tekst(stmt, "ElctrncSeqNb") ?? Tekst(stmt, "Id") ?? datum.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var transacties = new List<CamtTransactie>();
        foreach (var ntry in Kinderen(stmt, "Ntry"))
        {
            var status = Kind(ntry, "Sts") is { } sts ? (Tekst(sts, "Cd") ?? sts.Value.Trim()) : "BOOK";
            if (status != "BOOK")
            {
                meldingen.Add($"Een transactie op het afschrift van {Weergave(datum)} is niet geboekt (status {status}) en wordt overgeslagen.");
                continue;
            }

            transacties.Add(LeesTransactie(ntry, datum));
        }

        return new CamtAfschrift(iban, nummer, datum, begin.Bedrag, eind.Bedrag, transacties);
    }

    private static CamtTransactie LeesTransactie(XElement ntry, DateOnly afschriftdatum)
    {
        var isBij = Tekst(ntry, "CdtDbtInd") switch
        {
            "CRDT" => true,
            "DBIT" => false,
            var anders => throw new FormatException($"onbekende bij/af-aanduiding '{anders}'"),
        };
        var bedrag = Bedrag(Tekst(ntry, "Amt"));
        var details = Kind(ntry, "NtryDtls");
        var tx = details is null ? [] : Kinderen(details, "TxDtls").ToList();
        var batch = details is null ? null : Kind(details, "Btch");
        int? batchAantal = batch is not null && int.TryParse(Tekst(batch, "NbOfTxs"), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n
            : tx.Count > 1 ? tx.Count
            : null;
        var enkel = tx.Count == 1 ? tx[0] : null;

        string? tegenIban = null, tegennaam = null;
        if (enkel is not null && Kind(enkel, "RltdPties") is { } partijen)
        {
            var rol = isBij ? "Dbtr" : "Cdtr";
            tegenIban = Tekst(partijen, rol + "Acct", "Id", "IBAN");
            tegennaam = Naam(Kind(partijen, rol)) ?? Naam(Kind(partijen, "Ultmt" + rol));
        }

        var omschrijving = enkel is null ? null : Omschrijving(enkel);
        omschrijving ??= Tekst(ntry, "AddtlNtryInf");
        return new CamtTransactie(
            Tekst(ntry, "AcctSvcrRef") ?? (enkel is null ? null : Tekst(enkel, "Refs", "AcctSvcrRef")),
            Tekst(ntry, "NtryRef"),
            Datum(Kind(ntry, "BookgDt")) ?? afschriftdatum,
            bedrag,
            isBij,
            Code(Kind(ntry, "BkTxCd") ?? (enkel is null ? null : Kind(enkel, "BkTxCd"))),
            tegenIban,
            tegennaam,
            omschrijving,
            enkel is null ? null : Leeg(Tekst(enkel, "Refs", "EndToEndId")),
            enkel is null ? null : Tekst(enkel, "Refs", "MndtId"),
            batchAantal,
            batch is null ? null : Tekst(batch, "PmtInfId"),
            enkel is null ? null : Tekst(enkel, "RtrInf", "Rsn", "Cd"));
    }

    private static BankTransactiecode Code(XElement? code)
    {
        if (code is null)
        {
            return default;
        }

        var domein = Kind(code, "Domn");
        var eigen = Kind(code, "Prtry");
        return new BankTransactiecode(
            domein is null ? null : Tekst(domein, "Cd"),
            domein is null ? null : Tekst(domein, "Fmly", "Cd"),
            domein is null ? null : Tekst(domein, "Fmly", "SubFmlyCd"),
            eigen is null ? null : Tekst(eigen, "Cd"),
            eigen is null ? null : Tekst(eigen, "Issr"));
    }

    private static string? Omschrijving(XElement tx)
    {
        var rmt = Kind(tx, "RmtInf");
        if (rmt is null)
        {
            return null;
        }

        var vrij = Kinderen(rmt, "Ustrd").Select(u => u.Value.Trim()).Where(u => u.Length > 0).ToList();
        if (vrij.Count > 0)
        {
            return string.Join(' ', vrij);
        }

        return Kinderen(rmt, "Strd").Select(s => Tekst(s, "CdtrRefInf", "Ref")).FirstOrDefault(r => r is not null);
    }

    /// <summary>Naam in 001.08 onder <c>Pty/Nm</c>, in 001.02 direct onder de partij.</summary>
    private static string? Naam(XElement? partij) => partij is null ? null : Tekst(partij, "Pty", "Nm") ?? Tekst(partij, "Nm");

    private static long Saldo(XElement bal)
    {
        var centen = Bedrag(Tekst(bal, "Amt")).Centen;
        return Tekst(bal, "CdtDbtInd") == "DBIT" ? -centen : centen;
    }

    private static Bedrag Bedrag(string? tekst)
    {
        if (!decimal.TryParse(tekst, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var euro) || decimal.Round(euro, 2) != euro)
        {
            throw new FormatException($"ongeldig bedrag '{tekst}'");
        }

        return new Bedrag((long)(euro * 100));
    }

    private static DateOnly? Datum(XElement? datum)
    {
        var tekst = datum is null ? null : Tekst(datum, "Dt") ?? Tekst(datum, "DtTm");
        if (tekst is null)
        {
            return null;
        }

        return DateOnly.TryParseExact(tekst.Length >= 10 ? tekst[..10] : tekst, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : throw new FormatException($"ongeldige datum '{tekst}'");
    }

    private static string Weergave(DateOnly datum) => datum.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    private static string? Leeg(string? tekst) => string.IsNullOrWhiteSpace(tekst) || tekst == "NOTPROVIDED" ? null : tekst;

    private static IEnumerable<XElement> Kinderen(XElement element, string naam) => element.Elements().Where(e => e.Name.LocalName == naam);

    private static IEnumerable<XElement> Alle(XElement element, string naam) => element.Descendants().Where(e => e.Name.LocalName == naam);

    private static XElement? Kind(XElement element, string naam) => Kinderen(element, naam).FirstOrDefault();

    private static string? Tekst(XElement element, params string[] pad)
    {
        var huidig = element;
        foreach (var stap in pad)
        {
            huidig = Kind(huidig, stap);
            if (huidig is null)
            {
                return null;
            }
        }

        var waarde = huidig.Value.Trim();
        return waarde.Length == 0 ? null : waarde;
    }
}
