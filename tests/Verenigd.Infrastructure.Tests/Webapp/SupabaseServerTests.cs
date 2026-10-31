using System.Net;
using System.Text;
using System.Text.Json;
using Verenigd.Application.Webapp;
using Verenigd.Infrastructure.Webapp;

namespace Verenigd.Infrastructure.Tests.Webapp;

/// <summary>De aanroepen naar Supabase (Auth, PostgREST, Edge Function), met een nep-HTTP-handler.</summary>
public sealed class SupabaseServerTests : IDisposable
{
    private static readonly Webappproject Project = new(new Uri("https://abcd.supabase.co"), "sb_publishable_x");
    private static readonly WebappSessie Sessie = new("toegang", "vernieuw");

    private readonly NepHandler _handler = new();
    private readonly HttpClient _http;
    private readonly SupabaseServer _server;

    public SupabaseServerTests()
    {
        _http = new HttpClient(_handler);
        _server = new SupabaseServer(_http);
    }

    public void Dispose()
    {
        _http.Dispose();
        _handler.Dispose();
    }

    [Fact]
    public async Task Inloggen_met_wachtwoord_daarna_totp_challenge_en_verify()
    {
        _handler.Antwoorden.Enqueue((HttpStatusCode.OK, """{"access_token":"aal1","refresh_token":"r1","user":{"id":"u","factors":[{"id":"f-oud","factor_type":"totp","status":"unverified"},{"id":"f1","factor_type":"totp","status":"verified"}]}}"""));
        _handler.Antwoorden.Enqueue((HttpStatusCode.OK, """{"id":"c1"}"""));
        _handler.Antwoorden.Enqueue((HttpStatusCode.OK, """{"access_token":"aal2","refresh_token":"r2"}"""));

        var sessie = await _server.InloggenAsync(Project, "beheer@example.org", "geheim", "123456");

        Assert.Equal(new WebappSessie("aal2", "r2"), sessie);
        var (wachtwoord, challenge, verify) = (_handler.Verzoeken[0], _handler.Verzoeken[1], _handler.Verzoeken[2]);
        Assert.Equal("https://abcd.supabase.co/auth/v1/token?grant_type=password", wachtwoord.Url);
        Assert.Equal("sb_publishable_x", wachtwoord.Apikey);
        Assert.Equal("beheer@example.org", wachtwoord.Json!.RootElement.GetProperty("email").GetString());
        Assert.Equal("https://abcd.supabase.co/auth/v1/factors/f1/challenge", challenge.Url);
        Assert.Equal("Bearer aal1", challenge.Autorisatie);
        Assert.Equal("https://abcd.supabase.co/auth/v1/factors/f1/verify", verify.Url);
        Assert.Equal("c1", verify.Json!.RootElement.GetProperty("challenge_id").GetString());
        Assert.Equal("123456", verify.Json!.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Verkeerd_wachtwoord_geeft_een_toegangsfout()
    {
        _handler.Antwoorden.Enqueue((HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"Invalid login credentials"}"""));

        var fout = await Assert.ThrowsAsync<WebappToegangException>(() => _server.InloggenAsync(Project, "b@example.org", "fout", "123456"));
        Assert.Contains("wachtwoord", fout.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Zonder_authenticator_app_eerst_mfa_instellen()
    {
        _handler.Antwoorden.Enqueue((HttpStatusCode.OK, """{"access_token":"aal1","refresh_token":"r1","user":{"id":"u","factors":[]}}"""));

        var fout = await Assert.ThrowsAsync<WebappToegangException>(() => _server.InloggenAsync(Project, "b@example.org", "geheim", "123456"));
        Assert.Contains("authenticator", fout.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verkeerde_code_geeft_een_toegangsfout()
    {
        _handler.Antwoorden.Enqueue((HttpStatusCode.OK, """{"access_token":"aal1","refresh_token":"r1","user":{"id":"u","factors":[{"id":"f1","factor_type":"totp","status":"verified"}]}}"""));
        _handler.Antwoorden.Enqueue((HttpStatusCode.OK, """{"id":"c1"}"""));
        _handler.Antwoorden.Enqueue((HttpStatusCode.UnprocessableEntity, """{"code":"mfa_verification_failed"}"""));

        var fout = await Assert.ThrowsAsync<WebappToegangException>(() => _server.InloggenAsync(Project, "b@example.org", "geheim", "000000"));
        Assert.Contains("code", fout.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vernieuwen_met_de_vernieuwingssleutel()
    {
        _handler.Antwoorden.Enqueue((HttpStatusCode.OK, """{"access_token":"a3","refresh_token":"r3"}"""));

        var sessie = await _server.VernieuwAsync(Project, "r2");

        Assert.Equal(new WebappSessie("a3", "r3"), sessie);
        Assert.Equal("https://abcd.supabase.co/auth/v1/token?grant_type=refresh_token", _handler.Verzoeken[0].Url);
        Assert.Equal("r2", _handler.Verzoeken[0].Json!.RootElement.GetProperty("refresh_token").GetString());
    }

    [Fact]
    public async Task Geen_verbinding_of_gepauzeerd_project_is_onbereikbaar()
    {
        _handler.Fout = new HttpRequestException("dns");
        var fout = await Assert.ThrowsAsync<WebappOnbereikbaarException>(() => _server.VernieuwAsync(Project, "r2"));
        Assert.Contains("pauzeren", fout.Message, StringComparison.Ordinal);

        _handler.Fout = null;
        _handler.Antwoorden.Enqueue((HttpStatusCode.ServiceUnavailable, "{}"));
        await Assert.ThrowsAsync<WebappOnbereikbaarException>(() => _server.VernieuwAsync(Project, "r2"));
    }

    [Fact]
    public async Task Synchroniseren_stuurt_de_kopie_in_snake_case_naar_de_rpc()
    {
        _handler.Antwoorden.Enqueue((HttpStatusCode.OK, """{"leden":1,"verwijderd":2}"""));
        var id = Guid.Parse("10000000-0000-4000-8000-000000000001");
        var kopie = new Ledenkopie(["Welpen"], [new Kopielid(id, "123", "Bram", "de", "Jong", new DateOnly(2018, 3, 1), "b@example.org", "+31612345678",
            new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc), [new Kopievoogd(1, null, "v@example.org", null, "+31611111111")], [new Kopiefunctie("Welpen", "Jeugdlid", false)])]);

        var resultaat = await _server.SynchroniseerAsync(Project, Sessie, kopie);

        Assert.Equal(new Synchronisatie(1, 2), resultaat);
        var verzoek = _handler.Verzoeken[0];
        Assert.Equal("https://abcd.supabase.co/rest/v1/rpc/synchroniseer_leden", verzoek.Url);
        Assert.Equal("Bearer toegang", verzoek.Autorisatie);
        var lid = verzoek.Json!.RootElement.GetProperty("gegevens").GetProperty("leden")[0];
        Assert.Equal(id.ToString(), lid.GetProperty("id").GetString());
        Assert.Equal("2018-03-01", lid.GetProperty("geboortedatum").GetString());
        Assert.Equal("2026-10-31T12:00:00.0000000Z", lid.GetProperty("laatst_gewijzigd").GetString());
        Assert.Equal(JsonValueKind.Null, lid.GetProperty("voogden")[0].GetProperty("naam").ValueKind);
        Assert.False(lid.GetProperty("functies")[0].GetProperty("is_leiding").GetBoolean());
        Assert.Equal("Welpen", verzoek.Json!.RootElement.GetProperty("gegevens").GetProperty("speltakken")[0].GetString());
    }

    [Fact]
    public async Task Open_voorstellen_met_naam_van_de_leider()
    {
        _handler.Antwoorden.Enqueue((HttpStatusCode.OK, """[{"id":"20000000-0000-4000-8000-000000000001","lid_id":"10000000-0000-4000-8000-000000000001","veld":"email","oud":"a@example.org","nieuw":"b@example.org","ingediend_op":"2026-10-31T20:41:00+00:00","leiding":{"naam":"Akela"}}]"""));

        var voorstel = Assert.Single(await _server.HaalOpenVoorstellenAsync(Project, Sessie));

        Assert.Equal("Akela", voorstel.IngediendDoor);
        Assert.Equal("email", voorstel.Veld);
        Assert.Equal(new DateTime(2026, 10, 31, 20, 41, 0, DateTimeKind.Utc), voorstel.IngediendOpUtc);
        Assert.StartsWith("https://abcd.supabase.co/rest/v1/voorstellen?select=", _handler.Verzoeken[0].Url, StringComparison.Ordinal);
        Assert.Contains("status=eq.open", _handler.Verzoeken[0].Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Afhandelen_via_de_rpc()
    {
        _handler.Antwoorden.Enqueue((HttpStatusCode.NoContent, ""));
        var id = Guid.NewGuid();

        await _server.HandelAfAsync(Project, Sessie, id, goedgekeurd: false);

        var json = _handler.Verzoeken[0].Json!.RootElement;
        Assert.Equal("https://abcd.supabase.co/rest/v1/rpc/handel_voorstel_af", _handler.Verzoeken[0].Url);
        Assert.Equal(id.ToString(), json.GetProperty("voorstel").GetString());
        Assert.Equal("afgewezen", json.GetProperty("nieuwe_status").GetString());
    }

    [Fact]
    public async Task Leiding_met_speltakken()
    {
        _handler.Antwoorden.Enqueue((HttpStatusCode.OK, """[{"user_id":"30000000-0000-4000-8000-000000000001","naam":"Akela","email":"akela@example.org","leiding_speltakken":[{"speltak":"Welpen"}]}]"""));

        var leider = Assert.Single(await _server.HaalLeidingAsync(Project, Sessie));

        Assert.Equal((Guid.Parse("30000000-0000-4000-8000-000000000001"), "Akela", "akela@example.org"), (leider.Id, leider.Naam, leider.Email));
        Assert.Equal(["Welpen"], leider.Speltakken);
    }

    [Fact]
    public async Task Uitnodigen_via_de_edge_function_met_foutmelding_van_de_functie()
    {
        _handler.Antwoorden.Enqueue((HttpStatusCode.OK, """{"id":"30000000-0000-4000-8000-000000000002"}"""));
        _handler.Antwoorden.Enqueue((HttpStatusCode.BadRequest, """{"fout":"Ongeldig e-mailadres."}"""));

        var id = await _server.NodigUitAsync(Project, Sessie, "akela@example.org", "Akela", ["Welpen"]);
        var fout = await Assert.ThrowsAsync<WebappException>(() => _server.NodigUitAsync(Project, Sessie, "x", "Akela", ["Welpen"]));

        Assert.Equal(Guid.Parse("30000000-0000-4000-8000-000000000002"), id);
        Assert.Equal("https://abcd.supabase.co/functions/v1/leiding", _handler.Verzoeken[0].Url);
        Assert.Equal("uitnodigen", _handler.Verzoeken[0].Json!.RootElement.GetProperty("actie").GetString());
        Assert.Equal("Ongeldig e-mailadres.", fout.Message);
    }

    [Fact]
    public async Task Intrekken_en_speltakken_zetten()
    {
        _handler.Antwoorden.Enqueue((HttpStatusCode.OK, "{}"));
        _handler.Antwoorden.Enqueue((HttpStatusCode.NoContent, ""));
        var id = Guid.NewGuid();

        await _server.TrekInAsync(Project, Sessie, id);
        await _server.ZetSpeltakkenAsync(Project, Sessie, id, ["Welpen", "Scouts"]);

        Assert.Equal("intrekken", _handler.Verzoeken[0].Json!.RootElement.GetProperty("actie").GetString());
        Assert.Equal("https://abcd.supabase.co/rest/v1/rpc/zet_leiding_speltakken", _handler.Verzoeken[1].Url);
        Assert.Equal(2, _handler.Verzoeken[1].Json!.RootElement.GetProperty("speltakken").GetArrayLength());
    }

    [Fact]
    public async Task Een_verlopen_toegang_op_de_api_is_een_toegangsfout()
    {
        _handler.Antwoorden.Enqueue((HttpStatusCode.Unauthorized, """{"message":"JWT expired"}"""));

        await Assert.ThrowsAsync<WebappToegangException>(() => _server.HaalLeidingAsync(Project, Sessie));
    }

    private sealed record Verzoek(string Url, string? Apikey, string? Autorisatie, JsonDocument? Json);

    private sealed class NepHandler : HttpMessageHandler
    {
        public Queue<(HttpStatusCode, string)> Antwoorden { get; } = new();
        public List<Verzoek> Verzoeken { get; } = [];
        public Exception? Fout { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Fout is not null)
            {
                throw Fout;
            }

            var inhoud = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Verzoeken.Add(new Verzoek(
                request.RequestUri!.ToString(),
                request.Headers.TryGetValues("apikey", out var sleutel) ? sleutel.Single() : null,
                request.Headers.Authorization?.ToString(),
                string.IsNullOrEmpty(inhoud) ? null : JsonDocument.Parse(inhoud)));
            var (status, tekst) = Antwoorden.Dequeue();
            return new HttpResponseMessage(status) { Content = new StringContent(tekst, Encoding.UTF8, "application/json") };
        }
    }
}
