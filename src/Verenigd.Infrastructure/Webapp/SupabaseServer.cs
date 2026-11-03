using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Verenigd.Application.Webapp;

namespace Verenigd.Infrastructure.Webapp;

/// <summary>
/// Het Supabase-project van de webapp via de HTTP-API's: Auth (wachtwoord + TOTP, besluit 231), PostgREST (tabellen en
/// functies, met de beveiligingsregels van de server) en de Edge Function "leiding" (besluit 230).
/// Alleen de publieke sleutel en de sessie van de beheerder; de service_role-sleutel nooit (besluit 37).
/// Meldingen bevatten geen persoonsgegevens.
/// </summary>
public sealed class SupabaseServer(HttpClient http) : IWebappServer
{
    private const string Onbereikbaar =
        "Geen verbinding met de webapp. Controleer de internetverbinding. Gratis Supabase-projecten pauzeren na 7 dagen zonder gebruik: zet het project dan weer aan op supabase.com.";

    public async Task<WebappSessie> InloggenAsync(Webappproject project, string email, string wachtwoord, string code, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        var inlog = await VerstuurAsync(project, HttpMethod.Post, "auth/v1/token?grant_type=password", null, new { email, password = wachtwoord }, annulering,
            bijFout: (status, _) => status is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                ? new WebappToegangException("Het e-mailadres of wachtwoord klopt niet.")
                : null).ConfigureAwait(false);
        var toegang = Tekst(inlog, "access_token");
        var factor = (inlog["user"]?["factors"] as JsonArray ?? [])
            .FirstOrDefault(f => (string?)f?["factor_type"] == "totp" && (string?)f?["status"] == "verified")?["id"]?.GetValue<string>()
            ?? throw new WebappToegangException("Stel eerst de authenticator-app in: log één keer in op de webapp en volg de stappen.");

        var uitdaging = await VerstuurAsync(project, HttpMethod.Post, $"auth/v1/factors/{factor}/challenge", toegang, new { }, annulering).ConfigureAwait(false);
        var bevestigd = await VerstuurAsync(project, HttpMethod.Post, $"auth/v1/factors/{factor}/verify", toegang,
            new { challenge_id = Tekst(uitdaging, "id"), code }, annulering,
            bijFout: (status, _) => status is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
                ? new WebappToegangException("De code klopt niet of is verlopen. Probeer de nieuwe code uit de app.")
                : null).ConfigureAwait(false);
        return new WebappSessie(Tekst(bevestigd, "access_token"), Tekst(bevestigd, "refresh_token"));
    }

    public async Task<WebappSessie> VernieuwAsync(Webappproject project, string vernieuwingssleutel, CancellationToken annulering = default)
    {
        var antwoord = await VerstuurAsync(project, HttpMethod.Post, "auth/v1/token?grant_type=refresh_token", null, new { refresh_token = vernieuwingssleutel }, annulering,
            bijFout: (status, _) => status is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                ? new WebappToegangException("De sessie is verlopen. Log opnieuw in.")
                : null).ConfigureAwait(false);
        return new WebappSessie(Tekst(antwoord, "access_token"), Tekst(antwoord, "refresh_token"));
    }

    public async Task<Synchronisatie> SynchroniseerAsync(Webappproject project, WebappSessie sessie, Ledenkopie kopie, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(sessie);
        ArgumentNullException.ThrowIfNull(kopie);
        var gegevens = new
        {
            speltakken = kopie.Speltakken,
            leden = kopie.Leden.Select(l => new
            {
                id = l.Id,
                lidnummer = l.Lidnummer,
                voornaam = l.Voornaam,
                tussenvoegsel = l.Tussenvoegsel,
                achternaam = l.Achternaam,
                geboortedatum = l.Geboortedatum?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                email = l.Email,
                telefoon = l.Telefoon,
                laatst_gewijzigd = DateTime.SpecifyKind(l.LaatstGewijzigdUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
                voogden = l.Voogden.Select(v => new { volgnummer = v.Volgnummer, naam = v.Naam, email = v.Email, telefoon = v.Telefoon, mobiel = v.Mobiel }),
                functies = l.Functies.Select(f => new { speltak = f.Speltak, rol = f.Rol, is_leiding = f.IsLeiding }),
            }),
        };
        var antwoord = await VerstuurAsync(project, HttpMethod.Post, "rest/v1/rpc/synchroniseer_leden", sessie.Toegangssleutel, new { gegevens }, annulering).ConfigureAwait(false);
        return new Synchronisatie(antwoord["leden"]?.GetValue<int>() ?? 0, antwoord["verwijderd"]?.GetValue<int>() ?? 0);
    }

    public async Task<IReadOnlyList<Voorstel>> HaalOpenVoorstellenAsync(Webappproject project, WebappSessie sessie, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(sessie);
        var antwoord = await VerstuurAsync(project, HttpMethod.Get,
            "rest/v1/voorstellen?select=id,lid_id,veld,oud,nieuw,ingediend_op,leiding(naam)&status=eq.open&order=ingediend_op.asc",
            sessie.Toegangssleutel, null, annulering).ConfigureAwait(false);
        return [.. (antwoord as JsonArray ?? []).Select(v => new Voorstel(
            Guid.Parse(Tekst(v!, "id")), Guid.Parse(Tekst(v!, "lid_id")), Tekst(v!, "veld"),
            (string?)v!["oud"], (string?)v!["nieuw"], (string?)v!["leiding"]?["naam"] ?? "(onbekend)",
            DateTimeOffset.Parse(Tekst(v!, "ingediend_op"), CultureInfo.InvariantCulture).UtcDateTime))];
    }

    public Task HandelAfAsync(Webappproject project, WebappSessie sessie, Guid voorstel, bool goedgekeurd, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(sessie);
        return VerstuurAsync(project, HttpMethod.Post, "rest/v1/rpc/handel_voorstel_af", sessie.Toegangssleutel,
            new { voorstel, nieuwe_status = goedgekeurd ? "goedgekeurd" : "afgewezen" }, annulering);
    }

    public async Task<IReadOnlyList<Leider>> HaalLeidingAsync(Webappproject project, WebappSessie sessie, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(sessie);
        var antwoord = await VerstuurAsync(project, HttpMethod.Get, "rest/v1/leiding?select=user_id,naam,email,leiding_speltakken(speltak)&order=naam.asc",
            sessie.Toegangssleutel, null, annulering).ConfigureAwait(false);
        return [.. (antwoord as JsonArray ?? []).Select(l => new Leider(
            Guid.Parse(Tekst(l!, "user_id")), Tekst(l!, "naam"), Tekst(l!, "email"),
            [.. (l!["leiding_speltakken"] as JsonArray ?? []).Select(s => (string)s!["speltak"]!).Order(StringComparer.Ordinal)]))];
    }

    public async Task<Guid> NodigUitAsync(Webappproject project, WebappSessie sessie, string email, string naam, IReadOnlyList<string> speltakken, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(sessie);
        var antwoord = await VerstuurAsync(project, HttpMethod.Post, "functions/v1/leiding", sessie.Toegangssleutel,
            new { actie = "uitnodigen", email, naam, speltakken }, annulering).ConfigureAwait(false);
        return Guid.Parse(Tekst(antwoord, "id"));
    }

    public Task ZetSpeltakkenAsync(Webappproject project, WebappSessie sessie, Guid leider, IReadOnlyList<string> speltakken, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(sessie);
        return VerstuurAsync(project, HttpMethod.Post, "rest/v1/rpc/zet_leiding_speltakken", sessie.Toegangssleutel, new { gebruiker = leider, speltakken }, annulering);
    }

    public Task TrekInAsync(Webappproject project, WebappSessie sessie, Guid leider, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(sessie);
        return VerstuurAsync(project, HttpMethod.Post, "functions/v1/leiding", sessie.Toegangssleutel, new { actie = "intrekken", id = leider }, annulering);
    }

    private static string Tekst(JsonNode node, string naam) =>
        (string?)node[naam] ?? throw new WebappException("Onverwacht antwoord van de webapp.");

    private async Task<JsonNode> VerstuurAsync(
        Webappproject project, HttpMethod methode, string pad, string? toegang, object? inhoud, CancellationToken annulering,
        Func<HttpStatusCode, JsonNode?, WebappException?>? bijFout = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        using var verzoek = new HttpRequestMessage(methode, new Uri(project.Adres, pad));
        verzoek.Headers.Add("apikey", project.PubliekeSleutel);
        if (toegang is not null)
        {
            verzoek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", toegang);
        }

        if (inhoud is not null)
        {
            verzoek.Content = JsonContent.Create(inhoud);
        }

        HttpResponseMessage antwoord;
        try
        {
            antwoord = await http.SendAsync(verzoek, annulering).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new WebappOnbereikbaarException(Onbereikbaar, ex);
        }
        catch (TaskCanceledException ex) when (!annulering.IsCancellationRequested)
        {
            throw new WebappOnbereikbaarException(Onbereikbaar, ex);
        }

        using (antwoord)
        {
            var tekst = await antwoord.Content.ReadAsStringAsync(annulering).ConfigureAwait(false);
            JsonNode? json = null;
            try
            {
                json = string.IsNullOrWhiteSpace(tekst) ? null : JsonNode.Parse(tekst);
            }
            catch (JsonException)
            {
                json = null;
            }

            if (antwoord.IsSuccessStatusCode)
            {
                return json ?? new JsonObject();
            }

            if (bijFout?.Invoke(antwoord.StatusCode, json) is { } eigen)
            {
                throw eigen;
            }

            var status = (int)antwoord.StatusCode;
            throw status switch
            {
                401 or 403 => new WebappToegangException("Geen toegang (meer). Log opnieuw in als beheerder met de code uit de authenticator-app."),
                >= 500 => new WebappOnbereikbaarException(Onbereikbaar),
                _ when (string?)json?["fout"] is { } melding => new WebappException(melding),
                _ => new WebappException($"De webapp weigerde de opdracht (code {status})."),
            };
        }
    }
}
