using System.Globalization;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Boekhouding;

/// <summary>Eén cel uit een werkblad: de (laatst berekende) waarde en eventueel de formule.</summary>
public sealed record Cel(object? Waarde, string? Formule);

/// <summary>Een werkblad als eenvoudige tabel (rij en kolom tellen vanaf 1), los van het bestandsformaat.</summary>
public sealed class Werkblad
{
    private readonly Dictionary<(int Rij, int Kolom), Cel> _cellen = [];

    public int Rijen { get; private set; }

    public Cel? this[int rij, int kolom] => _cellen.GetValueOrDefault((rij, kolom));

    public void Zet(int rij, int kolom, object? waarde, string? formule = null)
    {
        _cellen[(rij, kolom)] = new Cel(waarde, formule);
        Rijen = Math.Max(Rijen, rij);
    }
}

public interface IWerkbladLezer
{
    /// <exception cref="InvalidOperationException">Het bestand of het werkblad kan niet worden gelezen.</exception>
    Werkblad Lees(string pad, string werkblad);
}

/// <summary>Eén rekening uit het Voorblad, met het eindsaldo als beginbalans van het volgende jaar.</summary>
public sealed record VoorbladRekening(
    int Nummer, string Naam, string? Rubriek, RekeningSoort Soort, RekeningRol Rol, bool Overslaan, Bedrag Debet, Bedrag Credit);

public sealed record VoorbladVoorstel(int Jaar, IReadOnlyList<VoorbladRekening> Rekeningen, IReadOnlyList<string> Meldingen)
{
    public Bedrag TotaalDebet => Rekeningen.Where(r => !r.Overslaan).Select(r => r.Debet).Som();

    public Bedrag TotaalCredit => Rekeningen.Where(r => !r.Overslaan).Select(r => r.Credit).Som();

    public bool IsInEvenwicht => TotaalDebet == TotaalCredit;

    /// <summary>De beginbalans geldt voor het jaar ná het jaar van het Excel-bestand.</summary>
    public int Beginjaar => Jaar + 1;

    /// <summary>Komt in de omschrijving van de openingsbalans, bijvoorbeeld "Openingsbalans 2026 (overgenomen uit Excel)".</summary>
    public string Herkomst { get; init; } = "overgenomen uit Excel";

    /// <summary>Ook het verkoopdagboek VRK aanmaken (standaardinrichting, besluit 118).</summary>
    public bool MetVerkoopboek { get; init; }
}

/// <summary>
/// Leest het tabblad <c>Voorblad</c> van de huidige Excel-boekhouding (besluit 62): kolom A rekeningnummer, B naam,
/// G/H W&amp;V, I/J balans per 31-12; kopregels zonder nummer zijn rubrieken. De totaalregel (SUM) sluit het schema af.
/// </summary>
public static class VoorbladInlezer
{
    public const string Werkbladnaam = "Voorblad";
    private const int KolomNummer = 1, KolomNaam = 2, KolomBeginDebet = 3, KolomWenvDebet = 7, KolomWenvCredit = 8, KolomBalansDebet = 9, KolomBalansCredit = 10;

    public static VoorbladVoorstel Lees(Werkblad blad)
    {
        ArgumentNullException.ThrowIfNull(blad);
        if (Getal(blad[1, KolomNummer]) is not { } jaarwaarde || jaarwaarde is < 2000 or > 2100)
        {
            throw new InvalidOperationException("Dit lijkt geen Voorblad van de boekhouding: cel A1 bevat geen boekjaar.");
        }

        var jaar = (int)jaarwaarde;
        var rekeningen = new List<VoorbladRekening>();
        var meldingen = new List<string>();
        string? rubriek = null;
        var reserveGevonden = false;

        for (var rij = 2; rij <= blad.Rijen; rij++)
        {
            if (blad[rij, KolomBeginDebet]?.Formule?.StartsWith("=SUM(", StringComparison.OrdinalIgnoreCase) == true
                || blad[rij, KolomNummer]?.Waarde is string)
            {
                break; // totaalregel of het begin van de jaarrekening
            }

            var naam = Tekst(blad[rij, KolomNaam]);
            if (Getal(blad[rij, KolomNummer]) is not { } nummerwaarde)
            {
                if (naam is not null)
                {
                    rubriek = naam;
                }

                continue;
            }

            var nummer = (int)nummerwaarde;
            if (naam is null)
            {
                meldingen.Add($"Rekening {nummer} heeft geen naam en is overgeslagen.");
                continue;
            }

            var heeftBalans = HeeftInhoud(blad[rij, KolomBalansDebet]) || HeeftInhoud(blad[rij, KolomBalansCredit]);
            var heeftWenv = HeeftInhoud(blad[rij, KolomWenvDebet]) || HeeftInhoud(blad[rij, KolomWenvCredit]);
            var soort = heeftBalans ? RekeningSoort.Balans
                : heeftWenv ? RekeningSoort.Resultaat
                : nummer < 4000 ? RekeningSoort.Balans : RekeningSoort.Resultaat;

            var rol = RekeningRol.Geen;
            if (!reserveGevonden && naam.Contains("reserve", StringComparison.OrdinalIgnoreCase))
            {
                rol = RekeningRol.Reserve;
                reserveGevonden = true;
            }
            else if (naam.Equals("Kruisposten", StringComparison.OrdinalIgnoreCase))
            {
                rol = RekeningRol.Kruisposten;
            }
            else if (naam.Equals("Kas", StringComparison.OrdinalIgnoreCase))
            {
                rol = RekeningRol.Kas;
            }

            var debet = soort == RekeningSoort.Balans ? Centen(blad[rij, KolomBalansDebet]) : Bedrag.Nul;
            var credit = soort == RekeningSoort.Balans ? Centen(blad[rij, KolomBalansCredit]) : Bedrag.Nul;
            var overslaan = naam.Equals("Memoriaal", StringComparison.OrdinalIgnoreCase);
            if (overslaan)
            {
                // Besluit 70: memoriaal is in Verenigd een dagboek, geen rekening.
                var saldo = Centen(blad[rij, KolomBalansDebet]).Centen - Centen(blad[rij, KolomBalansCredit]).Centen;
                if (saldo != 0)
                {
                    meldingen.Add($"Rekening {nummer} Memoriaal heeft een saldo van {new Bedrag(Math.Abs(saldo))}; boek dat eerst over.");
                }
            }

            rekeningen.Add(new VoorbladRekening(nummer, naam, rubriek, soort, rol, overslaan, debet, credit));
        }

        if (rekeningen.Count == 0)
        {
            throw new InvalidOperationException("Er zijn geen rekeningen gevonden (kolom A rekeningnummer, kolom B naam).");
        }

        var voorstel = new VoorbladVoorstel(jaar, rekeningen, meldingen);
        if (!voorstel.IsInEvenwicht)
        {
            meldingen.Add(
                $"De beginbalans is niet in evenwicht: debet {voorstel.TotaalDebet}, credit {voorstel.TotaalCredit} " +
                $"(verschil {new Bedrag(Math.Abs(voorstel.TotaalDebet.Centen - voorstel.TotaalCredit.Centen))}).");
        }

        return voorstel;
    }

    private static bool HeeftInhoud(Cel? cel) =>
        cel is not null && (!string.IsNullOrWhiteSpace(cel.Formule) || cel.Waarde switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            _ => true,
        });

    private static double? Getal(Cel? cel) => cel?.Waarde switch
    {
        double d => d,
        int i => i,
        long l => l,
        decimal m => (double)m,
        _ => null,
    };

    private static string? Tekst(Cel? cel) => cel?.Waarde is string s && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

    private static Bedrag Centen(Cel? cel) =>
        Getal(cel) is { } waarde ? new Bedrag((long)Math.Round((decimal)waarde * 100m, MidpointRounding.AwayFromZero)) : Bedrag.Nul;

    internal static string Jaar(int jaar) => jaar.ToString(CultureInfo.InvariantCulture);
}
