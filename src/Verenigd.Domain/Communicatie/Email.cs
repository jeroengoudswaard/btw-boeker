using System.Net.Mail;

namespace Verenigd.Domain.Communicatie;

/// <summary>Waarvoor een sjabloon dient (besluit 191); bepaalt welke invulvelden er zijn.</summary>
public enum Emailsoort
{
    Vrij = 1,
    Welkom = 2,
    Vooraankondiging = 3,
    Stornering = 4,
    Factuur = 5,
    Herinnering = 6,
}

public enum Verzendstatus
{
    Verzonden = 1,
    Mislukt = 2,
}

public static class Emailadres
{
    public static bool IsGeldig(string? adres)
    {
        var schoon = adres?.Trim();
        return !string.IsNullOrEmpty(schoon) && !schoon.Contains(' ', StringComparison.Ordinal)
            && MailAddress.TryCreate(schoon, out var mail) && mail.Host.Contains('.', StringComparison.Ordinal);
    }

    /// <summary>Voor ontdubbelen (besluit 31): zonder spaties, hoofdletterongevoelig.</summary>
    public static string Sleutel(string adres)
    {
        ArgumentNullException.ThrowIfNull(adres);
        return adres.Trim().ToUpperInvariant();
    }
}

/// <summary>Een e-mailsjabloon met invulvelden zoals <c>{aanhef}</c> (besluit 191). Standaardsjablonen zijn aan te passen, niet te verwijderen.</summary>
public sealed class Emailsjabloon
{
    public const int MaximaleNaamLengte = 100;
    public const int MaximaleOnderwerpLengte = 200;

    private Emailsjabloon()
    {
        Naam = null!;
        Onderwerp = null!;
        Tekst = null!;
    }

    public Emailsjabloon(Emailsoort soort, string naam, string onderwerp, string tekst)
        : this()
    {
        Soort = soort;
        Wijzig(naam, onderwerp, tekst);
    }

    public int Id { get; private set; }

    public Emailsoort Soort { get; private set; }

    public string Naam { get; private set; }

    public string Onderwerp { get; private set; }

    public string Tekst { get; private set; }

    public bool IsStandaard { get; private set; }

    public static Emailsjabloon Standaard(Emailsoort soort, string naam, string onderwerp, string tekst) =>
        new(soort, naam, onderwerp, tekst) { IsStandaard = true };

    public void Wijzig(string naam, string onderwerp, string tekst)
    {
        Naam = Verplicht(naam, MaximaleNaamLengte, nameof(naam));
        Onderwerp = Verplicht(onderwerp, MaximaleOnderwerpLengte, nameof(onderwerp));
        Tekst = tekst ?? string.Empty;
    }

    private static string Verplicht(string waarde, int maximum, string naam)
    {
        var schoon = waarde?.Trim();
        if (string.IsNullOrEmpty(schoon))
        {
            throw new ArgumentException("Vul dit veld in.", naam);
        }

        return schoon.Length <= maximum ? schoon : throw new ArgumentException($"Maximaal {maximum} tekens.", naam);
    }
}

/// <summary>Eén regel in het logboek (besluit 195): aan wie, wanneer, onderwerp en sjabloon; nooit de tekst zelf.</summary>
public sealed class VerzondenBericht
{
    private VerzondenBericht()
    {
        Naam = null!;
        Adres = null!;
        Onderwerp = null!;
        Sjabloon = null!;
    }

    public long Id { get; private set; }

    public DateTime TijdstipUtc { get; private set; }

    public string Naam { get; private set; }

    public string Adres { get; private set; }

    public string Onderwerp { get; private set; }

    public string Sjabloon { get; private set; }

    public Verzendstatus Status { get; private set; }

    /// <summary>De reden van een mislukte verzending, zonder persoonsgegevens.</summary>
    public string? Fout { get; private set; }

    public static VerzondenBericht Gelukt(string naam, string adres, string onderwerp, string sjabloon, DateTime nuUtc) =>
        Maak(naam, adres, onderwerp, sjabloon, Verzendstatus.Verzonden, null, nuUtc);

    public static VerzondenBericht Mislukt(string naam, string adres, string onderwerp, string sjabloon, string fout, DateTime nuUtc) =>
        Maak(naam, adres, onderwerp, sjabloon, Verzendstatus.Mislukt, fout, nuUtc);

    private static VerzondenBericht Maak(string naam, string adres, string onderwerp, string sjabloon, Verzendstatus status, string? fout, DateTime nuUtc) =>
        new()
        {
            Naam = naam ?? string.Empty,
            Adres = adres ?? string.Empty,
            Onderwerp = onderwerp ?? string.Empty,
            Sjabloon = sjabloon ?? string.Empty,
            Status = status,
            Fout = fout,
            TijdstipUtc = nuUtc,
        };
}

/// <summary>
/// De koppeling met Gmail (besluiten 192 en 193). Staat in de versleutelde database: client-gegevens van het eigen Google
/// Cloud-project en de vernieuwingssleutel; nooit in Git of in logbestanden.
/// </summary>
public sealed class Emailinstellingen
{
    public const int VasteId = 1;

    public int Id { get; private set; } = VasteId;

    public string? Afzendernaam { get; private set; }

    public string? Afzenderadres { get; private set; }

    public string? ClientId { get; private set; }

    public string? ClientGeheim { get; private set; }

    public string? Vernieuwingssleutel { get; private set; }

    public DateTime? GekoppeldOpUtc { get; private set; }

    public bool HeeftClient => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientGeheim);

    public bool IsGekoppeld => HeeftClient && !string.IsNullOrEmpty(Vernieuwingssleutel);

    public void ZetAfzender(string naam, string adres)
    {
        if (!Emailadres.IsGeldig(adres))
        {
            throw new ArgumentException("Vul een geldig e-mailadres in.", nameof(adres));
        }

        Afzendernaam = string.IsNullOrWhiteSpace(naam) ? null : naam.Trim();
        Afzenderadres = adres.Trim();
    }

    public void ZetClient(string clientId, string clientGeheim)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientGeheim);
        if (clientId != ClientId)
        {
            Ontkoppel(); // een andere client vraagt om een nieuwe koppeling
        }

        ClientId = clientId.Trim();
        ClientGeheim = clientGeheim.Trim();
    }

    public void Koppel(string vernieuwingssleutel, DateTime nuUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vernieuwingssleutel);
        Vernieuwingssleutel = vernieuwingssleutel;
        GekoppeldOpUtc = nuUtc;
    }

    public void Ontkoppel()
    {
        Vernieuwingssleutel = null;
        GekoppeldOpUtc = null;
    }
}
