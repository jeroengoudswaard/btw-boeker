using BtwBoeker.Application.Begrotingen;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Begrotingen;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Boekhouding;

/// <summary>Eén rekening onder een regel. <c>Begroot</c> is <c>null</c> als er voor het jaar niets begroot is.</summary>
public sealed record JaarrekeningRekening(Grootboekrekening Rekening, Bedrag Werkelijk, Bedrag? Begroot, Bedrag VorigJaar);

/// <summary>Een kop, regel of subtotaal; bij een kop zijn de bedragen nul.</summary>
public sealed record JaarrekeningRegel(
    IndelingsregelSoort Soort, string Tekst, Bedrag Werkelijk, Bedrag? Begroot, Bedrag VorigJaar, IReadOnlyList<JaarrekeningRekening> Rekeningen)
{
    /// <summary>De regel "Niet ingedeeld": rekeningen met bedragen die in geen regel van de indeling vallen.</summary>
    public bool IsNietIngedeeld { get; init; }
}

/// <summary>Activa, passiva, baten of lasten, met het totaal.</summary>
public sealed record JaarrekeningDeel(JaarrekeningAfdeling Afdeling, IReadOnlyList<JaarrekeningRegel> Regels, Bedrag Werkelijk, Bedrag? Begroot, Bedrag VorigJaar);

/// <summary>Een jaarstuk volgens de indeling (besluiten 263–268), met werkelijk, begroot en vorig jaar.</summary>
/// <param name="MetBegroting">Alleen bij baten en lasten, en alleen als er voor het jaar een begroting of meerjarenplan is.</param>
/// <param name="IsAutomatisch">Er is geen eigen indeling: één regel per rubriek, zoals het rekeningschema.</param>
public sealed record Jaarrekening(
    JaarrekeningRapport Rapport, int Jaar, bool MetBegroting, bool IsAutomatisch, IReadOnlyList<JaarrekeningDeel> Delen,
    IReadOnlyList<Grootboekrekening> NietIngedeeld)
{
    public JaarrekeningDeel Deel(JaarrekeningAfdeling afdeling) => Delen.Single(d => d.Afdeling == afdeling);

    /// <summary>Baten min lasten (positief is een overschot); alleen bij <see cref="JaarrekeningRapport.BatenEnLasten"/>.</summary>
    public long Resultaat(Func<JaarrekeningDeel, Bedrag?> kolom)
    {
        ArgumentNullException.ThrowIfNull(kolom);
        return (kolom(Deel(JaarrekeningAfdeling.Baten)) ?? Bedrag.Nul).Centen - (kolom(Deel(JaarrekeningAfdeling.Lasten)) ?? Bedrag.Nul).Centen;
    }
}

/// <summary>
/// Rekent een jaarstuk uit volgens de indeling. Er is geen tweede berekening: de bedragen komen uit <see cref="Grootboek"/>
/// (alleen geboekte posten, besluit 71; een historisch jaar uit het Voorblad, besluit 122) en de begroting of het
/// meerjarenplan (besluiten 149, 164 en 266). Alleen lezen; telt nooit mee in het grootboek.
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
        var metBegroting = rapport == JaarrekeningRapport.BatenEnLasten && Begrotingsbron(stand, jaar) is not null;

        var delen = new List<JaarrekeningDeel>();
        var nietIngedeeld = new List<Grootboekrekening>();
        foreach (var afdeling in Jaarrekeningindeling.Afdelingen(rapport))
        {
            var rekeningen = bedragen.Where(b => b.Afdeling == afdeling)
                .Select(b => new JaarrekeningRekening(b.Rekening, b.Werkelijk, metBegroting ? b.Begroot : null, b.VorigJaar))
                .Where(r => r.Werkelijk != Bedrag.Nul || r.VorigJaar != Bedrag.Nul || (r.Begroot is { } begroot && begroot != Bedrag.Nul))
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
                        uitkomst.Add(new JaarrekeningRegel(IndelingsregelSoort.Kop, opzet.Tekst, Bedrag.Nul, null, Bedrag.Nul, []));
                        sindsKop.Clear();
                        break;
                    case IndelingsregelSoort.Subtotaal:
                        uitkomst.Add(Som(IndelingsregelSoort.Subtotaal, opzet.Tekst, sindsKop, metBegroting));
                        break;
                    default:
                        var onder = toegewezen[i].ToList();
                        if (onder.Count > 0)
                        {
                            var regel = Regel(opzet.Tekst, onder, metBegroting);
                            uitkomst.Add(regel);
                            sindsKop.Add(regel);
                        }

                        break;
                }
            }

            var los = toegewezen[-1].ToList();
            if (los.Count > 0)
            {
                uitkomst.Add(Regel(NietIngedeeldTekst, los, metBegroting) with { IsNietIngedeeld = true });
                nietIngedeeld.AddRange(los.Select(r => r.Rekening).Where(r => !nietIngedeeld.Contains(r)));
            }

            if (afdeling == JaarrekeningAfdeling.Passiva)
            {
                var (dit, vorig) = (Grootboek.Balans(stand, jaar).ResultaatLopendJaar, Grootboek.Balans(stand, jaar - 1).ResultaatLopendJaar);
                if (dit != 0 || vorig != 0)
                {
                    uitkomst.Add(new JaarrekeningRegel(IndelingsregelSoort.Regel, ResultaatLopendJaarTekst, new Bedrag(dit), null, new Bedrag(vorig), []));
                }
            }

            var totaal = Som(IndelingsregelSoort.Subtotaal, Jaarrekeningindeling.Naam(afdeling), uitkomst.Where(r => r.Soort == IndelingsregelSoort.Regel), metBegroting);
            delen.Add(new JaarrekeningDeel(afdeling, uitkomst, totaal.Werkelijk, totaal.Begroot, totaal.VorigJaar));
        }

        return new Jaarrekening(rapport, jaar, metBegroting, eigen is null, delen, [.. nietIngedeeld.OrderBy(r => r.Nummer)]);
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

    /// <summary>De geldende begroting van het jaar, anders het meerjarenplan waarin het jaar valt (besluit 266).</summary>
    public static Func<Grootboekrekening, Bedrag>? Begrotingsbron(BoekhoudingStand stand, int jaar)
    {
        ArgumentNullException.ThrowIfNull(stand);
        if (Begrotingsrekenaar.Geldend(stand, jaar) is { } begroting)
        {
            return begroting.Bedrag;
        }

        var plan = stand.Meerjarenplannen.Where(p => p.Jaren.Contains(jaar)).Select(p => p.Startjaar).Distinct()
            .OrderByDescending(s => s).Select(s => Meerjarenrekenaar.Geldend(stand, s)).FirstOrDefault(p => p is not null);
        return plan is null ? null : r => plan.Bedrag(r, jaar);
    }

    private static List<Opzet> Automatisch(BoekhoudingStand stand, JaarrekeningRapport rapport) =>
    [
        .. AutomatischeIndeling(stand, rapport).Select(r => new Opzet(r.Afdeling, r.Soort, r.Tekst, g => r.Rubrieken.Any(x => Grootboek.ZelfdeRubriek(g, x)))),
        .. Jaarrekeningindeling.Afdelingen(rapport).Select(a => new Opzet(a, IndelingsregelSoort.Regel, OverigTekst, g => g is null)),
    ];

    private static JaarrekeningAfdeling Afdeling(Grootboekrekening rekening) =>
        rekening.Kant == ResultaatKant.Baten ? JaarrekeningAfdeling.Baten : JaarrekeningAfdeling.Lasten;

    private static List<Bedragen> Resultaatbedragen(BoekhoudingStand stand, int jaar)
    {
        var bron = Begrotingsbron(stand, jaar);
        return [.. stand.Rekeningen.Where(r => r.Soort == RekeningSoort.Resultaat).Select(r => new Bedragen(r, Afdeling(r),
            Begrotingsrekenaar.Werkelijk(stand, r, jaar), bron?.Invoke(r) ?? Bedrag.Nul, Begrotingsrekenaar.Werkelijk(stand, r, jaar - 1)))];
    }

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
                Bedrag.Nul,
                vorig.Where(s => s.Afdeling == a && Grootboek.ZelfdeRekening(s.Rekening, r)).Select(s => s.Bedrag).Som())))];
    }

    private static JaarrekeningRegel Regel(string tekst, List<JaarrekeningRekening> rekeningen, bool metBegroting) =>
        new(IndelingsregelSoort.Regel, tekst, rekeningen.Select(r => r.Werkelijk).Som(),
            metBegroting ? rekeningen.Select(r => r.Begroot ?? Bedrag.Nul).Som() : null, rekeningen.Select(r => r.VorigJaar).Som(), rekeningen);

    private static JaarrekeningRegel Som(IndelingsregelSoort soort, string tekst, IEnumerable<JaarrekeningRegel> regels, bool metBegroting)
    {
        var lijst = regels.ToList();
        return new JaarrekeningRegel(soort, tekst, lijst.Select(r => r.Werkelijk).Som(),
            metBegroting ? lijst.Select(r => r.Begroot ?? Bedrag.Nul).Som() : null, lijst.Select(r => r.VorigJaar).Som(), []);
    }

    private sealed record Opzet(JaarrekeningAfdeling Afdeling, IndelingsregelSoort Soort, string Tekst, Func<Rubriek?, bool> Bevat);

    private sealed record Bedragen(Grootboekrekening Rekening, JaarrekeningAfdeling Afdeling, Bedrag Werkelijk, Bedrag Begroot, Bedrag VorigJaar);
}
