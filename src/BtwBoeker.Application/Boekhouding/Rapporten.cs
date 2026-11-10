using System.Globalization;
using System.Text;
using BtwBoeker.Domain.Financien;

namespace BtwBoeker.Application.Boekhouding;

/// <summary>Zet berekende rapporten om naar tabellen voor afdrukken en export.</summary>
public static class Rapporten
{
    public static RapportTabel Saldibalans(Saldibalans balans, string administratie)
    {
        ArgumentNullException.ThrowIfNull(balans);
        var rijen = balans.Regels
            .Select(r => new RapportRij([r.Rekening.ToString(), r.Debet, r.Credit, Saldo(r.Saldo, debet: true), Saldo(r.Saldo, debet: false)], false, false))
            .Append(new RapportRij(["Totaal", balans.TotaalDebet, balans.TotaalCredit, null, null], false, true))
            .ToList();
        return new RapportTabel($"Saldibalans {Jaar(balans.Jaar)}", administratie,
            [new("Rekening", false), new("Debet", true), new("Credit", true), new("Saldo debet", true), new("Saldo credit", true)], rijen);
    }

    public static RapportTabel WinstEnVerlies(WinstEnVerlies wenv, string administratie)
    {
        ArgumentNullException.ThrowIfNull(wenv);
        var rijen = new List<RapportRij>();
        Sectie(rijen, "Baten", wenv.Baten, wenv.TotaalBaten);
        Sectie(rijen, "Lasten", wenv.Lasten, wenv.TotaalLasten);
        rijen.Add(new RapportRij([wenv.Resultaat >= 0 ? "Overschot" : "Tekort", new Bedrag(Math.Abs(wenv.Resultaat))], false, true));
        return new RapportTabel($"Staat van baten en lasten {Jaar(wenv.Jaar)}", administratie, [new("Omschrijving", false), new("Bedrag", true)], rijen);
    }

    public static RapportTabel Balans(Balans balans, string administratie)
    {
        ArgumentNullException.ThrowIfNull(balans);
        var rijen = new List<RapportRij>();
        Sectie(rijen, "Activa", balans.Activa, balans.TotaalActiva);
        var passiva = balans.Passiva.ToList();
        rijen.Add(new RapportRij(["Passiva", null], true, false));
        foreach (var rubriek in passiva)
        {
            Rubriek(rijen, rubriek);
        }

        if (balans.ResultaatLopendJaar != 0)
        {
            rijen.Add(new RapportRij(["Resultaat lopend boekjaar", new Bedrag(balans.ResultaatLopendJaar)], false, false));
        }

        rijen.Add(new RapportRij(["Totaal passiva", balans.TotaalPassiva], false, true));
        return new RapportTabel($"Balans per 31-12-{Jaar(balans.Jaar)}", administratie, [new("Omschrijving", false), new("Bedrag", true)], rijen);
    }

    /// <summary>
    /// Een jaarstuk volgens de eigen indeling: dit jaar en vorig jaar, met of zonder de rekeningen onder elke regel.
    /// Altijd in Expert-termen.
    /// </summary>
    public static RapportTabel Jaarrekening(Jaarrekening jaarrekening, string administratie, bool metRekeningen)
    {
        ArgumentNullException.ThrowIfNull(jaarrekening);
        var balans = jaarrekening.Rapport == Domain.Financien.Boekhouding.JaarrekeningRapport.Balans;
        static object?[] Cellen(string tekst, Bedrag werkelijk, Bedrag vorig, bool leeg = false) =>
            leeg ? [tekst, null, null] : [tekst, werkelijk, vorig];

        var rijen = new List<RapportRij>();
        foreach (var deel in jaarrekening.Delen)
        {
            var naam = Domain.Financien.Boekhouding.Jaarrekeningindeling.Naam(deel.Afdeling);
            rijen.Add(new RapportRij(Cellen(naam, Bedrag.Nul, Bedrag.Nul, leeg: true), true, false));
            foreach (var regel in deel.Regels)
            {
                switch (regel.Soort)
                {
                    case Domain.Financien.Boekhouding.IndelingsregelSoort.Kop:
                        rijen.Add(new RapportRij(Cellen(regel.Tekst, Bedrag.Nul, Bedrag.Nul, leeg: true), true, false));
                        break;
                    case Domain.Financien.Boekhouding.IndelingsregelSoort.Subtotaal:
                        rijen.Add(new RapportRij(Cellen(regel.Tekst, regel.Werkelijk, regel.VorigJaar), false, true));
                        break;
                    default:
                        rijen.Add(new RapportRij(Cellen((regel.IsNietIngedeeld ? "⚠ " : string.Empty) + regel.Tekst, regel.Werkelijk, regel.VorigJaar), false, false));
                        if (metRekeningen)
                        {
                            rijen.AddRange(regel.Rekeningen.Select(r => new RapportRij(Cellen("      " + r.Rekening, r.Werkelijk, r.VorigJaar), false, false)));
                        }

                        break;
                }
            }

            rijen.Add(new RapportRij(Cellen($"Totaal {naam.ToLowerInvariant()}", deel.Werkelijk, deel.VorigJaar), false, true));
        }

        if (!balans)
        {
            rijen.Add(new RapportRij(Cellen("Resultaat (baten min lasten)", new Bedrag(jaarrekening.Resultaat(d => d.Werkelijk)),
                new Bedrag(jaarrekening.Resultaat(d => d.VorigJaar))), false, true));
        }

        var jaar = jaarrekening.Jaar;
        List<RapportKolom> kolommen = balans
            ? [new("Omschrijving", false), new($"31-12-{Jaar(jaar)}", true), new($"31-12-{Jaar(jaar - 1)}", true)]
            : [new("Omschrijving", false), new($"Werkelijk {Jaar(jaar)}", true), new($"Werkelijk {Jaar(jaar - 1)}", true)];
        return new RapportTabel(balans ? $"Balans per 31-12-{Jaar(jaar)}" : $"Staat van baten en lasten {Jaar(jaar)}", administratie, kolommen, rijen);
    }

    public static RapportTabel Kaart(Grootboekkaart kaart, string administratie)
    {
        ArgumentNullException.ThrowIfNull(kaart);
        var rijen = new List<RapportRij> { new(["", "", "Beginsaldo", null, null, new Bedrag(kaart.Beginsaldo)], false, false) };
        rijen.AddRange(kaart.Mutaties.Select(m => new RapportRij(
            [Datum(m.Datum), m.Nummer, m.Omschrijving, m.Debet == Bedrag.Nul ? null : m.Debet, m.Credit == Bedrag.Nul ? null : m.Credit, new Bedrag(m.LopendSaldo)],
            false, false)));
        rijen.Add(new RapportRij(["", "", "Eindsaldo", null, null, new Bedrag(kaart.Eindsaldo)], false, true));
        return new RapportTabel(
            $"Grootboekkaart {kaart.Rekening}", $"{administratie} · {Datum(kaart.Van)} t/m {Datum(kaart.Tot)}",
            [new("Datum", false), new("Nummer", false), new("Omschrijving", false), new("Debet", true), new("Credit", true), new("Saldo", true)],
            rijen);
    }

    private static void Sectie(List<RapportRij> rijen, string kop, IEnumerable<RubriekBedragen> rubrieken, Bedrag totaal)
    {
        rijen.Add(new RapportRij([kop, null], true, false));
        foreach (var rubriek in rubrieken)
        {
            Rubriek(rijen, rubriek);
        }

        rijen.Add(new RapportRij([$"Totaal {kop.ToLowerInvariant()}", totaal], false, true));
    }

    private static void Rubriek(List<RapportRij> rijen, RubriekBedragen rubriek)
    {
        rijen.Add(new RapportRij([rubriek.Rubriek, null], true, false));
        rijen.AddRange(rubriek.Rekeningen.Select(r => new RapportRij(["   " + r.Rekening, r.Bedrag], false, false)));
        rijen.Add(new RapportRij(["   Subtotaal " + rubriek.Rubriek, rubriek.Totaal], false, false));
    }

    private static Bedrag? Saldo(long saldo, bool debet) => debet ? (saldo > 0 ? new Bedrag(saldo) : null) : (saldo < 0 ? new Bedrag(-saldo) : null);

    private static string Jaar(int jaar) => jaar.ToString(CultureInfo.InvariantCulture);

    private static string Datum(DateOnly datum) => datum.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
}

/// <summary>CSV-export (UTF-8 met BOM, <c>;</c>-gescheiden, bedragen als <c>1234,56</c>) die Excel in Nederland direct opent.</summary>
public static class CsvExport
{
    private static readonly CultureInfo Nederlands = CultureInfo.GetCultureInfo("nl-NL");

    public static string Schrijf(RapportTabel tabel)
    {
        ArgumentNullException.ThrowIfNull(tabel);
        var tekst = new StringBuilder("﻿");
        tekst.Append(Regel(tabel.Kolommen.Select(k => k.Naam))).Append("\r\n");
        foreach (var rij in tabel.Rijen)
        {
            tekst.Append(Regel(rij.Cellen.Select(Tekst))).Append("\r\n");
        }

        return tekst.ToString();
    }

    public static string Tekst(object? cel) => cel switch
    {
        null => string.Empty,
        Bedrag b => (b.Centen / 100m).ToString("0.00", Nederlands),
        _ => Convert.ToString(cel, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private static string Regel(IEnumerable<string> velden) =>
        string.Join(';', velden.Select(v => "\"" + v.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""));
}

/// <summary>Schrijft een rapport als Excel-bestand (.xlsx).</summary>
public interface ISpreadsheetSchrijver
{
    void Schrijf(RapportTabel tabel, string pad);
}
