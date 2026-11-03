using Verenigd.Application.Administraties;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Application.Email;
using Verenigd.Application.Import;
using Verenigd.Application.Incasso;
using Verenigd.Application.Inrichting;
using Verenigd.Application.Leden;
using Verenigd.Application.Webapp;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;
using Verenigd.Domain.Webapp;

namespace Verenigd.Application.Tests.Webapp;

/// <summary>Synchroniseren, inloggen en voorstellen afhandelen (besluiten 34, 38, 231 en 232).</summary>
public class WebappDienstTests
{
    private static readonly DateTime Nu = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Groep Welpen = new("Welpen", GroepSoort.Speltak);

    private readonly FakeLeden _leden = new();
    private readonly FakeOpslag _opslag = new();
    private readonly FakeServer _server = new();
    private readonly WebappDienst _dienst;

    public WebappDienstTests()
    {
        _dienst = new WebappDienst(new Sessie(_leden, _opslag), _server, new VasteTijd(new DateTimeOffset(Nu)));
    }

    private Persoon VoegLidToe()
    {
        var persoon = new Persoon(Lidnummer.Parse("100000001"),
            new LidGegevens("Bram", null, "Jong", null, null, "bram@example.org", null, null, "Actief", null, null, null, null, null, null), Nu.AddDays(-30));
        persoon.WerkFunctiesBij([new FunctieGegevens("Welpen", "Jeugdlid", Lidtype.Jeugdlid, new DateOnly(2025, 9, 1), null, "Actief")],
            new Dictionary<string, Groep> { ["Welpen"] = Welpen }, DateOnly.FromDateTime(Nu), Nu.AddDays(-30));
        _leden.Personen.Add(persoon);
        return persoon;
    }

    private void Gekoppeld()
    {
        _opslag.Instellingen.WijzigProject("https://abcd.supabase.co", "sb_publishable_x", "beheer@example.org");
        _opslag.Instellingen.Koppel("vernieuw-1");
    }

    [Fact]
    public async Task Inloggen_bewaart_de_vernieuwingssleutel()
    {
        _opslag.Instellingen.WijzigProject("https://abcd.supabase.co", "sb_publishable_x", "beheer@example.org");

        await _dienst.InloggenAsync("geheim", "123456");

        Assert.Equal(("beheer@example.org", "geheim", "123456"), _server.Inlog);
        Assert.Equal(new Uri("https://abcd.supabase.co"), _server.Project!.Adres);
        Assert.Equal("vernieuw-login", _opslag.Instellingen.Vernieuwingssleutel);
    }

    [Fact]
    public async Task Inloggen_zonder_project_geeft_een_melding()
    {
        var fout = await Assert.ThrowsAsync<WebappException>(() => _dienst.InloggenAsync("geheim", "123456"));
        Assert.Contains("Instellingen", fout.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Synchroniseren_stuurt_de_kopie_en_bewaart_de_nieuwe_vernieuwingssleutel()
    {
        Gekoppeld();
        var lid = VoegLidToe();
        _server.Voorstellen.Add(new Voorstel(Guid.NewGuid(), lid.Id, "email", "bram@example.org", "b@example.org", "Akela", Nu));

        var resultaat = await _dienst.SynchroniseerAsync();

        Assert.Equal(new Synchronisatieresultaat(1, 0, 1), resultaat);
        Assert.Equal([lid.Id], _server.Kopie!.Leden.Select(l => l.Id));
        Assert.Equal("vernieuw-2", _opslag.Instellingen.Vernieuwingssleutel); // de sleutel roteert
        Assert.True(_opslag.Instellingen.LaatsteGelukt);
        Assert.Equal(Nu, _opslag.Instellingen.LaatsteSynchronisatieUtc);
    }

    [Fact]
    public async Task Zonder_koppeling_wordt_niet_gesynchroniseerd()
    {
        await Assert.ThrowsAsync<WebappException>(() => _dienst.SynchroniseerAsync());
        Assert.Null(_server.Kopie);
    }

    [Fact]
    public async Task Een_verlopen_sessie_ontkoppelt_en_vraagt_opnieuw_in_te_loggen()
    {
        Gekoppeld();
        _server.VernieuwFout = new WebappToegangException("Verlopen");

        await Assert.ThrowsAsync<WebappToegangException>(() => _dienst.SynchroniseerAsync());

        Assert.Null(_opslag.Instellingen.Vernieuwingssleutel);
        Assert.False(_opslag.Instellingen.LaatsteGelukt);
    }

    [Fact]
    public async Task Onbereikbaar_houdt_de_koppeling_en_noteert_de_melding()
    {
        Gekoppeld();
        _server.VernieuwFout = new WebappOnbereikbaarException("Geen verbinding");

        await Assert.ThrowsAsync<WebappOnbereikbaarException>(() => _dienst.SynchroniseerAsync());

        Assert.Equal("vernieuw-1", _opslag.Instellingen.Vernieuwingssleutel);
        Assert.Equal("Geen verbinding", _opslag.Instellingen.LaatsteMelding);
    }

    [Fact]
    public async Task Voorstellen_tonen_lidnaam_huidige_waarde_en_afwijking()
    {
        Gekoppeld();
        var lid = VoegLidToe();
        _server.Voorstellen.Add(new Voorstel(Guid.NewGuid(), lid.Id, "email", "bram@example.org", "b@example.org", "Akela", Nu));
        _server.Voorstellen.Add(new Voorstel(Guid.NewGuid(), lid.Id, "telefoon", "+31600000000", "06 12345678", "Akela", Nu));
        _server.Voorstellen.Add(new Voorstel(Guid.NewGuid(), Guid.NewGuid(), "email", null, "x@example.org", "Akela", Nu));

        var regels = await _dienst.HaalVoorstellenAsync();

        Assert.Equal(3, regels.Count);
        Assert.Equal(("Bram Jong", "E-mail", false), (regels[0].Lidnaam, regels[0].Omschrijving, regels[0].WijktAf));
        Assert.True(regels[1].WijktAf); // in Verenigd is het telefoonnummer intussen anders (leeg)
        Assert.Null(regels[1].HuidigeWaarde);
        Assert.True(regels[2].LidOnbekend);
    }

    [Fact]
    public async Task Goedkeuren_past_lokaal_toe_en_daarna_op_de_server()
    {
        Gekoppeld();
        var lid = VoegLidToe();
        var voorstel = new Voorstel(Guid.NewGuid(), lid.Id, "email", "bram@example.org", "nieuw@example.org", "Akela", Nu);

        await _dienst.KeurGoedAsync(voorstel);

        Assert.Equal("nieuw@example.org", lid.Email);
        Assert.Contains(lid.Herkomst, h => h.Veld == "E-mail" && h.Bron == Veldbron.Leiding);
        Assert.Equal((voorstel.Id, true), _server.Afgehandeld);
    }

    [Fact]
    public async Task Goedkeuren_mislukt_lokaal_dan_niet_op_de_server()
    {
        Gekoppeld();
        var voorstel = new Voorstel(Guid.NewGuid(), Guid.NewGuid(), "email", null, "nieuw@example.org", "Akela", Nu);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _dienst.KeurGoedAsync(voorstel));

        Assert.Null(_server.Afgehandeld);
    }

    [Fact]
    public async Task Afwijzen_wijzigt_niets_lokaal()
    {
        Gekoppeld();
        var lid = VoegLidToe();
        var voorstel = new Voorstel(Guid.NewGuid(), lid.Id, "email", "bram@example.org", "nieuw@example.org", "Akela", Nu);

        await _dienst.WijsAfAsync(voorstel);

        Assert.Equal("bram@example.org", lid.Email);
        Assert.Equal((voorstel.Id, false), _server.Afgehandeld);
    }

    [Fact]
    public async Task Uitnodigen_geeft_de_speltakken_door()
    {
        Gekoppeld();

        var id = await _dienst.NodigUitAsync("akela@example.org", "Akela", ["Welpen"]);

        Assert.Equal(("akela@example.org", "Akela", "Welpen"), _server.Uitnodiging);
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Ontkoppelen_wist_de_sessie()
    {
        Gekoppeld();

        await _dienst.OntkoppelAsync();

        Assert.False(_opslag.Instellingen.IsGekoppeld);
        Assert.True(_opslag.Instellingen.IsIngesteld);
    }

    // ===== Fakes =====

    private sealed class VasteTijd(DateTimeOffset nu) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nu;
    }

    private sealed class FakeOpslag : IWebappOpslag
    {
        public Webappinstellingen Instellingen { get; } = new();

        public Task<Webappinstellingen> LaadAsync(CancellationToken annulering = default) => Task.FromResult(Instellingen);

        public Task BewaarAsync(Webappinstellingen instellingen, CancellationToken annulering = default) => Task.CompletedTask;
    }

    private sealed class FakeLeden : ILedenRepository
    {
        public List<Persoon> Personen { get; } = [];

        public Task<IReadOnlyList<Persoon>> HaalAlleAsync(CancellationToken annulering = default) => Task.FromResult<IReadOnlyList<Persoon>>(Personen);

        public Task<IReadOnlyList<Groep>> HaalGroepenAsync(CancellationToken annulering = default) => Task.FromResult<IReadOnlyList<Groep>>([Welpen]);

        public Task BewaarGroepsoortenAsync(IReadOnlyDictionary<string, GroepSoort> soortPerGroep, CancellationToken annulering = default) => Task.CompletedTask;

        public Task BewaarNotitiesAsync(Guid persoonId, string? notities, DateTime nuUtc, CancellationToken annulering = default) => Task.CompletedTask;

        public Task NeemVoorstelOverAsync(Guid persoonId, string veld, string? waarde, DateTime nuUtc, CancellationToken annulering = default)
        {
            var persoon = Personen.Find(p => p.Id == persoonId) ?? throw new KeyNotFoundException("Onbekend lid.");
            persoon.NeemVoorstelOver(veld, waarde, nuUtc);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeServer : IWebappServer
    {
        private int _teller;

        public Webappproject? Project { get; private set; }
        public (string, string, string)? Inlog { get; private set; }
        public Ledenkopie? Kopie { get; private set; }
        public List<Voorstel> Voorstellen { get; } = [];
        public (Guid, bool)? Afgehandeld { get; private set; }
        public (string, string, string)? Uitnodiging { get; private set; }
        public WebappException? VernieuwFout { get; set; }

        public Task<WebappSessie> InloggenAsync(Webappproject project, string email, string wachtwoord, string code, CancellationToken annulering = default)
        {
            Project = project;
            Inlog = (email, wachtwoord, code);
            return Task.FromResult(new WebappSessie("toegang", "vernieuw-login"));
        }

        public Task<WebappSessie> VernieuwAsync(Webappproject project, string vernieuwingssleutel, CancellationToken annulering = default)
        {
            if (VernieuwFout is not null)
            {
                throw VernieuwFout;
            }

            _teller++;
            return Task.FromResult(new WebappSessie("toegang", $"vernieuw-{_teller + 1}"));
        }

        public Task<Synchronisatie> SynchroniseerAsync(Webappproject project, WebappSessie sessie, Ledenkopie kopie, CancellationToken annulering = default)
        {
            Kopie = kopie;
            return Task.FromResult(new Synchronisatie(kopie.Leden.Count, 0));
        }

        public Task<IReadOnlyList<Voorstel>> HaalOpenVoorstellenAsync(Webappproject project, WebappSessie sessie, CancellationToken annulering = default) =>
            Task.FromResult<IReadOnlyList<Voorstel>>(Voorstellen);

        public Task HandelAfAsync(Webappproject project, WebappSessie sessie, Guid voorstel, bool goedgekeurd, CancellationToken annulering = default)
        {
            Afgehandeld = (voorstel, goedgekeurd);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Leider>> HaalLeidingAsync(Webappproject project, WebappSessie sessie, CancellationToken annulering = default) =>
            Task.FromResult<IReadOnlyList<Leider>>([]);

        public Task<Guid> NodigUitAsync(Webappproject project, WebappSessie sessie, string email, string naam, IReadOnlyList<string> speltakken, CancellationToken annulering = default)
        {
            Uitnodiging = (email, naam, string.Join(",", speltakken));
            return Task.FromResult(Guid.NewGuid());
        }

        public Task ZetSpeltakkenAsync(Webappproject project, WebappSessie sessie, Guid leider, IReadOnlyList<string> speltakken, CancellationToken annulering = default) => Task.CompletedTask;

        public Task TrekInAsync(Webappproject project, WebappSessie sessie, Guid leider, CancellationToken annulering = default) => Task.CompletedTask;
    }

    private sealed class Sessie(ILedenRepository leden, IWebappOpslag webapp) : IDatabaseSessie
    {
        public string Pad => "test.vdb";
        public IAdministratieRepository Administraties => throw new NotSupportedException();
        public ILedenRepository Leden { get; } = leden;
        public ILedenImportOpslag LedenImport => throw new NotSupportedException();
        public IContributieRepository Contributie => throw new NotSupportedException();
        public IBoekhoudingRepository Boekhouding => throw new NotSupportedException();
        public IStartwizardOpslag Startwizard => throw new NotSupportedException();
        public IEmailOpslag Email => throw new NotSupportedException();
        public IWebappOpslag Webapp { get; } = webapp;

        public Task MaakBackupAsync(string doelPad, CancellationToken annulering = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
