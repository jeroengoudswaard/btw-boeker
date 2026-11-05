using System.Globalization;
using System.Text;
using Verenigd.Domain.Financien;

namespace Verenigd.Application.Boekhouding;

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

    /// <summary>Begroot tegenover werkelijk (besluit 148), altijd in Expert-termen (besluit 131).</summary>
    public static RapportTabel Begroting(Begrotingen.Begrotingsoverzicht overzicht, string administratie)
    {
        ArgumentNullException.ThrowIfNull(overzicht);
        var rijen = new List<RapportRij>();
        foreach (var (kop, rubrieken) in new[] { ("Baten", overzicht.Baten), ("Lasten", overzicht.Lasten) })
        {
            rijen.Add(new RapportRij([kop, null, null, null, null, null], true, false));
            foreach (var rubriek in rubrieken)
            {
                rijen.AddRange(rubriek.Regels.Select(r => new RapportRij(
                    [r.Rekening.ToString(), r.WerkelijkVorigJaar, r.Begroot, r.Werkelijk, r.NaarRato, new Bedrag(r.Verschil)], false, false)));
                rijen.Add(new RapportRij([$"Totaal {rubriek.Naam}", rubriek.Totaal(r => r.WerkelijkVorigJaar), rubriek.Totaal(r => r.Begroot),
                    rubriek.Totaal(r => r.Werkelijk), rubriek.Totaal(r => r.NaarRato), null], false, true));
            }
        }

        var jaar = Jaar(overzicht.Jaar);
        return new RapportTabel($"Begroting en werkelijk {jaar}", administratie,
        [
            new("Rekening", false), new($"Werkelijk {Jaar(overzicht.Jaar - 1)}", true), new($"Begroot {jaar}", true),
            new($"Werkelijk {jaar}", true), new("Naar rato", true), new("Verschil", true),
        ], rijen);
    }

    /// <summary>
    /// De begroting voor de ALV (besluit 155): per rubriek met subtotalen en de kolommen werkelijk twee jaar eerder, begroot
    /// vorig jaar en begroot <paramref name="begrotingsjaar"/>.
    /// </summary>
    public static RapportTabel BegrotingAlv(BoekhoudingStand stand, int begrotingsjaar, string administratie)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var vorige = Begrotingen.Begrotingsrekenaar.Geldend(stand, begrotingsjaar - 1);
        var deze = Begrotingen.Begrotingsrekenaar.Geldend(stand, begrotingsjaar);
        var rekeningen = stand.Rekeningen.Where(r => r.Soort == Domain.Financien.Boekhouding.RekeningSoort.Resultaat)
            .Select(r => (Rekening: r, Werkelijk: Begrotingen.Begrotingsrekenaar.Werkelijk(stand, r, begrotingsjaar - 2),
                Vorig: vorige?.Bedrag(r) ?? Bedrag.Nul, Begroot: deze?.Bedrag(r) ?? Bedrag.Nul))
            .Where(r => r.Werkelijk != Bedrag.Nul || r.Vorig != Bedrag.Nul || r.Begroot != Bedrag.Nul)
            .ToList();
        var rijen = new List<RapportRij>();
        var totalen = new Dictionary<Domain.Financien.Boekhouding.ResultaatKant, Bedrag[]>();
        foreach (var (kop, kant) in new[] { ("Baten", Domain.Financien.Boekhouding.ResultaatKant.Baten), ("Lasten", Domain.Financien.Boekhouding.ResultaatKant.Lasten) })
        {
            var deel = rekeningen.Where(r => (r.Rekening.Kant == Domain.Financien.Boekhouding.ResultaatKant.Baten) == (kant == Domain.Financien.Boekhouding.ResultaatKant.Baten)).ToList();
            rijen.Add(new RapportRij([kop, null, null, null], true, false));
            foreach (var rubriek in deel.GroupBy(r => r.Rekening.Rubriek).OrderBy(g => g.Key?.Volgorde ?? int.MaxValue))
            {
                rijen.AddRange(rubriek.OrderBy(r => r.Rekening.Nummer).Select(r => new RapportRij([r.Rekening.ToString(), r.Werkelijk, r.Vorig, r.Begroot], false, false)));
                rijen.Add(new RapportRij([$"Totaal {rubriek.Key?.Naam ?? "Overig"}", rubriek.Select(r => r.Werkelijk).Som(), rubriek.Select(r => r.Vorig).Som(),
                    rubriek.Select(r => r.Begroot).Som()], false, true));
            }

            totalen[kant] = [deel.Select(r => r.Werkelijk).Som(), deel.Select(r => r.Vorig).Som(), deel.Select(r => r.Begroot).Som()];
            rijen.Add(new RapportRij([$"Totaal {kop.ToLowerInvariant()}", .. totalen[kant].Cast<object?>()], false, true));
        }

        var b = totalen[Domain.Financien.Boekhouding.ResultaatKant.Baten];
        var l = totalen[Domain.Financien.Boekhouding.ResultaatKant.Lasten];
        rijen.Add(new RapportRij(["Resultaat (baten min lasten)", .. Enumerable.Range(0, 3).Select(i => (object?)new Bedrag(b[i].Centen - l[i].Centen))], false, true));
        return new RapportTabel($"Begroting {Jaar(begrotingsjaar)}", administratie,
        [
            new("Omschrijving", false), new($"Werkelijk {Jaar(begrotingsjaar - 2)}", true), new($"Begroot {Jaar(begrotingsjaar - 1)}", true),
            new($"Begroot {Jaar(begrotingsjaar)}", true),
        ], rijen);
    }

    /// <summary>Het meerjarenplan (besluiten 162 en 165): een kolom per jaar, liggend afgedrukt, in Expert-termen.</summary>
    public static RapportTabel Meerjarenplan(Begrotingen.Meerjarenoverzicht overzicht, string administratie)
    {
        ArgumentNullException.ThrowIfNull(overzicht);
        var jaren = overzicht.Jaren;
        var leeg = Enumerable.Repeat<object?>(null, jaren.Count).ToArray();
        var rijen = new List<RapportRij>();
        foreach (var (kop, regels, totaal) in new[]
        {
            ("Baten", overzicht.Baten, (Func<int, Bedrag>)overzicht.TotaalBaten),
            ("Lasten", overzicht.Lasten, (Func<int, Bedrag>)overzicht.TotaalLasten),
        })
        {
            rijen.Add(new RapportRij([kop, .. leeg], true, false));
            rijen.AddRange(regels.Where(r => r.Bedragen.Any(b => b != Bedrag.Nul))
                .Select(r => new RapportRij([r.Rekening.ToString(), .. r.Bedragen.Select(b => b == Bedrag.Nul ? null : (object?)b)], false, false)));
            rijen.Add(new RapportRij([$"Totaal {kop.ToLowerInvariant()}", .. Enumerable.Range(0, jaren.Count).Select(i => (object?)totaal(i))], false, true));
        }

        rijen.Add(new RapportRij(["Resultaat (baten min lasten)", .. Enumerable.Range(0, jaren.Count).Select(i => (object?)new Bedrag(overzicht.Resultaat(i)))], false, true));
        return new RapportTabel($"Meerjarenplan {Jaar(overzicht.Plan.Startjaar)}–{Jaar(overzicht.Plan.Eindjaar)}",
            $"{administratie} · {overzicht.Plan.Omschrijving}",
            [new("Rekening", false), .. jaren.Select(j => new RapportKolom(Jaar(j), true))], rijen)
        { IsLiggend = true };
    }

    /// <summary>
    /// Een jaarstuk volgens de eigen indeling (besluiten 263–268): werkelijk, begroot (alleen bij baten en lasten) en vorig
    /// jaar, met of zonder de rekeningen onder elke regel. Altijd in Expert-termen (besluit 131).
    /// </summary>
    public static RapportTabel Jaarrekening(Jaarrekening jaarrekening, string administratie, bool metRekeningen)
    {
        ArgumentNullException.ThrowIfNull(jaarrekening);
        var balans = jaarrekening.Rapport == Domain.Financien.Boekhouding.JaarrekeningRapport.Balans;
        var begroot = jaarrekening.MetBegroting;
        object?[] Cellen(string tekst, Bedrag werkelijk, Bedrag? begrootBedrag, Bedrag vorig, bool leeg = false) => leeg
            ? begroot ? [tekst, null, null, null] : [tekst, null, null]
            : begroot ? [tekst, werkelijk, begrootBedrag, vorig] : [tekst, werkelijk, vorig];

        var rijen = new List<RapportRij>();
        foreach (var deel in jaarrekening.Delen)
        {
            var naam = Domain.Financien.Boekhouding.Jaarrekeningindeling.Naam(deel.Afdeling);
            rijen.Add(new RapportRij(Cellen(naam, Bedrag.Nul, null, Bedrag.Nul, leeg: true), true, false));
            foreach (var regel in deel.Regels)
            {
                switch (regel.Soort)
                {
                    case Domain.Financien.Boekhouding.IndelingsregelSoort.Kop:
                        rijen.Add(new RapportRij(Cellen(regel.Tekst, Bedrag.Nul, null, Bedrag.Nul, leeg: true), true, false));
                        break;
                    case Domain.Financien.Boekhouding.IndelingsregelSoort.Subtotaal:
                        rijen.Add(new RapportRij(Cellen(regel.Tekst, regel.Werkelijk, regel.Begroot, regel.VorigJaar), false, true));
                        break;
                    default:
                        rijen.Add(new RapportRij(Cellen((regel.IsNietIngedeeld ? "⚠ " : string.Empty) + regel.Tekst, regel.Werkelijk, regel.Begroot, regel.VorigJaar), false, false));
                        if (metRekeningen)
                        {
                            rijen.AddRange(regel.Rekeningen.Select(r => new RapportRij(Cellen("      " + r.Rekening, r.Werkelijk, r.Begroot, r.VorigJaar), false, false)));
                        }

                        break;
                }
            }

            rijen.Add(new RapportRij(Cellen($"Totaal {naam.ToLowerInvariant()}", deel.Werkelijk, deel.Begroot, deel.VorigJaar), false, true));
        }

        if (!balans)
        {
            rijen.Add(new RapportRij(Cellen("Resultaat (baten min lasten)", new Bedrag(jaarrekening.Resultaat(d => d.Werkelijk)),
                begroot ? new Bedrag(jaarrekening.Resultaat(d => d.Begroot)) : null, new Bedrag(jaarrekening.Resultaat(d => d.VorigJaar))), false, true));
        }

        var jaar = jaarrekening.Jaar;
        List<RapportKolom> kolommen = balans
            ? [new("Omschrijving", false), new($"31-12-{Jaar(jaar)}", true), new($"31-12-{Jaar(jaar - 1)}", true)]
            : [new("Omschrijving", false), new($"Werkelijk {Jaar(jaar)}", true), .. begroot ? [new RapportKolom($"Begroot {Jaar(jaar)}", true)] : Array.Empty<RapportKolom>(),
                new($"Werkelijk {Jaar(jaar - 1)}", true)];
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
