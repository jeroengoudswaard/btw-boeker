using System.Globalization;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Boekhouding;

public sealed record HistorieRegelInvoer(DateOnly Datum, string Omschrijving, Bedrag Debet, Bedrag Credit, HistorischeRegelSoort Soort);

/// <summary>Eén rekening met de regels uit de T-rekening en het totaal volgens het Voorblad.</summary>
public sealed record HistorieRekening(
    int Nummer, string Naam, RekeningSoort Soort, IReadOnlyList<HistorieRegelInvoer> Regels, Bedrag TotaalDebet, Bedrag TotaalCredit, bool OpVoorblad)
{
    public long SaldoRegels => Regels.Sum(r => r.Debet.Centen - r.Credit.Centen);

    public long SaldoVoorblad => TotaalDebet.Centen - TotaalCredit.Centen;

    /// <summary>Het saldo van de regels is gelijk aan het Voorblad (besluit 126: anders een ⚠).</summary>
    public bool Klopt => OpVoorblad && Regels.Count > 0 && SaldoRegels == SaldoVoorblad;
}

public sealed record HistorieVoorstel(int Jaar, IReadOnlyList<HistorieRekening> Rekeningen, IReadOnlyList<string> Meldingen)
{
    public int AantalRegels => Rekeningen.Sum(r => r.Regels.Count);

    public int AantalAfwijkend => Rekeningen.Count(r => r.OpVoorblad && r.Regels.Count > 0 && !r.Klopt);
}

/// <summary>
/// Leest de T-rekeningen uit de Excel-boekhouding (besluiten 122 en 123). Op elk werkblad behalve het Voorblad staan
/// rekeningen naast elkaar: in rij 2 het rekeningnummer met daarnaast naam, "Debet" en "Credit"; daaronder per regel
/// datum, omschrijving, debet en credit. SUM-regels (totalen) worden overgeslagen. Regels zonder datum zijn begin- of
/// tussensaldi; regels zonder datum of met een datum buiten het jaar krijgen 1 januari, met de oorspronkelijke datum
/// in de omschrijving.
/// </summary>
public static class HistorieInlezer
{
    private const int Koprij = 2;

    /// <exception cref="InvalidOperationException">Er is geen (geldig) Voorblad.</exception>
    public static HistorieVoorstel Lees(IReadOnlyList<GenoemdWerkblad> bladen)
    {
        ArgumentNullException.ThrowIfNull(bladen);
        var voorblad = bladen.FirstOrDefault(b => string.Equals(b.Naam, VoorbladInlezer.Werkbladnaam, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Het werkblad Voorblad ontbreekt; dit lijkt geen Excel-boekhouding.");
        var schema = VoorbladInlezer.Lees(voorblad.Blad);
        var jaar = schema.Jaar;
        var meldingen = new List<string>();

        var tRekeningen = new Dictionary<int, (string Naam, List<HistorieRegelInvoer> Regels)>();
        foreach (var blad in bladen.Where(b => !ReferenceEquals(b, voorblad)))
        {
            LeesBlad(blad.Blad, jaar, tRekeningen, meldingen);
        }

        var rekeningen = new List<HistorieRekening>();
        foreach (var rekening in schema.Rekeningen.Where(r => !r.Overslaan))
        {
            var regels = tRekeningen.Remove(rekening.Nummer, out var t) ? t.Regels : [];
            var (debet, credit) = rekening.Soort == RekeningSoort.Balans ? (rekening.Debet, rekening.Credit) : (rekening.WenvDebet, rekening.WenvCredit);
            rekeningen.Add(new HistorieRekening(rekening.Nummer, rekening.Naam, rekening.Soort, regels, debet, credit, true));
        }

        foreach (var (nummer, (naam, regels)) in tRekeningen.OrderBy(t => t.Key))
        {
            if (schema.Rekeningen.Any(r => r.Nummer == nummer))
            {
                continue; // bijvoorbeeld 9990 Memoriaal (besluit 70)
            }

            meldingen.Add($"Rekening {nummer} {naam} staat niet op het Voorblad; kies of u haar overslaat of als inactieve rekening toevoegt.");
            rekeningen.Add(new HistorieRekening(nummer, naam, nummer < 4000 ? RekeningSoort.Balans : RekeningSoort.Resultaat, regels, Bedrag.Nul, Bedrag.Nul, false));
        }

        return new HistorieVoorstel(jaar, [.. rekeningen.OrderBy(r => r.Nummer)], meldingen);
    }

    private static void LeesBlad(Werkblad blad, int jaar, Dictionary<int, (string Naam, List<HistorieRegelInvoer> Regels)> rekeningen, List<string> meldingen)
    {
        var kolommen = blad.Kolommen;
        for (var kolom = 1; kolom + 3 <= kolommen; kolom++)
        {
            if (VoorbladInlezer.Getal(blad[Koprij, kolom]) is not { } nummerwaarde || nummerwaarde is < 1 or > 99999 || nummerwaarde % 1 != 0
                || !string.Equals(VoorbladInlezer.Tekst(blad[Koprij, kolom + 3]), "Credit", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var nummer = (int)nummerwaarde;
            if (!rekeningen.TryGetValue(nummer, out var rekening))
            {
                rekening = (VoorbladInlezer.Tekst(blad[Koprij, kolom + 1]) ?? $"Rekening {nummer}", []);
                rekeningen[nummer] = rekening;
            }

            for (var rij = Koprij + 1; rij <= blad.Rijen; rij++)
            {
                var debet = Bedragcel(blad[rij, kolom + 2]);
                var credit = Bedragcel(blad[rij, kolom + 3]);
                if (debet == Bedrag.Nul && credit == Bedrag.Nul)
                {
                    continue;
                }

                var omschrijving = VoorbladInlezer.Tekst(blad[rij, kolom + 1]) ?? string.Empty;
                var (datum, soort) = blad[rij, kolom]?.Waarde switch
                {
                    DateTime d when d.Year == jaar => (DateOnly.FromDateTime(d), HistorischeRegelSoort.Gewoon),
                    DateTime d => (new DateOnly(jaar, 1, 1), Omschrijf(ref omschrijving, d)),
                    _ => (new DateOnly(jaar, 1, 1), HistorischeRegelSoort.Saldoregel),
                };
                rekening.Regels.Add(new HistorieRegelInvoer(datum, omschrijving, debet, credit, soort));
            }

            kolom += 3;
        }
    }

    private static HistorischeRegelSoort Omschrijf(ref string omschrijving, DateTime datum)
    {
        omschrijving = $"({datum.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}) {omschrijving}".Trim();
        return HistorischeRegelSoort.Gewoon;
    }

    /// <summary>Een bedrag, behalve SUM-formules (totaalregels, die anders dubbel tellen).</summary>
    private static Bedrag Bedragcel(Cel? cel) =>
        cel?.Formule?.Contains("SUM(", StringComparison.OrdinalIgnoreCase) == true ? Bedrag.Nul : VoorbladInlezer.Centen(cel);
}
