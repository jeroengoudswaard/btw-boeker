using Verenigd.Application.Database;
using Verenigd.Domain.Leden;
using Verenigd.Domain.Webapp;

namespace Verenigd.Application.Webapp;

public sealed record Synchronisatieresultaat(int Leden, int Verwijderd, int OpenVoorstellen);

/// <summary>Een voorstel zoals de wachtrij het toont (besluit 34): met de waarde die Verenigd nu heeft.</summary>
public sealed record Voorstelregel(Voorstel Voorstel, string Lidnaam, string Omschrijving, string? HuidigeWaarde, bool WijktAf, bool LidOnbekend);

/// <summary>
/// De webapp vanuit de desktop-app (besluiten 34, 38, 230–232): inloggen als beheerder met MFA, de ledenkopie
/// synchroniseren, voorstellen goedkeuren of afwijzen en leiding beheren. Werkt de rest van de app ook zonder internet.
/// </summary>
public sealed class WebappDienst(IDatabaseSessie sessie, IWebappServer server, TimeProvider tijd)
{
    private DateTime NuUtc => tijd.GetUtcNow().UtcDateTime;

    public Task<Webappinstellingen> LaadAsync(CancellationToken annulering = default) => sessie.Webapp.LaadAsync(annulering);

    public async Task BewaarProjectAsync(string projectadres, string publiekeSleutel, string beheerderEmail, CancellationToken annulering = default)
    {
        var instellingen = await sessie.Webapp.LaadAsync(annulering).ConfigureAwait(false);
        instellingen.WijzigProject(projectadres, publiekeSleutel, beheerderEmail);
        await sessie.Webapp.BewaarAsync(instellingen, annulering).ConfigureAwait(false);
    }

    public async Task InloggenAsync(string wachtwoord, string code, CancellationToken annulering = default)
    {
        var instellingen = await sessie.Webapp.LaadAsync(annulering).ConfigureAwait(false);
        var project = Project(instellingen);
        var nieuw = await server.InloggenAsync(project, instellingen.BeheerderEmail!, wachtwoord, code.Trim(), annulering).ConfigureAwait(false);
        instellingen.Koppel(nieuw.Vernieuwingssleutel);
        await sessie.Webapp.BewaarAsync(instellingen, annulering).ConfigureAwait(false);
    }

    public async Task OntkoppelAsync(CancellationToken annulering = default)
    {
        var instellingen = await sessie.Webapp.LaadAsync(annulering).ConfigureAwait(false);
        instellingen.Ontkoppel();
        await sessie.Webapp.BewaarAsync(instellingen, annulering).ConfigureAwait(false);
    }

    /// <summary>Besluiten 38 en 232: de ledenkopie naar de server en het aantal open voorstellen terug.</summary>
    public async Task<Synchronisatieresultaat> SynchroniseerAsync(CancellationToken annulering = default)
    {
        var instellingen = await sessie.Webapp.LaadAsync(annulering).ConfigureAwait(false);
        try
        {
            var (project, verbinding) = await VerbindAsync(instellingen, annulering).ConfigureAwait(false);
            var personen = await sessie.Leden.HaalAlleAsync(annulering).ConfigureAwait(false);
            var groepen = await sessie.Leden.HaalGroepenAsync(annulering).ConfigureAwait(false);
            var kopie = Webappkopie.Maak(personen, groepen, DateOnly.FromDateTime(NuUtc));
            var gesynchroniseerd = await server.SynchroniseerAsync(project, verbinding, kopie, annulering).ConfigureAwait(false);
            var voorstellen = await server.HaalOpenVoorstellenAsync(project, verbinding, annulering).ConfigureAwait(false);
            var resultaat = new Synchronisatieresultaat(gesynchroniseerd.Leden, gesynchroniseerd.Verwijderd, voorstellen.Count);
            instellingen.RegistreerSynchronisatie(NuUtc, true,
                $"{resultaat.Leden} leden gesynchroniseerd, {resultaat.OpenVoorstellen} voorstellen open.");
            await sessie.Webapp.BewaarAsync(instellingen, annulering).ConfigureAwait(false);
            return resultaat;
        }
        catch (WebappException ex)
        {
            instellingen.RegistreerSynchronisatie(NuUtc, false, ex.Message);
            await sessie.Webapp.BewaarAsync(instellingen, annulering).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<IReadOnlyList<Voorstelregel>> HaalVoorstellenAsync(CancellationToken annulering = default)
    {
        var (project, verbinding) = await VerbindAsync(annulering).ConfigureAwait(false);
        var voorstellen = await server.HaalOpenVoorstellenAsync(project, verbinding, annulering).ConfigureAwait(false);
        var personen = (await sessie.Leden.HaalAlleAsync(annulering).ConfigureAwait(false)).ToDictionary(p => p.Id);
        return [.. voorstellen.Select(v =>
        {
            if (!personen.TryGetValue(v.LidId, out var persoon) || !Leidingveld.IsGeldig(v.Veld))
            {
                return new Voorstelregel(v, "(onbekend lid)", Leidingveld.IsGeldig(v.Veld) ? Leidingveld.Omschrijving(v.Veld) : v.Veld, null, false, true);
            }

            var huidig = Leidingveld.HuidigeWaarde(persoon, v.Veld);
            return new Voorstelregel(v, persoon.VolledigeNaam, Leidingveld.Omschrijving(v.Veld), huidig,
                !string.Equals(Leeg(huidig), Leeg(v.Oud), StringComparison.Ordinal), false);
        })];
    }

    /// <summary>Eerst lokaal (bron "leiding"), daarna op de server; mislukt lokaal, dan blijft het voorstel open.</summary>
    public async Task KeurGoedAsync(Voorstel voorstel, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(voorstel);
        var (project, verbinding) = await VerbindAsync(annulering).ConfigureAwait(false);
        await sessie.Leden.NeemVoorstelOverAsync(voorstel.LidId, voorstel.Veld, voorstel.Nieuw, NuUtc, annulering).ConfigureAwait(false);
        await server.HandelAfAsync(project, verbinding, voorstel.Id, true, annulering).ConfigureAwait(false);
    }

    public async Task WijsAfAsync(Voorstel voorstel, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(voorstel);
        var (project, verbinding) = await VerbindAsync(annulering).ConfigureAwait(false);
        await server.HandelAfAsync(project, verbinding, voorstel.Id, false, annulering).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Leider>> HaalLeidingAsync(CancellationToken annulering = default)
    {
        var (project, verbinding) = await VerbindAsync(annulering).ConfigureAwait(false);
        return await server.HaalLeidingAsync(project, verbinding, annulering).ConfigureAwait(false);
    }

    public async Task<Guid> NodigUitAsync(string email, string naam, IReadOnlyList<string> speltakken, CancellationToken annulering = default)
    {
        var (project, verbinding) = await VerbindAsync(annulering).ConfigureAwait(false);
        return await server.NodigUitAsync(project, verbinding, email.Trim(), naam.Trim(), speltakken, annulering).ConfigureAwait(false);
    }

    public async Task ZetSpeltakkenAsync(Guid leider, IReadOnlyList<string> speltakken, CancellationToken annulering = default)
    {
        var (project, verbinding) = await VerbindAsync(annulering).ConfigureAwait(false);
        await server.ZetSpeltakkenAsync(project, verbinding, leider, speltakken, annulering).ConfigureAwait(false);
    }

    public async Task TrekInAsync(Guid leider, CancellationToken annulering = default)
    {
        var (project, verbinding) = await VerbindAsync(annulering).ConfigureAwait(false);
        await server.TrekInAsync(project, verbinding, leider, annulering).ConfigureAwait(false);
    }

    private static string? Leeg(string? waarde) => string.IsNullOrWhiteSpace(waarde) ? null : waarde.Trim();

    private static Webappproject Project(Webappinstellingen instellingen) =>
        instellingen.IsIngesteld
            ? new Webappproject(new Uri(instellingen.Projectadres!), instellingen.PubliekeSleutel!)
            : throw new WebappException("Vul eerst bij Instellingen › Webapp het projectadres, de publieke sleutel en uw e-mailadres in.");

    private async Task<(Webappproject, WebappSessie)> VerbindAsync(CancellationToken annulering) =>
        await VerbindAsync(await sessie.Webapp.LaadAsync(annulering).ConfigureAwait(false), annulering).ConfigureAwait(false);

    /// <summary>Vernieuwt de sessie met de bewaarde vernieuwingssleutel (die roteert); verlopen = ontkoppelen.</summary>
    private async Task<(Webappproject, WebappSessie)> VerbindAsync(Webappinstellingen instellingen, CancellationToken annulering)
    {
        var project = Project(instellingen);
        if (!instellingen.IsGekoppeld)
        {
            throw new WebappToegangException("Log eerst in bij Webapp › Synchroniseren (wachtwoord en code uit de authenticator-app).");
        }

        try
        {
            var verbinding = await server.VernieuwAsync(project, instellingen.Vernieuwingssleutel!, annulering).ConfigureAwait(false);
            instellingen.Koppel(verbinding.Vernieuwingssleutel);
            await sessie.Webapp.BewaarAsync(instellingen, annulering).ConfigureAwait(false);
            return (project, verbinding);
        }
        catch (WebappToegangException)
        {
            instellingen.Ontkoppel();
            await sessie.Webapp.BewaarAsync(instellingen, annulering).ConfigureAwait(false);
            throw;
        }
    }
}
