using System.Globalization;
using System.Text.RegularExpressions;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.Application.Import;

/// <summary>
/// Voegt de vier SOL3-exports samen tot één <see cref="ImportLid"/> per lidnummer en controleert de gegevens.
/// SOL3 levert één regel per persoon × eenheid (× rol); identieke regels worden zonder melding ontdubbeld.
/// </summary>
public static class Sol3Inlezer
{
    private const string Actief = "Actief";
    private static readonly CultureInfo Nederlands = CultureInfo.GetCultureInfo("nl-NL");

    public static Sol3Import Lees(IReadOnlyDictionary<Sol3Export, CsvTabel> tabellen, Sol3ImportOpties opties)
    {
        ArgumentNullException.ThrowIfNull(tabellen);
        ArgumentNullException.ThrowIfNull(opties);
        var meldingen = new List<Importmelding>();
        var plaatsvervangers = opties.VoogdPlaatsvervangers
            .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            .ToList();

        var leden = PerLid(tabellen[Sol3Export.LedenBasis], Sol3Export.LedenBasis, meldingen, null);
        var bekend = leden.Keys.ToHashSet();
        var huishoudens = PerLid(tabellen[Sol3Export.Huishouden], Sol3Export.Huishouden, meldingen, bekend);
        var voogden = PerLid(tabellen[Sol3Export.Voogden], Sol3Export.Voogden, meldingen, bekend);
        var functies = PerLid(tabellen[Sol3Export.Functies], Sol3Export.Functies, meldingen, bekend, ontdubbel: false);

        ControleerContactpersoonIds(leden, meldingen);

        var resultaat = new List<ImportLid>(leden.Count);
        foreach (var (lidnummer, regels) in leden)
        {
            var melder = new Melder(meldingen, lidnummer.Waarde);
            var lid = Unieke(regels, Sol3Export.LedenBasis, melder);
            huishoudens.TryGetValue(lidnummer, out var huishoudenRegels);
            var huishouden = huishoudenRegels is null ? null : Unieke(huishoudenRegels, Sol3Export.Huishouden, melder);
            if (huishouden is null)
            {
                melder.Meld(Sol3Export.Huishouden, null, "Geen adresgegevens: het lid staat niet in export 2.");
            }

            else if (huishouden.Waarde(Kolom.ContactpersoonId) is { } id
                && !string.Equals(id, lid.Waarde(Kolom.ContactpersoonId), StringComparison.Ordinal))
            {
                melder.Meld(Sol3Export.Huishouden, huishouden.Regelnummer, "Contactpersoon-ID wijkt af van export 1.");
            }

            voogden.TryGetValue(lidnummer, out var voogdRegels);
            var voogd = voogdRegels is null ? null : Unieke(voogdRegels, Sol3Export.Voogden, melder);

            if (!functies.TryGetValue(lidnummer, out var functieRegels))
            {
                melder.Meld(Sol3Export.Functies, null, "Geen functies: het lid staat niet in export 4.");
            }

            var (iban, rekeninghouder) = huishouden is null ? (null, null) : LeesBetaler(huishouden, melder);
            resultaat.Add(new ImportLid(
                lidnummer,
                LeesGegevens(lid, huishouden, melder),
                voogd is null ? [] : LeesVoogden(voogd, plaatsvervangers, melder),
                LeesFuncties(functieRegels ?? [], melder),
                iban,
                rekeninghouder));
        }

        return new Sol3Import(resultaat, meldingen);
    }

    private static Dictionary<Lidnummer, List<CsvRegel>> PerLid(
        CsvTabel tabel, Sol3Export export, List<Importmelding> meldingen, HashSet<Lidnummer>? bekend, bool ontdubbel = true)
    {
        var perLid = new Dictionary<Lidnummer, List<CsvRegel>>();
        var onbekend = new HashSet<Lidnummer>();
        foreach (var regel in tabel.Regels)
        {
            if (!Lidnummer.TryParse(regel[Kolom.Lidnummer], out var lidnummer))
            {
                meldingen.Add(new Importmelding(export, regel.Regelnummer, null, "Ongeldig of leeg lidnummer; de regel is overgeslagen."));
                continue;
            }

            if (bekend is not null && !bekend.Contains(lidnummer))
            {
                if (onbekend.Add(lidnummer))
                {
                    meldingen.Add(new Importmelding(
                        export, regel.Regelnummer, lidnummer.Waarde, "Het lidnummer staat niet in export 1; de gegevens zijn overgeslagen."));
                }

                continue;
            }

            if (!perLid.TryGetValue(lidnummer, out var regels))
            {
                perLid[lidnummer] = regels = [];
            }

            if (!ontdubbel || !regels.Exists(r => ZelfdeInhoud(export, r, regel)))
            {
                regels.Add(regel);
            }
        }

        return perLid;
    }

    /// <summary>Alleen de kolommen die Verenigd gebruikt tellen mee; organisatie- en eenheidkolommen niet.</summary>
    private static bool ZelfdeInhoud(Sol3Export export, CsvRegel a, CsvRegel b) =>
        ExportHerkenning.VerplichteKolommen(export).All(k => string.Equals(a[k], b[k], StringComparison.Ordinal));

    private static CsvRegel Unieke(List<CsvRegel> regels, Sol3Export export, Melder melder)
    {
        if (regels.Count > 1)
        {
            melder.Meld(export, regels[1].Regelnummer,
                "Het lid staat in meerdere regels met verschillende gegevens; de eerste regel is gebruikt.");
        }

        return regels[0];
    }

    private static LidGegevens LeesGegevens(CsvRegel lid, CsvRegel? huishouden, Melder melder)
    {
        var export = Sol3Export.LedenBasis;
        var status = lid.Waarde(Kolom.Status);
        if (!string.Equals(status, Actief, StringComparison.Ordinal))
        {
            melder.Meld(export, lid.Regelnummer, $"Onbekende status '{status}'; verwacht '{Actief}'.");
        }

        var land = huishouden?.Waarde(Kolom.Land);
        return new LidGegevens(
            lid[Kolom.Voornaam],
            lid.Waarde(Kolom.Tussenvoegsel),
            lid[Kolom.Achternaam],
            Datum(lid, Kolom.Geboortedatum, export, melder),
            lid.Waarde(Kolom.Gender),
            lid.Waarde(Kolom.Email),
            Telefoon(lid, Kolom.Telefoon, export, melder, "Telefoon"),
            lid.Waarde(Kolom.ContactpersoonId),
            status,
            huishouden?.Waarde(Kolom.Straat),
            huishouden?.Waarde(Kolom.Huisnummer),
            huishouden?.Waarde(Kolom.Toevoeging),
            huishouden is null ? null : LeesPostcode(huishouden, land, melder),
            huishouden?.Waarde(Kolom.Plaats) is { } plaats ? Plaatsnaam(plaats) : null,
            land,
            Datum(lid, Kolom.Startdatum, export, melder));
    }

    private static string? LeesPostcode(CsvRegel huishouden, string? land, Melder melder)
    {
        if (huishouden.Waarde(Kolom.Postcode) is not { } invoer)
        {
            return null;
        }

        var postcode = Postcode.Normaliseer(invoer);
        if (!postcode.Herkend && (land is null || land.Equals("Nederland", StringComparison.OrdinalIgnoreCase)))
        {
            melder.Meld(Sol3Export.Huishouden, huishouden.Regelnummer, "Postcode is geen geldige Nederlandse postcode; ongewijzigd bewaard.");
        }

        return postcode.Waarde;
    }

    /// <summary>Plaatsnamen in alleen hoofdletters of alleen kleine letters krijgen een hoofdletter per woord.</summary>
    private static string Plaatsnaam(string plaats)
    {
        if (!string.Equals(plaats, plaats.ToUpper(Nederlands), StringComparison.Ordinal)
            && !string.Equals(plaats, plaats.ToLower(Nederlands), StringComparison.Ordinal))
        {
            return plaats;
        }

        var titel = Nederlands.TextInfo.ToTitleCase(plaats.ToLower(Nederlands));
        return titel.StartsWith("'S", StringComparison.Ordinal) ? "'s" + titel[2..] : titel;
    }

    private static (Iban? Iban, string? Rekeninghouder) LeesBetaler(CsvRegel huishouden, Melder melder)
    {
        if (!Iban.TryMaak(huishouden.Waarde(Kolom.Iban), out var iban))
        {
            return (null, null);
        }

        if (!iban.ControlegetalGeldig)
        {
            melder.Meld(Sol3Export.Huishouden, huishouden.Regelnummer, "IBAN heeft een ongeldig controlegetal.");
        }

        return (iban, huishouden.Waarde(Kolom.Rekeninghouder));
    }

    private static List<VoogdGegevens> LeesVoogden(CsvRegel regel, List<Regex> plaatsvervangers, Melder melder)
    {
        var voogden = new List<VoogdGegevens>(2);
        Voeg(1, Kolom.Voogd1Naam, Kolom.Voogd1Email, Kolom.Voogd1Telefoon, Kolom.Voogd1Mobiel);
        Voeg(2, Kolom.Voogd2Naam, Kolom.Voogd2Email, Kolom.Voogd2Telefoon, Kolom.Voogd2Mobiel);
        return voogden;

        void Voeg(int volgnummer, string naamKolom, string emailKolom, string telefoonKolom, string mobielKolom)
        {
            var naam = regel.Waarde(naamKolom);
            var email = regel.Waarde(emailKolom);
            var telefoon = Telefoon(regel, telefoonKolom, Sol3Export.Voogden, melder, $"Voogd {volgnummer} telefoon");
            var mobiel = Telefoon(regel, mobielKolom, Sol3Export.Voogden, melder, $"Voogd {volgnummer} mobiel");
            if (naam is null && email is null && telefoon is null && mobiel is null)
            {
                return;
            }

            var onbekend = naam is null || plaatsvervangers.Exists(p => p.IsMatch(naam));
            voogden.Add(new VoogdGegevens(volgnummer, onbekend ? null : naam, onbekend, email, telefoon, mobiel));
        }
    }

    private static List<FunctieGegevens> LeesFuncties(List<CsvRegel> regels, Melder melder)
    {
        var functies = new List<FunctieGegevens>();
        foreach (var regel in regels)
        {
            var export = Sol3Export.Functies;
            var rol = regel.Waarde(Kolom.Rol);
            var groep = regel.Waarde(Kolom.Groep);
            if (rol is null || groep is null)
            {
                melder.Meld(export, regel.Regelnummer, "Functie zonder rol of eenheid; de regel is overgeslagen.");
                continue;
            }

            var lidtype = regel.Waarde(Kolom.Lidtype) switch
            {
                { } t when t.Equals("Jeugdlid", StringComparison.OrdinalIgnoreCase) => Lidtype.Jeugdlid,
                { } t when t.Equals("Kaderlid", StringComparison.OrdinalIgnoreCase) => Lidtype.Kaderlid,
                var t => Onbekend(t),
            };
            var status = regel.Waarde(Kolom.Status);
            if (!string.Equals(status, Actief, StringComparison.Ordinal))
            {
                melder.Meld(export, regel.Regelnummer, $"Onbekende functiestatus '{status}'; verwacht '{Actief}'.");
            }

            var functie = new FunctieGegevens(
                groep, rol, lidtype, Datum(regel, Kolom.Startdatum, export, melder), Datum(regel, Kolom.Einddatum, export, melder), status);
            if (!functies.Contains(functie))
            {
                functies.Add(functie);
            }

            Lidtype Onbekend(string? waarde)
            {
                melder.Meld(export, regel.Regelnummer, $"Onbekend lidtype '{waarde}'; verwacht 'Jeugdlid' of 'Kaderlid'.");
                return Lidtype.Onbekend;
            }
        }

        // Besluit 41: dezelfde rol in dezelfde groep zonder einddatum is één functie, met de vroegste startdatum.
        var samengevoegd = new List<FunctieGegevens>(functies.Count);
        foreach (var functie in functies)
        {
            var index = functie.Einddatum is null
                ? samengevoegd.FindIndex(s => s.Einddatum is null && s.Groep == functie.Groep && s.Rol == functie.Rol)
                : -1;
            if (index < 0)
            {
                samengevoegd.Add(functie);
            }
            else if (functie.Startdatum < samengevoegd[index].Startdatum || samengevoegd[index].Startdatum is null)
            {
                samengevoegd[index] = samengevoegd[index] with { Startdatum = functie.Startdatum ?? samengevoegd[index].Startdatum };
            }
        }

        return samengevoegd;
    }

    private static DateOnly? Datum(CsvRegel regel, string kolom, Sol3Export export, Melder melder)
    {
        if (regel.Waarde(kolom) is not { } tekst)
        {
            return null;
        }

        if (Sol3Datum.TryParse(tekst, out var datum))
        {
            return datum;
        }

        melder.Meld(export, regel.Regelnummer, $"{kolom} is geen leesbare datum (verwacht dag-maand-jaar).");
        return null;
    }

    private static string? Telefoon(CsvRegel regel, string kolom, Sol3Export export, Melder melder, string omschrijving)
    {
        if (regel.Waarde(kolom) is not { } tekst)
        {
            return null;
        }

        var nummer = Telefoonnummer.Normaliseer(tekst);
        if (!nummer.Herkend)
        {
            melder.Meld(export, regel.Regelnummer, $"{omschrijving} is niet herkend; ongewijzigd bewaard.");
        }

        return nummer.Waarde;
    }

    /// <summary>Besluit 27: het Contactpersoon-ID hoort 1-op-1 bij het lidnummer (ordinaal vergeleken, besluit 30).</summary>
    private static void ControleerContactpersoonIds(Dictionary<Lidnummer, List<CsvRegel>> leden, List<Importmelding> meldingen)
    {
        var perId = leden
            .Where(l => l.Value[0].Waarde(Kolom.ContactpersoonId) is not null)
            .GroupBy(l => l.Value[0][Kolom.ContactpersoonId], StringComparer.Ordinal)
            .Where(g => g.Count() > 1);
        foreach (var groep in perId)
        {
            var nummers = groep.Select(l => l.Key.Waarde).ToList();
            foreach (var (lidnummer, regels) in groep)
            {
                meldingen.Add(new Importmelding(Sol3Export.LedenBasis, regels[0].Regelnummer, lidnummer.Waarde,
                    $"Contactpersoon-ID wordt ook gebruikt door lidnummer {string.Join(", ", nummers.Where(n => n != lidnummer.Waarde))}."));
            }
        }
    }

    private sealed class Melder(List<Importmelding> meldingen, string lidnummer)
    {
        public void Meld(Sol3Export export, int? regel, string tekst) =>
            meldingen.Add(new Importmelding(export, regel, lidnummer, tekst));
    }
}
