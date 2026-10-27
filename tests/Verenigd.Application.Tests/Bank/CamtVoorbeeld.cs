using System.Globalization;
using System.Security;
using System.Text;

namespace Verenigd.Application.Tests.Bank;

/// <summary>Een verzonnen transactie; <paramref name="Centen"/> met teken (positief = bij).</summary>
internal sealed record VbTransactie(
    long Centen, string? Familie = "RCDT", string? Sub = "ESCT", string Eigen = "541", string? Ref = null,
    string? TegenIban = CamtVoorbeeld.Tegen, string? Naam = "Verzonnen BV", string? Omschrijving = "Factuur 1", string? E2e = "E2E-1",
    int? Batch = null, string Status = "BOOK", string? Mandaat = null, string NtryRef = "1", string BatchId = "RUN-1", string? Reden = null);

internal sealed record VbAfschrift(DateOnly Datum, long Begin, params VbTransactie[] Transacties)
{
    public long Eind => Begin + Transacties.Where(t => t.Status == "BOOK").Sum(t => t.Centen);
}

/// <summary>
/// Maakt CAMT.053-bestanden in de opbouw van de Rabobank-export (één afschrift per dag, PRCD/OPBD/CLBD), zonder echte gegevens.
/// </summary>
internal static class CamtVoorbeeld
{
    public const string Betaal = "NL44RABO0123456789";
    public const string Spaar = "NL93RABO0987654321";
    public const string Tegen = "NL20INGB0001234567";

    public static Stream Stroom(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));

    public static string Xml(string iban, string versie, params VbAfschrift[] afschriften)
    {
        var v08 = versie == "08";
        var x = new StringBuilder();
        x.Append(CultureInfo.InvariantCulture, $"""<?xml version="1.0" encoding="UTF-8"?><Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.{versie}"><BkToCstmrStmt><GrpHdr><MsgId>MSG1</MsgId><CreDtTm>2026-10-01T10:00:00</CreDtTm></GrpHdr>""");
        var volgnummer = 100;
        foreach (var a in afschriften)
        {
            volgnummer++;
            x.Append(CultureInfo.InvariantCulture, $"<Stmt><Id>{a.Datum:yyyyMMdd}{volgnummer}</Id><ElctrncSeqNb>{volgnummer}</ElctrncSeqNb><CreDtTm>{a.Datum:yyyy-MM-dd}T23:00:00</CreDtTm>");
            x.Append(CultureInfo.InvariantCulture, $"<Acct><Id><IBAN>{iban}</IBAN></Id><Ccy>EUR</Ccy><Nm>Verzonnen vereniging</Nm></Acct>");
            x.Append(Saldo("PRCD", a.Begin, a.Datum.AddDays(-1))).Append(Saldo("OPBD", a.Begin, a.Datum)).Append(Saldo("CLBD", a.Eind, a.Datum));
            foreach (var t in a.Transacties)
            {
                x.Append(Transactie(t, a.Datum, v08));
            }

            x.Append("</Stmt>");
        }

        x.Append("</BkToCstmrStmt></Document>");
        return x.ToString();
    }

    private static string Bedrag(long centen) => (Math.Abs(centen) / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    private static string Ind(long centen) => centen >= 0 ? "CRDT" : "DBIT";

    private static string Saldo(string type, long centen, DateOnly datum) =>
        string.Create(CultureInfo.InvariantCulture,
            $"""<Bal><Tp><CdOrPrtry><Cd>{type}</Cd></CdOrPrtry></Tp><Amt Ccy="EUR">{Bedrag(centen)}</Amt><CdtDbtInd>{Ind(centen)}</CdtDbtInd><Dt><Dt>{datum:yyyy-MM-dd}</Dt></Dt></Bal>""");

    private static string Transactie(VbTransactie t, DateOnly datum, bool v08)
    {
        var code = (t.Familie is null ? string.Empty : $"<Domn><Cd>PMNT</Cd><Fmly><Cd>{t.Familie}</Cd><SubFmlyCd>{t.Sub}</SubFmlyCd></Fmly></Domn>")
            + $"<Prtry><Cd>{t.Eigen}</Cd><Issr>RABOBANK</Issr></Prtry>";
        var status = v08 ? $"<Sts><Cd>{t.Status}</Cd></Sts>" : $"<Sts>{t.Status}</Sts>";
        var bij = t.Centen >= 0;
        string Partij(string rol, string? naam) => naam is null ? string.Empty : v08 ? $"<{rol}><Pty><Nm>{Esc(naam)}</Nm></Pty></{rol}>" : $"<{rol}><Nm>{Esc(naam)}</Nm></{rol}>";
        var partij = bij ? "Dbtr" : "Cdtr";
        var partijen = t.Batch is not null ? string.Empty
            : "<RltdPties>" + Partij(partij, t.Naam) + (t.TegenIban is null ? string.Empty : $"<{partij}Acct><Id><IBAN>{t.TegenIban}</IBAN></Id></{partij}Acct>") + "</RltdPties>";
        var refs = "<Refs>" + (t.Ref is null ? string.Empty : $"<AcctSvcrRef>{t.Ref}</AcctSvcrRef>") + (t.Mandaat is null ? string.Empty : $"<MndtId>{t.Mandaat}</MndtId>")
            + (t.E2e is null ? string.Empty : $"<EndToEndId>{t.E2e}</EndToEndId>") + "</Refs>";
        var batch = t.Batch is { } n ? string.Create(CultureInfo.InvariantCulture, $"<Btch><PmtInfId>{t.BatchId}</PmtInfId><NbOfTxs>{n}</NbOfTxs></Btch>") : string.Empty;
        var tekst = (t.Omschrijving is null ? string.Empty : $"<RmtInf><Ustrd>{Esc(t.Omschrijving)}</Ustrd></RmtInf>")
            + (t.Reden is null ? string.Empty : $"<RtrInf><Rsn><Cd>{t.Reden}</Cd></Rsn></RtrInf>");
        return string.Create(CultureInfo.InvariantCulture,
            $"""<Ntry><NtryRef>{t.NtryRef}</NtryRef><Amt Ccy="EUR">{Bedrag(t.Centen)}</Amt><CdtDbtInd>{Ind(t.Centen)}</CdtDbtInd>{status}<BookgDt><Dt>{datum:yyyy-MM-dd}</Dt></BookgDt><ValDt><Dt>{datum:yyyy-MM-dd}</Dt></ValDt>{(t.Ref is null ? "" : $"<AcctSvcrRef>{t.Ref}</AcctSvcrRef>")}<BkTxCd>{code}</BkTxCd><NtryDtls>{batch}<TxDtls>{refs}<Amt Ccy="EUR">{Bedrag(t.Centen)}</Amt><CdtDbtInd>{Ind(t.Centen)}</CdtDbtInd>{partijen}{tekst}</TxDtls></NtryDtls></Ntry>""");
    }

    private static string Esc(string tekst) => SecurityElement.Escape(tekst);
}
