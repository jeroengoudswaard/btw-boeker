using Verenigd.Domain.Webapp;

namespace Verenigd.Application.Webapp;

// ===== De ledenkopie voor de server (besluiten 35, 38 en 229): geen adres, niets financieels =====

public sealed record Kopievoogd(int Volgnummer, string? Naam, string? Email, string? Telefoon, string? Mobiel);

public sealed record Kopiefunctie(string Speltak, string Rol, bool IsLeiding);

public sealed record Kopielid(
    Guid Id, string Lidnummer, string Voornaam, string? Tussenvoegsel, string Achternaam, DateOnly? Geboortedatum,
    string? Email, string? Telefoon, DateTime LaatstGewijzigdUtc, IReadOnlyList<Kopievoogd> Voogden, IReadOnlyList<Kopiefunctie> Functies);

public sealed record Ledenkopie(IReadOnlyList<string> Speltakken, IReadOnlyList<Kopielid> Leden);

// ===== Wat van de server komt =====

/// <summary>Een open voorstel van de leiding (besluit 34).</summary>
public sealed record Voorstel(Guid Id, Guid LidId, string Veld, string? Oud, string? Nieuw, string IngediendDoor, DateTime IngediendOpUtc);

public sealed record Leider(Guid Id, string Naam, string Email, IReadOnlyList<string> Speltakken);

public sealed record Webappproject(Uri Adres, string PubliekeSleutel);

/// <summary>Een beheerderssessie met MFA (aal2).</summary>
public sealed record WebappSessie(string Toegangssleutel, string Vernieuwingssleutel);

public sealed record Synchronisatie(int Leden, int Verwijderd);

/// <summary>Het Supabase-project, via de API's van Supabase (Auth, PostgREST en de Edge Function "leiding").</summary>
public interface IWebappServer
{
    /// <summary>Wachtwoord en daarna de TOTP-code (besluit 231); geeft een sessie met MFA.</summary>
    Task<WebappSessie> InloggenAsync(Webappproject project, string email, string wachtwoord, string code, CancellationToken annulering = default);

    Task<WebappSessie> VernieuwAsync(Webappproject project, string vernieuwingssleutel, CancellationToken annulering = default);

    Task<Synchronisatie> SynchroniseerAsync(Webappproject project, WebappSessie sessie, Ledenkopie kopie, CancellationToken annulering = default);

    Task<IReadOnlyList<Voorstel>> HaalOpenVoorstellenAsync(Webappproject project, WebappSessie sessie, CancellationToken annulering = default);

    Task HandelAfAsync(Webappproject project, WebappSessie sessie, Guid voorstel, bool goedgekeurd, CancellationToken annulering = default);

    Task<IReadOnlyList<Leider>> HaalLeidingAsync(Webappproject project, WebappSessie sessie, CancellationToken annulering = default);

    Task<Guid> NodigUitAsync(Webappproject project, WebappSessie sessie, string email, string naam, IReadOnlyList<string> speltakken, CancellationToken annulering = default);

    Task ZetSpeltakkenAsync(Webappproject project, WebappSessie sessie, Guid leider, IReadOnlyList<string> speltakken, CancellationToken annulering = default);

    Task TrekInAsync(Webappproject project, WebappSessie sessie, Guid leider, CancellationToken annulering = default);
}

/// <summary>De instellingen van de webapp, in de versleutelde database.</summary>
public interface IWebappOpslag
{
    Task<Webappinstellingen> LaadAsync(CancellationToken annulering = default);

    Task BewaarAsync(Webappinstellingen instellingen, CancellationToken annulering = default);
}

/// <summary>Een fout bij de webapp, met een melding die de gebruiker kan lezen (zonder persoonsgegevens).</summary>
public class WebappException : Exception
{
    public WebappException()
    {
    }

    public WebappException(string message)
        : base(message)
    {
    }

    public WebappException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Geen internet, of het project is gepauzeerd (besluit 228).</summary>
public sealed class WebappOnbereikbaarException : WebappException
{
    public WebappOnbereikbaarException()
    {
    }

    public WebappOnbereikbaarException(string message)
        : base(message)
    {
    }

    public WebappOnbereikbaarException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Inloggen mislukt of de sessie is verlopen: opnieuw inloggen.</summary>
public sealed class WebappToegangException : WebappException
{
    public WebappToegangException()
    {
    }

    public WebappToegangException(string message)
        : base(message)
    {
    }

    public WebappToegangException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
