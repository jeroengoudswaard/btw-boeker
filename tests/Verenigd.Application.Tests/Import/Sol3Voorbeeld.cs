using System.Text;

namespace Verenigd.Application.Tests.Import;

/// <summary>
/// Synthetische SOL3-exports met de echte kopregels (besluit 20) en verzonnen gegevens. Nooit echte leden gebruiken.
/// </summary>
internal static class Sol3Voorbeeld
{
    public const string KopLeden = "Lidnummer;Voornaam;Tussenvoegsel;Achternaam;Geboortedatum;Gender;E-mail;Telefoon;Startdatum functie;Status;Contactpersoon-ID;Organisatie naam;Organisatie plaats;Naam organisatie eenheid";
    public const string KopHuishouden = "Lidnummer;Contactpersoon-ID;Lid straat;Lid huisnummer;Lid huisnr.toev.;Postcode;Lid plaats;Land;Betaalprofiel IBAN;Betaalprofiel rekeningshoudersnaam;Organisatie naam;Organisatie plaats;Naam organisatie eenheid";
    public const string KopVoogden = "Lidnummer;Voogd 1 naam;Voogd 1 e-mail;Voogd 1 telefoon;Voogd 2 volledige naam;Voogd 2 e-mail;Voogd 2: Telefoon;Voogd 1 mobiel;Voogd 2 mobiel;Organisatie naam;Organisatie plaats;Naam organisatie eenheid";
    public const string KopFuncties = "Rol;Naam organisatie eenheid;Startdatum functie;Einddatum functie;Role Membership Type;Status;Organisatie naam;Organisatie plaats;Lidnummer";

    public static readonly Encoding Windows1252 = MaakWindows1252();

    /// <summary>Een CSV zoals SOL3 hem levert: <c>;</c>-gescheiden, alle velden tussen aanhalingstekens, Windows-1252.</summary>
    public static byte[] Csv(string kopregel, params string[][] regels)
    {
        var tekst = new StringBuilder();
        tekst.Append(Regel(kopregel.Split(';'))).Append("\r\n");
        foreach (var regel in regels)
        {
            tekst.Append(Regel(regel)).Append("\r\n");
        }

        return Windows1252.GetBytes(tekst.ToString());
    }

    public static string[] Lid(
        string lidnummer, string voornaam = "Anna", string achternaam = "Vries", string tussenvoegsel = "de",
        string geboortedatum = "01-03-2015", string email = "anna@example.org", string telefoon = "06-12345678",
        string status = "Actief", string contactpersoonId = "", string eenheid = "Welpen") =>
        [lidnummer, voornaam, tussenvoegsel, achternaam, geboortedatum, "Vrouw", email, telefoon, "19-09-2025", status,
            contactpersoonId.Length > 0 ? contactpersoonId : "003Ab0000" + lidnummer[^6..], "Testgroep", "Ergens", eenheid];

    public static string[] Huishouden(
        string lidnummer, string iban = "", string rekeninghouder = "", string postcode = "1234ab", string land = "Nederland",
        string contactpersoonId = "") =>
        [lidnummer, contactpersoonId.Length > 0 ? contactpersoonId : "003Ab0000" + lidnummer[^6..], "Dorpsstraat", "1", "",
            postcode, "ERGENS", land, iban, rekeninghouder, "Testgroep", "Ergens", "Welpen"];

    public static string[] Voogden(
        string lidnummer, string naam1 = "P. de Vries", string email1 = "p@example.org", string naam2 = "", string email2 = "") =>
        [lidnummer, naam1, email1, "070-1234567", naam2, email2, "", "0612345678", "", "Testgroep", "Ergens", "Welpen"];

    public static string[] Functie(
        string lidnummer, string rol = "Lid", string groep = "Welpen", string start = "19-09-2025", string eind = "",
        string type = "Jeugdlid", string status = "Actief") =>
        [rol, groep, start, eind, type, status, "Testgroep", "Ergens", lidnummer];

    private static string Regel(IEnumerable<string> velden) =>
        string.Join(';', velden.Select(v => "\"" + v.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""));

    private static Encoding MaakWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
