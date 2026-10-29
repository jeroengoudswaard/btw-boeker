using System.Globalization;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.Application.Import;

/// <summary>Bepaalt zonder iets te wijzigen wat een import zou doen (het voorbeeldoverzicht).</summary>
public static class ImportPlanner
{
    public static ImportVoorstel Plan(ImportStand stand, Sol3Import import, DateOnly importdatum)
    {
        ArgumentNullException.ThrowIfNull(stand);
        ArgumentNullException.ThrowIfNull(import);
        var personen = stand.Personen.ToDictionary(p => p.Lidnummer);
        var inImport = import.Leden.Select(l => l.Lidnummer).ToHashSet();
        var nieuw = new List<ImportRegel>();
        var gewijzigd = new List<ImportRegel>();
        var meldingen = new List<Importmelding>(import.Meldingen);
        var ibanVerschillen = new List<IbanVerschil>();

        foreach (var lid in import.Leden)
        {
            if (!personen.TryGetValue(lid.Lidnummer, out var persoon))
            {
                nieuw.Add(new ImportRegel(lid.Lidnummer, Naam(lid.Gegevens), []));
                MeldMogelijkDubbel(lid, stand, import, meldingen);
                continue;
            }

            var wijzigingen = new List<Veldwijziging>();
            if (persoon.UitgeschrevenOp is { } uitgeschreven)
            {
                wijzigingen.Add(new Veldwijziging("Uitgeschreven op", Datum(uitgeschreven), null));
            }

            wijzigingen.AddRange(persoon.VergelijkGegevens(lid.Gegevens));
            wijzigingen.AddRange(persoon.VergelijkVoogden(lid.Voogden));
            wijzigingen.AddRange(persoon.VergelijkFuncties(lid.Functies, importdatum));
            wijzigingen.AddRange(VergelijkBetaler(stand, lid));
            if (AfwijkendIban(stand, lid) is { } huidig)
            {
                ibanVerschillen.Add(new IbanVerschil(lid.Lidnummer, persoon.VolledigeNaam, huidig, lid.Iban!.Value));
                meldingen.Add(new Importmelding(null, null, lid.Lidnummer.Waarde,
                    $"Het IBAN uit SOL3 (eindigend op {Laatste4(lid.Iban!.Value)}) wijkt af van het IBAN in Verenigd (eindigend op {Laatste4(huidig)}). "
                    + "Standaard blijft het IBAN in Verenigd staan; vink 'IBAN uit SOL3 overnemen' aan als het nieuwe klopt. Een nieuw IBAN vraagt meestal om een nieuwe machtiging."));
            }

            if (wijzigingen.Count > 0)
            {
                gewijzigd.Add(new ImportRegel(lid.Lidnummer, persoon.VolledigeNaam, wijzigingen));
            }
        }

        var uitgeschrevenRegels = stand.Personen
            .Where(p => !p.IsUitgeschreven && !inImport.Contains(p.Lidnummer))
            .Select(p => new ImportRegel(p.Lidnummer, p.VolledigeNaam, [new Veldwijziging("Uitgeschreven op", null, Datum(importdatum))]))
            .ToList();

        var bestaandeGroepen = stand.Groepen.Select(g => g.Naam).ToHashSet(StringComparer.Ordinal);
        var nieuweGroepen = import.Leden
            .SelectMany(l => l.Functies)
            .Select(f => f.Groep)
            .Distinct(StringComparer.Ordinal)
            .Where(g => !bestaandeGroepen.Contains(g))
            .Select(g => new NieuweGroep(g, Groep.VoorgesteldeSoort(g)))
            .ToList();

        return new ImportVoorstel(nieuw, gewijzigd, uitgeschrevenRegels, nieuweGroepen, meldingen) { IbanVerschillen = ibanVerschillen };
    }

    /// <summary>Besluiten 22 en 29: een gevuld IBAN uit SOL3 bepaalt de betaler; een leeg IBAN wijzigt niets.</summary>
    internal static IEnumerable<Veldwijziging> VergelijkBetaler(ImportStand stand, ImportLid lid)
    {
        if (lid.Iban is not { } iban)
        {
            yield break;
        }

        var koppeling = stand.Koppelingen.FirstOrDefault(k => k.Lidnummer == lid.Lidnummer);
        var huidig = koppeling is null ? null : stand.Betalers.FirstOrDefault(b => b.Id == koppeling.BetalerId);
        if (huidig?.Iban != iban)
        {
            yield return new Veldwijziging("IBAN", huidig?.Iban.ToString(), iban.ToString());
        }

        var doel = stand.Betalers.FirstOrDefault(b => b.Iban == iban);
        if (lid.Rekeninghouder is not null && !string.Equals(doel?.Rekeninghouder, lid.Rekeninghouder, StringComparison.Ordinal))
        {
            yield return new Veldwijziging("Rekeninghouder", doel?.Rekeninghouder, lid.Rekeninghouder);
        }
    }

    /// <summary>Het IBAN van de huidige betaler als SOL3 een ander IBAN levert; anders <c>null</c> (besluit 141).</summary>
    internal static Iban? AfwijkendIban(ImportStand stand, ImportLid lid)
    {
        var koppeling = stand.Koppelingen.FirstOrDefault(k => k.Lidnummer == lid.Lidnummer);
        var huidig = koppeling is null ? null : stand.Betalers.FirstOrDefault(b => b.Id == koppeling.BetalerId);
        return lid.Iban is { } iban && huidig is not null && huidig.Iban != iban ? huidig.Iban : null;
    }

    /// <summary>Alleen de laatste vier cijfers, zodat een melding geen volledig IBAN bevat.</summary>
    private static string Laatste4(Iban iban) => iban.Waarde[^4..];

    /// <summary>Besluit 23: niet automatisch samenvoegen, wel waarschuwen.</summary>
    private static void MeldMogelijkDubbel(ImportLid lid, ImportStand stand, Sol3Import import, List<Importmelding> meldingen)
    {
        var anderen = stand.Personen.Select(p => (p.Lidnummer, p.Gegevens))
            .Concat(import.Leden.Select(l => (l.Lidnummer, l.Gegevens)))
            .Where(a => a.Lidnummer != lid.Lidnummer && ZelfdePersoon(a.Gegevens, lid.Gegevens))
            .Select(a => a.Lidnummer.Waarde)
            .Distinct()
            .ToList();
        if (anderen.Count > 0)
        {
            meldingen.Add(new Importmelding(null, null, lid.Lidnummer.Waarde,
                $"Mogelijk dubbele persoon: zelfde naam en geboortedatum als lidnummer {string.Join(", ", anderen)}. Niet samengevoegd."));
        }
    }

    private static bool ZelfdePersoon(LidGegevens a, LidGegevens b) =>
        a.Geboortedatum is not null && a.Geboortedatum == b.Geboortedatum
        && string.Equals(a.Voornaam, b.Voornaam, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.Achternaam, b.Achternaam, StringComparison.OrdinalIgnoreCase);

    internal static string Naam(LidGegevens gegevens) =>
        string.Join(' ', new[] { gegevens.Voornaam, gegevens.Tussenvoegsel, gegevens.Achternaam }.Where(d => !string.IsNullOrWhiteSpace(d)));

    private static string Datum(DateOnly datum) => datum.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
}

/// <summary>Voert een import uit op een (door de database gevolgde) stand.</summary>
public static class ImportToepasser
{
    public static ImportWijzigingen PasToe(
        ImportStand stand, Sol3Import import, ImportKeuzes keuzes, DateOnly importdatum, DateTime nuUtc)
    {
        ArgumentNullException.ThrowIfNull(stand);
        ArgumentNullException.ThrowIfNull(import);
        ArgumentNullException.ThrowIfNull(keuzes);
        var voorstel = ImportPlanner.Plan(stand, import, importdatum);

        var groepen = stand.Groepen.ToDictionary(g => g.Naam, StringComparer.Ordinal);
        var nieuweGroepen = new List<Groep>();
        foreach (var nieuw in voorstel.NieuweGroepen)
        {
            var groep = new Groep(nieuw.Naam, keuzes.GroepSoorten.GetValueOrDefault(nieuw.Naam, nieuw.VoorgesteldeSoort));
            groepen[groep.Naam] = groep;
            nieuweGroepen.Add(groep);
        }

        var personen = stand.Personen.ToDictionary(p => p.Lidnummer);
        var betalers = stand.Betalers.ToList();
        var koppelingen = stand.Koppelingen.ToDictionary(k => k.Lidnummer);
        var nieuwePersonen = new List<Persoon>();
        var nieuweBetalers = new List<Betaler>();
        var nieuweKoppelingen = new List<BetalerLid>();

        foreach (var lid in import.Leden)
        {
            if (personen.TryGetValue(lid.Lidnummer, out var persoon))
            {
                if (persoon.IsUitgeschreven)
                {
                    persoon.Herinschrijven(nuUtc);
                }

                persoon.WerkGegevensBij(lid.Gegevens, nuUtc);
            }
            else
            {
                persoon = new Persoon(lid.Lidnummer, lid.Gegevens, nuUtc);
                nieuwePersonen.Add(persoon);
            }

            persoon.WerkVoogdenBij(lid.Voogden, nuUtc);
            persoon.WerkFunctiesBij(lid.Functies, groepen, importdatum, nuUtc);
            // Besluit 141: een afwijkend IBAN alleen overnemen als de gebruiker dat per lid kiest.
            if (ImportPlanner.AfwijkendIban(stand, lid) is null || keuzes.IbanOvernemen.Contains(lid.Lidnummer))
            {
                KoppelBetaler(lid);
            }
        }

        var uitgeschreven = 0;
        foreach (var regel in voorstel.Uitgeschreven.Where(r => keuzes.Uitschrijven.Contains(r.Lidnummer)))
        {
            personen[regel.Lidnummer].Uitschrijven(importdatum, nuUtc);
            uitgeschreven++;
        }

        return new ImportWijzigingen(nieuwePersonen, nieuweGroepen, nieuweBetalers, nieuweKoppelingen, voorstel.Gewijzigd.Count, uitgeschreven);

        void KoppelBetaler(ImportLid lid)
        {
            if (lid.Iban is not { } iban)
            {
                return;
            }

            var betaler = betalers.Find(b => b.Iban == iban);
            if (betaler is null)
            {
                betaler = new Betaler(iban, lid.Rekeninghouder);
                betalers.Add(betaler);
                nieuweBetalers.Add(betaler);
            }
            else if (lid.Rekeninghouder is not null)
            {
                betaler.WijzigRekeninghouder(lid.Rekeninghouder);
            }

            if (koppelingen.TryGetValue(lid.Lidnummer, out var koppeling))
            {
                koppeling.KoppelAan(betaler.Id);
            }
            else
            {
                koppeling = new BetalerLid(lid.Lidnummer, betaler.Id);
                koppelingen[lid.Lidnummer] = koppeling;
                nieuweKoppelingen.Add(koppeling);
            }
        }
    }
}
