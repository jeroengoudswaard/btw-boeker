using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Boekhouding;

/// <summary>Eén rekening onder een regel.</summary>
public sealed record JaarrekeningRekening(Grootboekrekening Rekening, Bedrag Werkelijk, Bedrag VorigJaar);

/// <summary>Een kop, regel of subtotaal; bij een kop zijn de bedragen nul.</summary>
public sealed record JaarrekeningRegel(
    IndelingsregelSoort Soort, string Tekst, Bedrag Werkelijk, Bedrag VorigJaar, IReadOnlyList<JaarrekeningRekening> Rekeningen)
{
    /// <summary>De regel "Niet ingedeeld": rekeningen met bedragen die in geen regel van de indeling vallen.</summary>
    public bool IsNietIngedeeld { get; init; }
}

/// <summary>Activa, passiva, baten of lasten, met het totaal.</summary>
public sealed record JaarrekeningDeel(JaarrekeningAfdeling Afdeling, IReadOnlyList<JaarrekeningRegel> Regels, Bedrag Werkelijk, Bedrag VorigJaar);

/// <summary>Een jaarstuk volgens de indeling (besluit 27), met dit jaar en vorig jaar.</summary>
/// <param name="IsAutomatisch">Er is geen eigen indeling: één regel per rubriek, zoals het rekeningschema.</param>
public sealed record Jaarrekening(
    JaarrekeningRapport Rapport, int Jaar, bool IsAutomatisch, IReadOnlyList<JaarrekeningDeel> Delen,
    IReadOnlyList<Grootboekrekening> NietIngedeeld)
{
    public JaarrekeningDeel Deel(JaarrekeningAfdeling afdeling) => Delen.Single(d => d.Afdeling == afdeling);

    /// <summary>Baten min lasten (positief is een overschot); alleen bij <see cref="JaarrekeningRapport.BatenEnLasten"/>.</summary>
    public long Resultaat(Func<JaarrekeningDeel, Bedrag> kolom)
    {
        ArgumentNullException.ThrowIfNull(kolom);
        return kolom(Deel(JaarrekeningAfdeling.Baten)).Centen - kolom(Deel(JaarrekeningAfdeling.Lasten)).Centen;
    }
}

/// <summary>
/// Rekent een jaarstuk uit volgens de indeling. Er is geen tweede berekening: de bedragen komen uit <see cref="Grootboek"/>
/// (alleen geboekte posten). Alleen lezen; telt nooit mee in het grootboek.
/// </summary>
public static class Jaarrekeningrekenaar
{
    public const string NietIngedeeldTekst = "Niet ingedeeld";
    public const string ResultaatLopendJaarTekst = "Resultaat lopend boekjaar";
    private const string OverigTekst = "Overig";

    public static Jaarrekening Bereken(BoekhoudingStand stand, JaarrekeningRapport rapport, int jaar)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var eigen = stand.Jaarrekeningindelingen.FirstOrDefault(i => i.Rapport == rapport && i.Regels.Count > 0);
        var regels = eigen is null ? Automatisch(stand, rapport) : [.. eigen.Regels.Select(r => new Opzet(r.Afdeling, r.Soort, r.Tekst, r.Bevat))];
        var bedragen = rapport == JaarrekeningRapport.Balans ? Balansbedragen(stand, jaar) : Resultaatbedragen(stand, jaar);

        var delen = new List<JaarrekeningDeel>();
        var nietIngedeeld = new List<Grootboekrekening>();
        foreach (var afdeling in Jaarrekeningindeling.Afdelingen(rapport))
        {
            var rekeningen = bedragen.Where(b => b.Afdeling == afdeling)
                .Select(b => new JaarrekeningRekening(b.Rekening, b.Werkelijk, b.VorigJaar))
                .Where(r => r.Werkelijk != Bedrag.Nul || r.VorigJaar != Bedrag.Nul)
                .OrderBy(r => r.Rekening.Nummer)
                .ToList();
            var deel = regels.Where(r => r.Afdeling == afdeling).ToList();
            var toegewezen = rekeningen.ToLookup(r => deel.FindIndex(o => o.Soort == IndelingsregelSoort.Regel && o.Bevat(r.Rekening.Rubriek)));

            var uitkomst = new List<JaarrekeningRegel>();
            var sindsKop = new List<JaarrekeningRegel>();
            for (var i = 0; i < deel.Count; i++)
            {
                var opzet = deel[i];
                switch (opzet.Soort)
                {
                    case IndelingsregelSoort.Kop:
                        uitkomst.Add(new JaarrekeningRegel(IndelingsregelSoort.Kop, opzet.Tekst, Bedrag.Nul, Bedrag.Nul, []));
                        sindsKop.Clear();
                        break;
                    case IndelingsregelSoort.Subtotaal:
                        uitkomst.Add(Som(IndelingsregelSoort.Subtotaal, opzet.Tekst, sindsKop));
                        break;
                    default:
                        var onder = toegewezen[i].ToList();
                        if (onder.Count > 0)
                        {
                            var regel = Regel(opzet.Tekst, onder);
                            uitkomst.Add(regel);
                            sindsKop.Add(regel);
                        }

                        break;
                }
            }

            var los = toegewezen[-1].ToList();
            if (los.Count > 0)
            {
                uitkomst.Add(Regel(NietIngedeeldTekst, los) with { IsNietIngedeeld = true });
                nietIngedeeld.AddRange(los.Select(r => r.Rekening).Where(r => !nietIngedeeld.Contains(r)));
            }

            if (afdeling == JaarrekeningAfdeling.Passiva)
            {
                var (dit, vorig) = (Grootboek.Balans(stand, jaar).ResultaatLopendJaar, Grootboek.Balans(stand, jaar - 1).ResultaatLopendJaar);
                if (dit != 0 || vorig != 0)
                {
                    uitkomst.Add(new JaarrekeningRegel(IndelingsregelSoort.Regel, ResultaatLopendJaarTekst, new Bedrag(dit), new Bedrag(vorig), []));
                }
            }

            var totaal = Som(IndelingsregelSoort.Subtotaal, Jaarrekeningindeling.Naam(afdeling), uitkomst.Where(r => r.Soort == IndelingsregelSoort.Regel));
            delen.Add(new JaarrekeningDeel(afdeling, uitkomst, totaal.Werkelijk, totaal.VorigJaar));
        }

        return new Jaarrekening(rapport, jaar, eigen is null, delen, [.. nietIngedeeld.OrderBy(r => r.Nummer)]);
    }

    /// <summary>
    /// De indeling zonder eigen keuzes: per deel één regel per rubriek in de volgorde van het rekeningschema, en "Overig"
    /// voor rekeningen zonder rubriek. Dit is ook het begin van de editor.
    /// </summary>
    public static IReadOnlyList<IndelingsregelInvoer> AutomatischeIndeling(BoekhoudingStand stand, JaarrekeningRapport rapport)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var soort = rapport == JaarrekeningRapport.Balans ? RekeningSoort.Balans : RekeningSoort.Resultaat;
        var rubrieken = stand.Rubrieken.Where(r => stand.Rekeningen.Any(g => g.Soort == soort && Grootboek.ZelfdeRubriek(g.Rubriek, r)))
            .OrderBy(r => r.Volgorde).ToList();
        return [.. Jaarrekeningindeling.Afdelingen(rapport).SelectMany(a => rubrieken
            .Where(r => rapport == JaarrekeningRapport.Balans || stand.Rekeningen.Any(g => Grootboek.ZelfdeRubriek(g.Rubriek, r) && g.Soort == soort && Afdeling(g) == a))
            .Select(r => new IndelingsregelInvoer(a, IndelingsregelSoort.Regel, r.Naam, [r])))];
    }

    /// <summary>Het werkelijke bedrag in de richting van de rekening: baten als credit, lasten als debet.</summary>
    public static Bedrag Werkelijk(BoekhoudingStand stand, Grootboekrekening rekening, int jaar, DateOnly? totEnMet = null)
    {
        ArgumentNullException.ThrowIfNull(rekening);
        var saldo = Grootboek.Resultaat(stand, rekening, jaar, totEnMet);
        return new Bedrag(rekening.Kant == ResultaatKant.Baten ? -saldo : saldo);
    }

    private static List<Opzet> Automatisch(BoekhoudingStand stand, JaarrekeningRapport rapport) =>
    [
        .. AutomatischeIndeling(stand, rapport).Select(r => new Opzet(r.Afdeling, r.Soort, r.Tekst, g => r.Rubrieken.Any(x => Grootboek.ZelfdeRubriek(g, x)))),
        .. Jaarrekeningindeling.Afdelingen(rapport).Select(a => new Opzet(a, IndelingsregelSoort.Regel, OverigTekst, g => g is null)),
    ];

    private static JaarrekeningAfdeling Afdeling(Grootboekrekening rekening) =>
        rekening.Kant == ResultaatKant.Baten ? JaarrekeningAfdeling.Baten : JaarrekeningAfdeling.Lasten;

    private static List<Bedragen> Resultaatbedragen(BoekhoudingStand stand, int jaar) =>
        [.. stand.Rekeningen.Where(r => r.Soort == RekeningSoort.Resultaat)
            .Select(r => new Bedragen(r, Afdeling(r), Werkelijk(stand, r, jaar), Werkelijk(stand, r, jaar - 1)))];

    /// <summary>Per rekening en deel; een rekening met een debetsaldo in het ene jaar en een creditsaldo in het andere staat in beide delen.</summary>
    private static List<Bedragen> Balansbedragen(BoekhoudingStand stand, int jaar)
    {
        static IEnumerable<(Grootboekrekening Rekening, JaarrekeningAfdeling Afdeling, Bedrag Bedrag)> Saldi(Balans balans) =>
            balans.Activa.SelectMany(r => r.Rekeningen).Select(r => (r.Rekening, JaarrekeningAfdeling.Activa, r.Bedrag))
                .Concat(balans.Passiva.SelectMany(r => r.Rekeningen).Select(r => (r.Rekening, JaarrekeningAfdeling.Passiva, r.Bedrag)));

        var dit = Saldi(Grootboek.Balans(stand, jaar)).ToList();
        var vorig = Saldi(Grootboek.Balans(stand, jaar - 1)).ToList();
        return [.. stand.Rekeningen.Where(r => r.Soort == RekeningSoort.Balans).SelectMany(r => Jaarrekeningindeling.Afdelingen(JaarrekeningRapport.Balans)
            .Select(a => new Bedragen(r, a,
                dit.Where(s => s.Afdeling == a && Grootboek.ZelfdeRekening(s.Rekening, r)).Select(s => s.Bedrag).Som(),
                vorig.Where(s => s.Afdeling == a && Grootboek.ZelfdeRekening(s.Rekening, r)).Select(s => s.Bedrag).Som())))];
    }

    private static JaarrekeningRegel Regel(string tekst, List<JaarrekeningRekening> rekeningen) =>
        new(IndelingsregelSoort.Regel, tekst, rekeningen.Select(r => r.Werkelijk).Som(), rekeningen.Select(r => r.VorigJaar).Som(), rekeningen);

    private static JaarrekeningRegel Som(IndelingsregelSoort soort, string tekst, IEnumerable<JaarrekeningRegel> regels)
    {
        var lijst = regels.ToList();
        return new JaarrekeningRegel(soort, tekst, lijst.Select(r => r.Werkelijk).Som(), lijst.Select(r => r.VorigJaar).Som(), []);
    }

    private sealed record Opzet(JaarrekeningAfdeling Afdeling, IndelingsregelSoort Soort, string Tekst, Func<Rubriek?, bool> Bevat);

    private sealed record Bedragen(Grootboekrekening Rekening, JaarrekeningAfdeling Afdeling, Bedrag Werkelijk, Bedrag VorigJaar);
}
