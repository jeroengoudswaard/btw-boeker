namespace Verenigd.Domain.Leden;

/// <summary>De persoonsgegevens die SOL3 levert (exports 1 en 2). Telefoon en postcode zijn al genormaliseerd.</summary>
public sealed record LidGegevens(
    string Voornaam,
    string? Tussenvoegsel,
    string Achternaam,
    DateOnly? Geboortedatum,
    string? Gender,
    string? Email,
    string? Telefoon,
    string? ContactpersoonId,
    string? Status,
    string? Straat,
    string? Huisnummer,
    string? Toevoeging,
    string? Postcode,
    string? Plaats,
    string? Land,
    DateOnly? Startdatum = null)
{
    internal const string Datumnotatie = "dd-MM-yyyy";

    /// <summary>Veldnaam zoals getoond aan de gebruiker, met de leesbare waarde.</summary>
    internal static IReadOnlyList<(string Veld, Func<LidGegevens, string?> Waarde)> Velden { get; } =
    [
        ("Voornaam", g => g.Voornaam),
        ("Tussenvoegsel", g => g.Tussenvoegsel),
        ("Achternaam", g => g.Achternaam),
        ("Geboortedatum", g => g.Geboortedatum?.ToString(Datumnotatie, System.Globalization.CultureInfo.InvariantCulture)),
        ("Gender", g => g.Gender),
        ("E-mail", g => g.Email),
        ("Telefoon", g => g.Telefoon is null ? null : Telefoonnummer.Weergave(g.Telefoon)),
        ("Contactpersoon-ID", g => g.ContactpersoonId),
        ("Status", g => g.Status),
        ("Straat", g => g.Straat),
        ("Huisnummer", g => g.Huisnummer),
        ("Toevoeging", g => g.Toevoeging),
        ("Postcode", g => g.Postcode),
        ("Plaats", g => g.Plaats),
        ("Land", g => g.Land),
        ("Startdatum", g => g.Startdatum?.ToString(Datumnotatie, System.Globalization.CultureInfo.InvariantCulture)),
    ];
}

/// <summary>Een voogd zoals SOL3 hem levert (export 3). Volgnummer 1 of 2.</summary>
public sealed record VoogdGegevens(int Volgnummer, string? Naam, bool NaamOnbekend, string? Email, string? Telefoon, string? Mobiel);

/// <summary>Een functie zoals SOL3 hem levert (export 4).</summary>
public sealed record FunctieGegevens(
    string Groep, string Rol, Lidtype Lidtype, DateOnly? Startdatum, DateOnly? Einddatum, string? Status);
