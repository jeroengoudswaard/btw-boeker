using System.Globalization;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Bank;

public sealed record NieuweMutatie(string Sleutel, CamtTransactie Transactie);

/// <summary>Wat een import voor één eigen rekening oplevert. <see cref="Dagboek"/> is <c>null</c> voor een nieuwe IBAN.</summary>
public sealed record BankimportRekening(
    Iban Iban, Dagboek? Dagboek, IReadOnlyList<CamtAfschrift> Afschriften, IReadOnlyList<NieuweMutatie> Nieuw, int AlBekend,
    int BuitenBoekjaar, IReadOnlyList<string> Meldingen)
{
    /// <summary>Al ingelezen transacties; de import vult daarmee velden aan die eerder nog niet werden bewaard.</summary>
    public IReadOnlyList<NieuweMutatie> Bekend { get; init; } = [];

    public DateOnly? Van => Afschriften.Count == 0 ? null : Afschriften.Min(a => a.Datum);

    public DateOnly? Tot => Afschriften.Count == 0 ? null : Afschriften.Max(a => a.Datum);
}

public sealed record BankimportVoorstel(IReadOnlyList<BankimportRekening> Rekeningen, IReadOnlyList<string> Meldingen)
{
    public int AantalNieuw => Rekeningen.Sum(r => r.Nieuw.Count);
}

/// <summary>
/// Bepaalt wat een of meer CAMT.053-bestanden toevoegen: nooit een transactie dubbel, afschriften maar één keer, en
/// controles op de aansluiting van saldi. Een pure functie; er wordt niets opgeslagen.
/// </summary>
public static class BankimportPlanner
{
    public static BankimportVoorstel Plan(BoekhoudingStand stand, IEnumerable<CamtBestand> bestanden)
    {
        ArgumentNullException.ThrowIfNull(stand);
        ArgumentNullException.ThrowIfNull(bestanden);
        var lijst = bestanden.ToList();
        var meldingen = lijst.SelectMany(b => b.Meldingen).Distinct().ToList();
        var bekend = stand.Bankmutaties.Select(m => m.Sleutel).ToHashSet(StringComparer.Ordinal);
        var rekeningen = new List<BankimportRekening>();
        foreach (var groep in lijst.SelectMany(b => b.Afschriften).GroupBy(a => a.Iban.Waarde, StringComparer.Ordinal))
        {
            var iban = groep.First().Iban;
            var dagboek = stand.Dagboeken.FirstOrDefault(d => d.Soort == DagboekSoort.Bank && d.Iban?.Waarde == iban.Waarde);
            var opgeslagen = dagboek is null ? [] : stand.Bankafschriften.Where(a => a.Dagboek.Id == dagboek.Id || ReferenceEquals(a.Dagboek, dagboek)).ToList();
            var afschriften = groep
                .GroupBy(a => (a.Datum, a.Nummer))
                .Select(g => g.First())
                .Where(a => !opgeslagen.Exists(o => o.Datum == a.Datum && o.Nummer == a.Nummer))
                .OrderBy(a => a.Datum)
                .ToList();

            var nieuw = new List<NieuweMutatie>();
            var bekendeTransacties = new List<NieuweMutatie>();
            var gezien = new HashSet<string>(StringComparer.Ordinal);
            int alBekend = 0, buiten = 0;
            foreach (var transactie in groep.SelectMany(a => a.Transacties))
            {
                var sleutel = Sleutel(iban, transactie);
                if (!gezien.Add(sleutel))
                {
                    continue;
                }

                if (bekend.Contains(sleutel))
                {
                    alBekend++;
                    bekendeTransacties.Add(new NieuweMutatie(sleutel, transactie));
                }
                else if (stand.Boekjaar(transactie.Boekdatum.Year) is not { IsOpen: true })
                {
                    buiten++;
                }
                else
                {
                    nieuw.Add(new NieuweMutatie(sleutel, transactie));
                }
            }

            var rekeningmeldingen = new List<string>();
            if (buiten > 0)
            {
                rekeningmeldingen.Add(string.Create(CultureInfo.CurrentCulture,
                    $"{buiten} {(buiten == 1 ? "transactie valt" : "transacties vallen")} buiten een open boekjaar en {(buiten == 1 ? "wordt" : "worden")} overgeslagen."));
            }

            rekeningmeldingen.AddRange(Aansluiting(afschriften, opgeslagen));
            rekeningen.Add(new BankimportRekening(iban, dagboek, afschriften, nieuw, alBekend, buiten, rekeningmeldingen) { Bekend = bekendeTransacties });
        }

        return new BankimportVoorstel(rekeningen, meldingen);
    }

    /// <summary>Het kenmerk dat een transactie uniek maakt: de bankreferentie, of anders datum, bedrag en regelkenmerken.</summary>
    public static string Sleutel(Iban iban, CamtTransactie transactie)
    {
        ArgumentNullException.ThrowIfNull(transactie);
        if (transactie.Bankreferentie is { } referentie)
        {
            return $"{iban.Waarde}|R|{referentie}";
        }

        var saldo = transactie.IsBij ? transactie.Bedrag.Centen : -transactie.Bedrag.Centen;
        return string.Create(CultureInfo.InvariantCulture,
            $"{iban.Waarde}|D|{transactie.Boekdatum:yyyyMMdd}|{saldo}|{transactie.Regelreferentie}|{transactie.EndToEndId}");
    }

    /// <summary>
    /// Besluit 80: het beginsaldo van het eerste afschrift in een boekjaar moet gelijk zijn aan het grootboeksaldo van
    /// <paramref name="rekening"/> op dat moment (openingsbalans plus eerder geboekte posten). Alleen als er nog geen
    /// eerdere afschriften van deze rekening zijn ingelezen.
    /// </summary>
    /// <returns>Een waarschuwing, of <c>null</c> als het aansluit of niet te controleren is.</returns>
    public static string? ControleerBeginsaldo(BoekhoudingStand stand, Grootboekrekening rekening, BankimportRekening import)
    {
        ArgumentNullException.ThrowIfNull(stand);
        ArgumentNullException.ThrowIfNull(rekening);
        ArgumentNullException.ThrowIfNull(import);
        if (import.Dagboek is { } dagboek && stand.Bankafschriften.Any(a => a.Dagboek.Id == dagboek.Id || ReferenceEquals(a.Dagboek, dagboek)))
        {
            return null;
        }

        var eerste = import.Afschriften.FirstOrDefault(a => stand.Boekjaar(a.Datum.Year) is not null);
        if (eerste is null)
        {
            return null;
        }

        var grootboek = Grootboek.GeboektePosten(stand, eerste.Datum.Year)
            .Where(p => p.Datum < eerste.Datum || p.Soort == PostSoort.Openingsbalans)
            .SelectMany(p => p.Regels)
            .Where(r => Grootboek.ZelfdeRekening(r.Rekening, rekening))
            .Sum(r => r.Saldo);
        return grootboek == eerste.Beginsaldo
            ? null
            : $"Het beginsaldo van de bank op {Weergave(eerste.Datum)} ({new Bedrag(eerste.Beginsaldo)}) is niet gelijk aan het saldo van {rekening} in het grootboek ({new Bedrag(grootboek)}).";
    }

    private static IEnumerable<string> Aansluiting(IReadOnlyList<CamtAfschrift> nieuw, IReadOnlyList<Domain.Financien.Bank.Bankafschrift> opgeslagen)
    {
        var tijdlijn = opgeslagen.Select(o => (o.Datum, o.Beginsaldo, o.Eindsaldo, IsNieuw: false))
            .Concat(nieuw.Select(n => (n.Datum, n.Beginsaldo, n.Eindsaldo, IsNieuw: true)))
            .OrderBy(a => a.Datum)
            .ToList();
        for (var i = 1; i < tijdlijn.Count; i++)
        {
            var (vorige, huidige) = (tijdlijn[i - 1], tijdlijn[i]);
            if ((vorige.IsNieuw || huidige.IsNieuw) && vorige.Eindsaldo != huidige.Beginsaldo)
            {
                yield return $"Tussen {Weergave(vorige.Datum)} en {Weergave(huidige.Datum)} ontbreekt een afschrift: het beginsaldo {new Bedrag(huidige.Beginsaldo)} sluit niet aan op het eindsaldo {new Bedrag(vorige.Eindsaldo)}.";
            }
        }

        foreach (var afschrift in nieuw)
        {
            var som = afschrift.Transacties.Sum(t => t.IsBij ? t.Bedrag.Centen : -t.Bedrag.Centen);
            if (afschrift.Beginsaldo + som != afschrift.Eindsaldo)
            {
                yield return $"Op het afschrift van {Weergave(afschrift.Datum)} sluiten de transacties niet aan op begin- en eindsaldo.";
            }
        }
    }

    private static string Weergave(DateOnly datum) => datum.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
}
