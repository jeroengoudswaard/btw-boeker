using System.Globalization;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.Application.Incasso;

/// <summary>
/// Berekent de incasso voor een maand (besluiten 44–59). Een maand is verschuldigd als het lid op de 1e van die maand
/// ingeschreven is, een categorie met tarief heeft en die maand nog niet in een (niet-geannuleerde) run zat.
/// </summary>
public static class ContributieRekenaar
{
    public static IncassoVoorstel Bereken(IncassoStand stand, Maand maand, DateOnly vandaag)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var instellingen = stand.Instellingen
            ?? throw new InvalidOperationException("Vul eerst de incassogegevens van de Vereniging in (Extra › Instellingen).");

        var meldingen = new List<string>();
        var uitvoerdatum = instellingen.Uitvoerdatum(maand);
        var vroegst = Bankwerkdagen.TelOp(vandaag, 1);
        if (uitvoerdatum < vroegst)
        {
            meldingen.Add($"De incassodag is te krap: de uitvoerdatum is verschoven naar {Datum(vroegst)}. Upload het bestand vandaag.");
            uitvoerdatum = vroegst;
        }

        var geincasseerd = stand.Runs
            .Where(r => r.Status == IncassorunStatus.Definitief)
            .SelectMany(r => r.Posten)
            .SelectMany(p => p.Regels)
            .Select(r => (r.Lidnummer, r.Maand))
            .ToHashSet();
        var koppelingen = stand.Koppelingen.ToDictionary(k => k.Lidnummer);
        var betalers = stand.Betalers.ToDictionary(b => b.Id);

        // Besluit 58: naheffen tot het begin van het lopende boekjaar, nooit vóór de eerste incassomaand.
        var naheffingVanaf = Max(instellingen.EersteIncassomaand, new Maand(maand.Jaar, 1));
        var vorigJaarVanaf = Max(instellingen.EersteIncassomaand, new Maand(maand.Jaar - 1, 1));
        var vorigJaarTot = new Maand(maand.Jaar - 1, 12);

        var verschuldigd = new List<(VoorstelRegel Regel, Persoon Persoon)>();
        var zelfTeInnen = new ZelfTeInnenLijst();

        foreach (var persoon in stand.Personen.OrderBy(p => p.Lidnummer.Waarde, StringComparer.Ordinal))
        {
            if (naheffingVanaf <= maand)
            {
                foreach (var m in naheffingVanaf.Tot(maand))
                {
                    switch (Beoordeel(stand, geincasseerd, persoon, m))
                    {
                        case (Uitkomst.Verschuldigd, var categorie, var bedrag, _):
                            verschuldigd.Add((new VoorstelRegel(persoon.Lidnummer, persoon.VolledigeNaam, persoon.Voornaam, m, categorie!, bedrag, m < maand), persoon));
                            break;
                        case (Uitkomst.Probleem, _, _, var reden):
                            zelfTeInnen.Voeg(persoon, m, null, reden!);
                            break;
                    }
                }
            }

            if (vorigJaarVanaf <= vorigJaarTot)
            {
                foreach (var m in vorigJaarVanaf.Tot(vorigJaarTot))
                {
                    if (Beoordeel(stand, geincasseerd, persoon, m) is (Uitkomst.Verschuldigd, _, var bedrag, _))
                    {
                        zelfTeInnen.Voeg(persoon, m, bedrag, "Gemiste maanden uit het vorig boekjaar (buiten de naheffingstermijn)");
                    }
                }
            }
        }

        var posten = new List<VoorstelPost>();
        foreach (var groep in verschuldigd.GroupBy(v => koppelingen.TryGetValue(v.Regel.Lidnummer, out var k) ? k.BetalerId : (Guid?)null))
        {
            var betaler = groep.Key is { } id && betalers.TryGetValue(id, out var b) ? b : null;
            var reden = betaler switch
            {
                null => "Geen betaler gekoppeld",
                { Iban.ControlegetalGeldig: false } => "Ongeldig IBAN bij de betaler",
                _ when !betaler.HeeftActieveMachtiging(uitvoerdatum) => "Geen geldige machtiging op de uitvoerdatum",
                _ => null,
            };
            if (reden is not null)
            {
                foreach (var (regel, persoon) in groep)
                {
                    zelfTeInnen.Voeg(persoon, regel.Maand, regel.Bedrag, reden);
                }

                continue;
            }

            var regels = groep.Select(g => g.Regel).ToList();
            posten.Add(new VoorstelPost(
                betaler!,
                betaler!.Rekeninghouder ?? regels[0].Naam,
                SepaTekst.Normaliseer(Omschrijving(instellingen.Omschrijvingssjabloon, regels), 140),
                regels));
        }

        return new IncassoVoorstel(maand, uitvoerdatum, posten, zelfTeInnen.Regels(), meldingen);
    }

    /// <summary>Bijvoorbeeld <c>maart 2027</c>, of <c>jan, feb en mrt 2027</c> bij meerdere maanden.</summary>
    public static string MaandenTekst(IEnumerable<Maand> maanden)
    {
        ArgumentNullException.ThrowIfNull(maanden);
        var lijst = maanden.Distinct().Order().ToList();
        if (lijst.Count == 1)
        {
            return lijst[0].ToString();
        }

        return string.Join(", ", lijst.GroupBy(m => m.Jaar).Select(j =>
        {
            var namen = j.Select(m => m.Kort).ToList();
            var opsomming = namen.Count == 1 ? namen[0] : string.Join(", ", namen.Take(namen.Count - 1)) + " en " + namen[^1];
            return $"{opsomming} {j.Key.ToString(CultureInfo.InvariantCulture)}";
        }));
    }

    private enum Uitkomst
    {
        NietVerschuldigd,
        Verschuldigd,
        Probleem,
    }

    private static (Uitkomst Uitkomst, string? Categorie, Bedrag Bedrag, string? Reden) Beoordeel(
        IncassoStand stand, HashSet<(Lidnummer, Maand)> geincasseerd, Persoon persoon, Maand maand)
    {
        var eerste = maand.EersteDag;
        if (persoon.LidSinds is not { } lidSinds || lidSinds > eerste
            || persoon.UitgeschrevenOp < eerste
            || geincasseerd.Contains((persoon.Lidnummer, maand)))
        {
            return (Uitkomst.NietVerschuldigd, null, Bedrag.Nul, null);
        }

        var categorie = LidCategorie.Op(stand.LidCategorieen, persoon.Lidnummer, eerste)?.Categorie;
        if (categorie is null)
        {
            var isJeugdlid = persoon.Functies.Any(f => f.Lidtype == Lidtype.Jeugdlid && f.IsActief(eerste));
            return isJeugdlid
                ? (Uitkomst.Probleem, null, Bedrag.Nul, "Jeugdlid heeft geen categorie")
                : (Uitkomst.NietVerschuldigd, null, Bedrag.Nul, null);
        }

        if (!categorie.HeeftTarief)
        {
            return (Uitkomst.NietVerschuldigd, null, Bedrag.Nul, null);
        }

        var tarief = stand.Tarieven.FirstOrDefault(t => t.Boekjaar == maand.Jaar && ZelfdeCategorie(t.Categorie, categorie));
        if (tarief is null)
        {
            return (Uitkomst.Probleem, null, Bedrag.Nul, $"Er is geen tarief {maand.Jaar.ToString(CultureInfo.InvariantCulture)} voor {categorie.Naam}");
        }

        return tarief.Maandbedrag == Bedrag.Nul
            ? (Uitkomst.NietVerschuldigd, null, Bedrag.Nul, null)
            : (Uitkomst.Verschuldigd, categorie.Naam, tarief.Maandbedrag, null);
    }

    internal static bool ZelfdeCategorie(Contributiecategorie a, Contributiecategorie b) =>
        ReferenceEquals(a, b) || (a.Id != 0 && a.Id == b.Id);

    private static string Omschrijving(string sjabloon, IReadOnlyList<VoorstelRegel> regels) =>
        sjabloon
            .Replace("{maanden}", MaandenTekst(regels.Select(r => r.Maand)), StringComparison.Ordinal)
            .Replace("{voornamen}", string.Join(", ", regels.Select(r => r.Voornaam).Distinct()), StringComparison.Ordinal);

    private static Maand Max(Maand a, Maand b) => a >= b ? a : b;

    private static string Datum(DateOnly datum) => datum.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    /// <summary>Verzamelt per lid en reden de maanden en (indien bekend) het bedrag.</summary>
    private sealed class ZelfTeInnenLijst
    {
        private readonly List<(Persoon Persoon, string Reden, List<Maand> Maanden, Bedrag? Bedrag)> _regels = [];

        public void Voeg(Persoon persoon, Maand maand, Bedrag? bedrag, string reden)
        {
            var index = _regels.FindIndex(r => ReferenceEquals(r.Persoon, persoon) && r.Reden == reden);
            if (index < 0)
            {
                _regels.Add((persoon, reden, [maand], bedrag));
                return;
            }

            var bestaand = _regels[index];
            bestaand.Maanden.Add(maand);
            _regels[index] = bestaand with { Bedrag = bestaand.Bedrag is { } b && bedrag is { } n ? b + n : null };
        }

        public IReadOnlyList<ZelfTeInnenRegel> Regels() =>
            [.. _regels.Select(r => new ZelfTeInnenRegel(r.Persoon.Lidnummer, r.Persoon.VolledigeNaam, MaandenTekst(r.Maanden), r.Bedrag, r.Reden))];
    }
}

/// <summary>Stelt de categorie van een lid voor (besluit 52).</summary>
public static class CategorieVoorstel
{
    /// <summary>
    /// <c>Jeugdlid &lt;speltak&gt;</c> bij een actieve jeugdlidfunctie, anders <c>Leiding</c> bij een actieve kaderfunctie,
    /// anders geen voorstel.
    /// </summary>
    public static Contributiecategorie? Bepaal(Persoon persoon, IEnumerable<Contributiecategorie> categorieen, DateOnly datum)
    {
        ArgumentNullException.ThrowIfNull(persoon);
        var lijst = categorieen.ToList();
        var actief = persoon.Functies.Where(f => f.IsActief(datum)).ToList();
        var jeugd = actief
            .Where(f => f.Lidtype == Lidtype.Jeugdlid)
            .Select(f => lijst.Find(c => c.Soort == CategorieSoort.Jeugdlid && string.Equals(c.Speltak, f.Groep.Naam, StringComparison.Ordinal)))
            .FirstOrDefault(c => c is not null);
        if (jeugd is not null)
        {
            return jeugd;
        }

        return actief.Any(f => f.Lidtype == Lidtype.Kaderlid) ? lijst.Find(c => c.Soort == CategorieSoort.Leiding) : null;
    }
}
