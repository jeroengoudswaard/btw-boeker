using Verenigd.Application.Boekhouding;

namespace Verenigd.App.Diensten;

/// <summary>Afdrukken via het standaard Windows-afdrukvenster, dus ook naar PDF ("Microsoft Print to PDF", besluit 67).</summary>
public interface IAfdrukDienst
{
    /// <returns><c>false</c> als de gebruiker het afdrukken annuleert.</returns>
    bool Druk(RapportTabel tabel);
}

/// <summary>Wat de pagina's (Boekhouding, E-mail) nodig hebben van buiten de ViewModels.</summary>
public sealed record BoekhoudingHulpmiddelen(
    IWerkbladLezer Lezer, ISpreadsheetSchrijver Schrijver, IAfdrukDienst Afdruk, Application.Facturen.IFactuurPdfMaker Pdf, ViewModels.Woordenboek? Woordenboek = null,
    Application.Bijlagen.IPdfTekstLezer? Tekstlezer = null, Application.Email.IEmailVerzender? Verzender = null, Application.Email.IGoogleKoppeling? Koppeling = null,
    Application.Webapp.IWebappServer? Server = null)
{
    /// <summary>Het Supabase-project van de webapp (fase 7); zonder server is de webapp niet bereikbaar.</summary>
    public Application.Webapp.IWebappServer Webapp { get; } = Server ?? new GeenWebapp();

    /// <summary>Versturen via de Gmail-API (besluit 192); zonder verzender mislukt elk bericht met een melding.</summary>
    public Application.Email.IEmailVerzender Email { get; } = Verzender ?? new GeenVerzender();

    /// <summary>De eenmalige toestemming bij Google in de browser.</summary>
    public Application.Email.IGoogleKoppeling Google { get; } = Koppeling ?? new GeenVerzender();

    /// <summary>Leest datum en bedrag uit PDF-bonnen (besluit 96); zonder lezer vult de gebruiker ze zelf in.</summary>
    public Application.Bijlagen.IPdfTekstLezer PdfTekst { get; } = Tekstlezer ?? new GeenTekst();

    /// <summary>De woorden van de gekozen weergave, gedeeld door de hele app (besluit 127).</summary>
    public ViewModels.Woordenboek Woorden { get; } = Woordenboek ?? new ViewModels.Woordenboek();
}

internal sealed class GeenTekst : Application.Bijlagen.IPdfTekstLezer
{
    public string? LeesTekst(byte[] pdf) => null;
}

internal sealed class GeenVerzender : Application.Email.IEmailVerzender, Application.Email.IGoogleKoppeling
{
    private const string Melding = "Versturen van e-mail is in deze omgeving niet beschikbaar.";

    public Task VerstuurAsync(Domain.Communicatie.Emailinstellingen instellingen, Application.Email.Emailbericht bericht, CancellationToken annulering = default) =>
        throw new Application.Email.EmailVerzendException(Melding);

    public Task<string> KoppelAsync(string clientId, string clientGeheim, CancellationToken annulering = default) =>
        throw new Application.Email.EmailVerzendException(Melding);
}

internal sealed class GeenWebapp : Application.Webapp.IWebappServer
{
    private static Application.Webapp.WebappOnbereikbaarException Fout() => new("De webapp is in deze omgeving niet beschikbaar.");

    public Task<Application.Webapp.WebappSessie> InloggenAsync(Application.Webapp.Webappproject project, string email, string wachtwoord, string code, CancellationToken annulering = default) => throw Fout();

    public Task<Application.Webapp.WebappSessie> VernieuwAsync(Application.Webapp.Webappproject project, string vernieuwingssleutel, CancellationToken annulering = default) => throw Fout();

    public Task<Application.Webapp.Synchronisatie> SynchroniseerAsync(Application.Webapp.Webappproject project, Application.Webapp.WebappSessie sessie, Application.Webapp.Ledenkopie kopie, CancellationToken annulering = default) => throw Fout();

    public Task<IReadOnlyList<Application.Webapp.Voorstel>> HaalOpenVoorstellenAsync(Application.Webapp.Webappproject project, Application.Webapp.WebappSessie sessie, CancellationToken annulering = default) => throw Fout();

    public Task HandelAfAsync(Application.Webapp.Webappproject project, Application.Webapp.WebappSessie sessie, Guid voorstel, bool goedgekeurd, CancellationToken annulering = default) => throw Fout();

    public Task<IReadOnlyList<Application.Webapp.Leider>> HaalLeidingAsync(Application.Webapp.Webappproject project, Application.Webapp.WebappSessie sessie, CancellationToken annulering = default) => throw Fout();

    public Task<Guid> NodigUitAsync(Application.Webapp.Webappproject project, Application.Webapp.WebappSessie sessie, string email, string naam, IReadOnlyList<string> speltakken, CancellationToken annulering = default) => throw Fout();

    public Task ZetSpeltakkenAsync(Application.Webapp.Webappproject project, Application.Webapp.WebappSessie sessie, Guid leider, IReadOnlyList<string> speltakken, CancellationToken annulering = default) => throw Fout();

    public Task TrekInAsync(Application.Webapp.Webappproject project, Application.Webapp.WebappSessie sessie, Guid leider, CancellationToken annulering = default) => throw Fout();
}
