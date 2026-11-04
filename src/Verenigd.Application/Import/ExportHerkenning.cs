namespace Verenigd.Application.Import;

/// <summary>De vier eigen SOL3-exports (besluit 20).</summary>
public enum Sol3Export
{
    LedenBasis = 1,
    Huishouden = 2,
    Voogden = 3,
    Functies = 4,
}

/// <summary>Kolomnamen zoals SOL3 ze levert.</summary>
internal static class Kolom
{
    public const string Lidnummer = "Lidnummer";
    public const string Voornaam = "Voornaam";
    public const string Tussenvoegsel = "Tussenvoegsel";
    public const string Achternaam = "Achternaam";
    public const string Geboortedatum = "Geboortedatum";
    public const string Gender = "Gender";
    public const string Email = "E-mail";
    public const string Telefoon = "Telefoon";
    public const string Status = "Status";
    public const string ContactpersoonId = "Contactpersoon-ID";

    public const string Straat = "Lid straat";
    public const string Huisnummer = "Lid huisnummer";
    public const string Toevoeging = "Lid huisnr.toev.";
    public const string Postcode = "Postcode";
    public const string Plaats = "Lid plaats";
    public const string Land = "Land";
    public const string Iban = "Betaalprofiel IBAN";
    public const string Rekeninghouder = "Betaalprofiel rekeningshoudersnaam";

    public const string Voogd1Naam = "Voogd 1 naam";
    public const string Voogd1Email = "Voogd 1 e-mail";
    public const string Voogd1Telefoon = "Voogd 1 telefoon";
    public const string Voogd1Mobiel = "Voogd 1 mobiel";
    public const string Voogd2Naam = "Voogd 2 volledige naam";
    public const string Voogd2Email = "Voogd 2 e-mail";
    public const string Voogd2Telefoon = "Voogd 2: Telefoon";
    public const string Voogd2Mobiel = "Voogd 2 mobiel";

    public const string Rol = "Rol";
    public const string Groep = "Naam organisatie eenheid";
    public const string Startdatum = "Startdatum functie";
    public const string Einddatum = "Einddatum functie";
    public const string Lidtype = "Role Membership Type";
}

/// <summary>Herkent een export aan zijn kopregel; de volgorde van de kolommen doet er niet toe.</summary>
public static class ExportHerkenning
{
    private static readonly Dictionary<Sol3Export, string[]> Verplicht = new()
    {
        [Sol3Export.LedenBasis] =
        [
            Kolom.Lidnummer, Kolom.Voornaam, Kolom.Tussenvoegsel, Kolom.Achternaam, Kolom.Geboortedatum, Kolom.Gender,
            Kolom.Email, Kolom.Telefoon, Kolom.Startdatum, Kolom.Status, Kolom.ContactpersoonId,
        ],
        [Sol3Export.Huishouden] =
        [
            Kolom.Lidnummer, Kolom.ContactpersoonId, Kolom.Straat, Kolom.Huisnummer, Kolom.Toevoeging, Kolom.Postcode,
            Kolom.Plaats, Kolom.Land, Kolom.Iban, Kolom.Rekeninghouder,
        ],
        [Sol3Export.Voogden] =
        [
            Kolom.Lidnummer, Kolom.Voogd1Naam, Kolom.Voogd1Email, Kolom.Voogd1Telefoon, Kolom.Voogd1Mobiel,
            Kolom.Voogd2Naam, Kolom.Voogd2Email, Kolom.Voogd2Telefoon, Kolom.Voogd2Mobiel,
        ],
        [Sol3Export.Functies] =
        [
            Kolom.Lidnummer, Kolom.Rol, Kolom.Groep, Kolom.Startdatum, Kolom.Einddatum, Kolom.Lidtype, Kolom.Status,
        ],
    };

    public static Sol3Export? Herken(IReadOnlyCollection<string> kolommen)
    {
        ArgumentNullException.ThrowIfNull(kolommen);
        bool Heeft(string kolom) => kolommen.Contains(kolom, StringComparer.OrdinalIgnoreCase);

        if (Heeft(Kolom.Lidtype) && Heeft(Kolom.Rol))
        {
            return Sol3Export.Functies;
        }

        if (Heeft(Kolom.Voogd1Email) || Heeft(Kolom.Voogd1Naam))
        {
            return Sol3Export.Voogden;
        }

        if (Heeft(Kolom.Iban) || Heeft(Kolom.Straat))
        {
            return Sol3Export.Huishouden;
        }

        return Heeft(Kolom.Voornaam) && Heeft(Kolom.Achternaam) ? Sol3Export.LedenBasis : null;
    }

    /// <summary>De kolommen die Verenigd uit deze export gebruikt.</summary>
    public static IReadOnlyList<string> VerplichteKolommen(Sol3Export export) => Verplicht[export];

    public static IReadOnlyList<string> OntbrekendeKolommen(Sol3Export export, IReadOnlyCollection<string> kolommen) =>
        [.. Verplicht[export].Where(k => !kolommen.Contains(k, StringComparer.OrdinalIgnoreCase))];

    /// <summary>Bijvoorbeeld "export 4 (Functies)".</summary>
    public static string Omschrijving(Sol3Export export) => export switch
    {
        Sol3Export.LedenBasis => "export 1 (Leden-basis)",
        Sol3Export.Huishouden => "export 2 (Huishouden)",
        Sol3Export.Voogden => "export 3 (Voogden)",
        Sol3Export.Functies => "export 4 (Functies)",
        _ => throw new ArgumentOutOfRangeException(nameof(export), export, null),
    };
}
