using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Verenigd.Domain.Leden;

namespace Verenigd.App.ViewModels.Leden;

public sealed record FunctieRegel(string Omschrijving, string Groep, Lidtype Lidtype, bool IsActief, bool IsTechnisch);

public sealed record VoogdRegel(string Titel, string Naam, string? Email, string? Telefoon, string? Mobiel);

/// <summary>Eén persoon in de ledenlijst. Alleen <see cref="Notities"/> is bewerkbaar; SOL3-velden zijn alleen-lezen (besluit 21).</summary>
public sealed partial class LidRegel : ObservableObject
{
    private const string Datumnotatie = "dd-MM-yyyy";
    private string? _bewaardeNotities;

    public LidRegel(Persoon persoon, DateOnly vandaag)
    {
        ArgumentNullException.ThrowIfNull(persoon);
        Id = persoon.Id;
        Lidnummer = persoon.Lidnummer.Waarde;
        Naam = persoon.VolledigeNaam;
        Voornaam = persoon.Voornaam;
        Achternaam = persoon.Achternaam;
        Geboortedatum = Datum(persoon.Geboortedatum);
        Gender = persoon.Gender;
        Email = persoon.Email;
        Telefoon = persoon.Telefoon is null ? null : Telefoonnummer.Weergave(persoon.Telefoon);
        Adres = string.Join(' ', new[] { persoon.Straat, persoon.Huisnummer, persoon.Toevoeging }.Where(d => !string.IsNullOrWhiteSpace(d)));
        PostcodePlaats = string.Join("  ", new[] { persoon.Postcode, persoon.Plaats }.Where(d => !string.IsNullOrWhiteSpace(d)));
        Land = persoon.Land;
        LidSinds = Datum(persoon.LidSinds);
        UitgeschrevenOp = persoon.UitgeschrevenOp;
        Functies = [.. persoon.Functies
            .OrderByDescending(f => f.IsActief(vandaag))
            .ThenBy(f => f.Groep.Soort == GroepSoort.Technisch)
            .ThenBy(f => f.Groep.Naam, StringComparer.CurrentCulture)
            .Select(f => new FunctieRegel(f.ToString(), f.Groep.Naam, f.Lidtype, f.IsActief(vandaag), f.Groep.Soort == GroepSoort.Technisch))];
        Groepen = string.Join(", ", Functies.Where(f => f.IsActief && !f.IsTechnisch).Select(f => f.Groep).Distinct());
        Initialen = Onderdeelkleuren.Initialen(persoon.Voornaam, persoon.Achternaam);
        Groepskleur = Onderdeelkleuren.VoorGroep(Functies.FirstOrDefault(f => f.IsActief && !f.IsTechnisch)?.Groep);
        Lidtypetekst = string.Join(", ", Functies.Where(f => f.IsActief && f.Lidtype != Lidtype.Onbekend).Select(f => f.Lidtype.ToString()).Distinct());
        Voogden = [.. persoon.Voogden.OrderBy(v => v.Volgnummer).Select(v => new VoogdRegel(
            $"Voogd {v.Volgnummer}",
            v.NaamOnbekend || v.Naam is null ? "(naam onbekend)" : v.Naam,
            v.Email,
            v.Telefoon is null ? null : Telefoonnummer.Weergave(v.Telefoon),
            v.Mobiel is null ? null : Telefoonnummer.Weergave(v.Mobiel)))];
        _bewaardeNotities = persoon.Notities;
        Notities = persoon.Notities;
    }

    public Guid Id { get; }
    public string Lidnummer { get; }
    public string Naam { get; }
    public string Voornaam { get; }
    public string Achternaam { get; }
    public string? Geboortedatum { get; }
    public string? Gender { get; }
    public string? Email { get; }
    public string? Telefoon { get; }
    public string Adres { get; }
    public string PostcodePlaats { get; }
    public string? Land { get; }
    public string? LidSinds { get; }
    public DateOnly? UitgeschrevenOp { get; }
    public IReadOnlyList<FunctieRegel> Functies { get; }
    public IReadOnlyList<VoogdRegel> Voogden { get; }

    /// <summary>Actieve, niet-technische groepen (besluit 40).</summary>
    public string Groepen { get; }

    public string Lidtypetekst { get; }

    /// <summary>Gekleurde initialen in de lijst, met de kleur van de eerste actieve groep.</summary>
    public string Initialen { get; }

    public Domain.Administraties.Kleur Groepskleur { get; }

    public bool IsUitgeschreven => UitgeschrevenOp is not null;

    public string Statustekst => UitgeschrevenOp is { } datum ? $"Uitgeschreven per {Datum(datum)}" : "Actief";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGewijzigd), nameof(Fout))]
    public partial string? Notities { get; set; }

    public bool IsGewijzigd => !string.Equals(Normaliseer(Notities), _bewaardeNotities, StringComparison.Ordinal);

    public string? Fout => Notities is { Length: > Persoon.MaximaleNotitieLengte }
        ? $"Notities mogen maximaal {Persoon.MaximaleNotitieLengte} tekens lang zijn."
        : null;

    public bool HeeftActieveFunctie(Func<FunctieRegel, bool> voorwaarde) => Functies.Any(f => f.IsActief && voorwaarde(f));

    internal string? BewaarbareNotities => Normaliseer(Notities);

    /// <summary>Na opslaan is de huidige waarde de nieuwe uitgangswaarde.</summary>
    internal void Bevestig()
    {
        _bewaardeNotities = Normaliseer(Notities);
        OnPropertyChanged(nameof(IsGewijzigd));
    }

    private static string? Normaliseer(string? tekst) => string.IsNullOrWhiteSpace(tekst) ? null : tekst.Trim();

    private static string? Datum(DateOnly? datum) => datum?.ToString(Datumnotatie, CultureInfo.InvariantCulture);
}
