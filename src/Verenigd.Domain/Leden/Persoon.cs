using Verenigd.Domain.Gedeeld;

namespace Verenigd.Domain.Leden;

/// <summary>
/// Een persoon uit SOL3. De SOL3-velden worden alleen door de import bijgewerkt (besluit 21);
/// Verenigd voegt eigen velden toe, zoals notities.
/// </summary>
public sealed class Persoon
{
    public const int MaximaleNotitieLengte = 4000;

    private readonly List<Functie> _functies = [];
    private readonly List<Voogd> _voogden = [];
    private readonly List<Veldherkomst> _herkomst = [];

    private Persoon()
    {
        Voornaam = null!;
        Achternaam = null!;
    }

    public Persoon(Lidnummer lidnummer, LidGegevens gegevens, DateTime nuUtc)
    {
        ArgumentNullException.ThrowIfNull(gegevens);
        Id = Guid.NewGuid();
        Lidnummer = lidnummer;
        Voornaam = gegevens.Voornaam;
        Achternaam = gegevens.Achternaam;
        Neem(gegevens);
        foreach (var (veld, _) in LidGegevens.Velden)
        {
            ZetHerkomst(veld, Veldbron.Sol3, nuUtc);
        }

        LaatstGewijzigdUtc = nuUtc;
    }

    /// <summary>Vaste ID, ook voor de synchronisatie met de webapp (besluit 38).</summary>
    public Guid Id { get; private set; }

    public Lidnummer Lidnummer { get; private set; }
    public string Voornaam { get; private set; }
    public string? Tussenvoegsel { get; private set; }
    public string Achternaam { get; private set; }
    public DateOnly? Geboortedatum { get; private set; }
    public string? Gender { get; private set; }
    public string? Email { get; private set; }
    public string? Telefoon { get; private set; }
    public string? ContactpersoonId { get; private set; }
    public string? Status { get; private set; }
    public string? Straat { get; private set; }
    public string? Huisnummer { get; private set; }
    public string? Toevoeging { get; private set; }
    public string? Postcode { get; private set; }
    public string? Plaats { get; private set; }
    public string? Land { get; private set; }

    /// <summary>Gezet als het lid uit de SOL3-export verdwenen is (besluit 25). Personen worden nooit verwijderd.</summary>
    public DateOnly? UitgeschrevenOp { get; private set; }

    public string? Notities { get; private set; }
    public DateTime LaatstGewijzigdUtc { get; private set; }

    public IReadOnlyList<Functie> Functies => _functies;
    public IReadOnlyList<Voogd> Voogden => _voogden;
    public IReadOnlyList<Veldherkomst> Herkomst => _herkomst;

    public bool IsUitgeschreven => UitgeschrevenOp is not null;

    public string VolledigeNaam =>
        string.Join(' ', new[] { Voornaam, Tussenvoegsel, Achternaam }.Where(d => !string.IsNullOrWhiteSpace(d)));

    /// <summary>Afgeleid: de vroegste startdatum van alle functies, ook van technische groepen (besluiten 24 en 40).</summary>
    public DateOnly? LidSinds => _functies.Min(f => f.Startdatum);

    public LidGegevens Gegevens => new(
        Voornaam, Tussenvoegsel, Achternaam, Geboortedatum, Gender, Email, Telefoon, ContactpersoonId, Status,
        Straat, Huisnummer, Toevoeging, Postcode, Plaats, Land);

    public IReadOnlyList<Veldwijziging> VergelijkGegevens(LidGegevens nieuw)
    {
        ArgumentNullException.ThrowIfNull(nieuw);
        var huidig = Gegevens;
        return [.. LidGegevens.Velden
            .Select(v => new Veldwijziging(v.Veld, v.Waarde(huidig), v.Waarde(nieuw)))
            .Where(w => !string.Equals(w.Oud, w.Nieuw, StringComparison.Ordinal))];
    }

    public void WerkGegevensBij(LidGegevens nieuw, DateTime nuUtc)
    {
        var wijzigingen = VergelijkGegevens(nieuw);
        if (wijzigingen.Count == 0)
        {
            return;
        }

        Voornaam = nieuw.Voornaam;
        Achternaam = nieuw.Achternaam;
        Neem(nieuw);
        foreach (var wijziging in wijzigingen)
        {
            ZetHerkomst(wijziging.Veld, Veldbron.Sol3, nuUtc);
        }

        LaatstGewijzigdUtc = nuUtc;
    }

    public IReadOnlyList<Veldwijziging> VergelijkVoogden(IReadOnlyList<VoogdGegevens> nieuw)
    {
        ArgumentNullException.ThrowIfNull(nieuw);
        var wijzigingen = new List<Veldwijziging>();
        foreach (var volgnummer in VoogdVolgnummers(nieuw))
        {
            var oud = _voogden.Find(v => v.Volgnummer == volgnummer)?.Gegevens;
            var nieuwe = nieuw.FirstOrDefault(v => v.Volgnummer == volgnummer);
            var label = $"Voogd {volgnummer}";
            if (oud is null || nieuwe is null)
            {
                wijzigingen.Add(new Veldwijziging(label, Omschrijf(oud), Omschrijf(nieuwe)));
                continue;
            }

            Vergelijk(label + " naam", oud.NaamOnbekend ? null : oud.Naam, nieuwe.NaamOnbekend ? null : nieuwe.Naam);
            Vergelijk(label + " e-mail", oud.Email, nieuwe.Email);
            Vergelijk(label + " telefoon", Weergave(oud.Telefoon), Weergave(nieuwe.Telefoon));
            Vergelijk(label + " mobiel", Weergave(oud.Mobiel), Weergave(nieuwe.Mobiel));
        }

        return wijzigingen;

        void Vergelijk(string veld, string? oud, string? nieuwe)
        {
            if (!string.Equals(oud, nieuwe, StringComparison.Ordinal))
            {
                wijzigingen.Add(new Veldwijziging(veld, oud, nieuwe));
            }
        }
    }

    public void WerkVoogdenBij(IReadOnlyList<VoogdGegevens> nieuw, DateTime nuUtc)
    {
        if (VergelijkVoogden(nieuw).Count == 0)
        {
            return;
        }

        foreach (var volgnummer in VoogdVolgnummers(nieuw))
        {
            var bestaand = _voogden.Find(v => v.Volgnummer == volgnummer);
            var nieuwe = nieuw.FirstOrDefault(v => v.Volgnummer == volgnummer);
            if (nieuwe is null)
            {
                _voogden.Remove(bestaand!);
            }
            else if (bestaand is null)
            {
                _voogden.Add(new Voogd(nieuwe));
            }
            else
            {
                bestaand.Neem(nieuwe);
            }
        }

        ZetHerkomst("Voogden", Veldbron.Sol3, nuUtc);
        LaatstGewijzigdUtc = nuUtc;
    }

    /// <summary>
    /// Verschillen in functies. Een functie wordt herkend aan groep, rol en startdatum. Een open functie die niet
    /// meer geleverd wordt, krijgt de importdatum als einddatum; beëindigde functies blijven bewaard.
    /// </summary>
    public IReadOnlyList<Veldwijziging> VergelijkFuncties(IReadOnlyList<FunctieGegevens> nieuw, DateOnly importdatum)
    {
        ArgumentNullException.ThrowIfNull(nieuw);
        var wijzigingen = new List<Veldwijziging>();
        foreach (var gegevens in nieuw)
        {
            var bestaand = _functies.Find(f => f.HeeftSleutel(gegevens));
            var nieuweOmschrijving = Functie.Omschrijf(gegevens.Rol, gegevens.Groep, gegevens.Startdatum, gegevens.Einddatum);
            if (bestaand is null)
            {
                wijzigingen.Add(new Veldwijziging("Functie", null, nieuweOmschrijving));
            }
            else if (!bestaand.IsGelijk(gegevens))
            {
                wijzigingen.Add(new Veldwijziging("Functie", bestaand.ToString(), nieuweOmschrijving));
            }
        }

        foreach (var verdwenen in TeBeeindigen(nieuw, importdatum))
        {
            wijzigingen.Add(new Veldwijziging(
                "Functie",
                verdwenen.ToString(),
                Functie.Omschrijf(verdwenen.Rol, verdwenen.Groep.Naam, verdwenen.Startdatum, importdatum)));
        }

        return wijzigingen;
    }

    /// <param name="groepen">Alle bekende groepen op naam; elke groep uit <paramref name="nieuw"/> moet erin staan.</param>
    public void WerkFunctiesBij(
        IReadOnlyList<FunctieGegevens> nieuw, IReadOnlyDictionary<string, Groep> groepen, DateOnly importdatum, DateTime nuUtc)
    {
        ArgumentNullException.ThrowIfNull(groepen);
        if (VergelijkFuncties(nieuw, importdatum).Count == 0)
        {
            return;
        }

        foreach (var verdwenen in TeBeeindigen(nieuw, importdatum).ToList())
        {
            verdwenen.Beeindig(importdatum);
        }

        foreach (var gegevens in nieuw)
        {
            var bestaand = _functies.Find(f => f.HeeftSleutel(gegevens));
            if (bestaand is null)
            {
                _functies.Add(new Functie(groepen[gegevens.Groep], gegevens));
            }
            else
            {
                bestaand.Neem(gegevens);
            }
        }

        ZetHerkomst("Functies", Veldbron.Sol3, nuUtc);
        LaatstGewijzigdUtc = nuUtc;
    }

    /// <summary>Uitschrijven met einddatum; open functies worden op dezelfde datum beëindigd (besluit 25).</summary>
    public void Uitschrijven(DateOnly datum, DateTime nuUtc)
    {
        UitgeschrevenOp = datum;
        foreach (var functie in _functies.Where(f => f.Einddatum is null || f.Einddatum > datum))
        {
            functie.Beeindig(datum);
        }

        ZetHerkomst("Uitgeschreven op", Veldbron.Verenigd, nuUtc);
        LaatstGewijzigdUtc = nuUtc;
    }

    /// <summary>Het lid staat weer in de SOL3-export.</summary>
    public void Herinschrijven(DateTime nuUtc)
    {
        UitgeschrevenOp = null;
        ZetHerkomst("Uitgeschreven op", Veldbron.Sol3, nuUtc);
        LaatstGewijzigdUtc = nuUtc;
    }

    public void WijzigNotities(string? notities, DateTime nuUtc)
    {
        var schoon = string.IsNullOrWhiteSpace(notities) ? null : notities.Trim();
        if (schoon is { Length: > MaximaleNotitieLengte })
        {
            throw new ArgumentException($"Notities mogen maximaal {MaximaleNotitieLengte} tekens lang zijn.", nameof(notities));
        }

        if (string.Equals(schoon, Notities, StringComparison.Ordinal))
        {
            return;
        }

        Notities = schoon;
        ZetHerkomst("Notities", Veldbron.Verenigd, nuUtc);
        LaatstGewijzigdUtc = nuUtc;
    }

    /// <summary>
    /// Een goedgekeurd voorstel van de leiding (besluit 34): het veld krijgt de nieuwe waarde met bron "leiding" (besluit 38).
    /// Telefoonnummers worden genormaliseerd (besluit 32); een lege waarde maakt het veld leeg.
    /// </summary>
    public void NeemVoorstelOver(string veld, string? waarde, DateTime nuUtc)
    {
        ArgumentNullException.ThrowIfNull(veld);
        if (!Leidingveld.IsGeldig(veld))
        {
            throw new ArgumentException($"Voor het veld '{veld}' kan de leiding geen voorstel doen.", nameof(veld));
        }

        var schoon = string.IsNullOrWhiteSpace(waarde) ? null : waarde.Trim();
        static string? Normaal(string? t) => t is null ? null : Telefoonnummer.Normaliseer(t).Waarde;

        switch (veld)
        {
            case Leidingveld.Email:
                Email = schoon;
                ZetHerkomst("E-mail", Veldbron.Leiding, nuUtc);
                break;
            case Leidingveld.Telefoon:
                Telefoon = Normaal(schoon);
                ZetHerkomst("Telefoon", Veldbron.Leiding, nuUtc);
                break;
            default:
                var (volgnummer, deel) = Leidingveld.Voogdveld(veld);
                var voogd = _voogden.Find(v => v.Volgnummer == volgnummer);
                if (voogd is null)
                {
                    voogd = new Voogd(new VoogdGegevens(volgnummer!.Value, null, true, null, null, null));
                    _voogden.Add(voogd);
                }

                var huidig = voogd.Gegevens;
                voogd.Neem(deel switch
                {
                    "naam" => huidig with { Naam = schoon, NaamOnbekend = schoon is null },
                    "email" => huidig with { Email = schoon },
                    "telefoon" => huidig with { Telefoon = Normaal(schoon) },
                    _ => huidig with { Mobiel = Normaal(schoon) },
                });
                ZetHerkomst("Voogden", Veldbron.Leiding, nuUtc);
                break;
        }

        LaatstGewijzigdUtc = nuUtc;
    }

    private IEnumerable<Functie> TeBeeindigen(IReadOnlyList<FunctieGegevens> nieuw, DateOnly importdatum) =>
        _functies.Where(f => (f.Einddatum is null || f.Einddatum > importdatum) && !nieuw.Any(f.HeeftSleutel));

    private IEnumerable<int> VoogdVolgnummers(IReadOnlyList<VoogdGegevens> nieuw) =>
        _voogden.Select(v => v.Volgnummer).Concat(nieuw.Select(v => v.Volgnummer)).Distinct().Order();

    private static string? Weergave(string? telefoon) => telefoon is null ? null : Telefoonnummer.Weergave(telefoon);

    private static string? Omschrijf(VoogdGegevens? voogd) =>
        voogd is null ? null : string.Join(", ", new[] { voogd.NaamOnbekend ? "(naam onbekend)" : voogd.Naam, voogd.Email }
            .Where(d => !string.IsNullOrWhiteSpace(d)));

    private void Neem(LidGegevens gegevens)
    {
        Tussenvoegsel = gegevens.Tussenvoegsel;
        Geboortedatum = gegevens.Geboortedatum;
        Gender = gegevens.Gender;
        Email = gegevens.Email;
        Telefoon = gegevens.Telefoon;
        ContactpersoonId = gegevens.ContactpersoonId;
        Status = gegevens.Status;
        Straat = gegevens.Straat;
        Huisnummer = gegevens.Huisnummer;
        Toevoeging = gegevens.Toevoeging;
        Postcode = gegevens.Postcode;
        Plaats = gegevens.Plaats;
        Land = gegevens.Land;
    }

    private void ZetHerkomst(string veld, Veldbron bron, DateTime nuUtc)
    {
        var bestaand = _herkomst.Find(h => h.Veld == veld);
        if (bestaand is null)
        {
            _herkomst.Add(new Veldherkomst(veld, bron, nuUtc));
        }
        else
        {
            bestaand.Wijzig(bron, nuUtc);
        }
    }
}
