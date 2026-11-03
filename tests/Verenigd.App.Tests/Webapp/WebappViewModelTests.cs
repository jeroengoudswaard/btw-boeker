using Verenigd.App.Tests.Leden;
using Verenigd.App.ViewModels.Webapp;
using Verenigd.Application.Webapp;
using Verenigd.Domain.Leden;

namespace Verenigd.App.Tests.Webapp;

/// <summary>Pagina Webapp (fase 7): koppelen, synchroniseren, voorstellen afhandelen en leiding beheren. Gegevens zijn verzonnen.</summary>
public sealed class WebappViewModelTests
{
    private static readonly DateTimeOffset Nu = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeDatabaseBeheer _beheer = new();
    private readonly FakeDialoogService _dialogen = new();
    private readonly NepWebappServer _server = new();

    private async Task<WebappViewModel> MaakAsync(bool ingesteld = true, bool gekoppeld = true)
    {
        await LedenTestdata.VulAsync(_beheer.Leden,
            LedenTestdata.Lid("100000001", "Anna", "de Vries", "Welpen", Lidtype.Jeugdlid),
            LedenTestdata.Lid("100000002", "Kees", "Jansen", "Scouts", Lidtype.Kaderlid, email: "kees@example.org", rol: "Leiding"));
        if (ingesteld)
        {
            _beheer.Webapp.Instellingen.WijzigProject("https://abcd.supabase.co", "sb_publishable_x", "beheer@example.org");
        }

        if (gekoppeld)
        {
            _beheer.Webapp.Instellingen.Koppel("vernieuw");
        }

        _beheer.Voeg("test.vdb", "geheim");
        var sessie = await _beheer.OpenAsync("test.vdb", "geheim");
        var pagina = new WebappViewModel(sessie, _dialogen, new VasteTijd(Nu), _server);
        await pagina.VernieuwCommand.ExecuteAsync(null);
        return pagina;
    }

    private Guid LidId(string voornaam) => _beheer.Leden.Stand.Personen.Single(p => p.Voornaam == voornaam).Id;

    [Fact]
    public async Task Zonder_project_staat_de_pagina_op_koppelen_met_uitleg()
    {
        var pagina = await MaakAsync(ingesteld: false, gekoppeld: false);

        Assert.False(pagina.IsIngesteld);
        Assert.False(pagina.IsGekoppeld);
        Assert.Equal(WebappSubtab.Synchroniseren, pagina.Subtab);
        Assert.False(pagina.InloggenCommand.CanExecute(null));
    }

    [Fact]
    public async Task Project_opslaan_en_inloggen_met_wachtwoord_en_code()
    {
        var pagina = await MaakAsync(ingesteld: false, gekoppeld: false);

        pagina.Projectadres = "https://abcd.supabase.co";
        pagina.PubliekeSleutel = "sb_publishable_x";
        pagina.BeheerderEmail = "beheer@example.org";
        await pagina.BewaarProjectCommand.ExecuteAsync(null);
        Assert.True(pagina.IsIngesteld);

        pagina.Wachtwoord = "geheim";
        pagina.Code = "12345";
        Assert.False(pagina.InloggenCommand.CanExecute(null)); // de code heeft 6 cijfers
        pagina.Code = "123 456";
        await pagina.InloggenCommand.ExecuteAsync(null);

        Assert.True(pagina.IsGekoppeld);
        Assert.Equal("123456", _server.Code);
        Assert.Equal(string.Empty, pagina.Wachtwoord); // niet bewaren na het inloggen
        Assert.Equal(string.Empty, pagina.Code);
        Assert.NotNull(_server.Kopie); // direct gesynchroniseerd
    }

    [Fact]
    public async Task Een_ongeldig_projectadres_geeft_een_foutmelding()
    {
        var pagina = await MaakAsync(ingesteld: false, gekoppeld: false);

        pagina.Projectadres = "http://abcd.supabase.co";
        pagina.PubliekeSleutel = "sb_publishable_x";
        pagina.BeheerderEmail = "beheer@example.org";
        await pagina.BewaarProjectCommand.ExecuteAsync(null);

        Assert.False(pagina.IsIngesteld);
        Assert.StartsWith("✕", pagina.Foutmelding, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Synchroniseren_toont_het_resultaat()
    {
        var pagina = await MaakAsync();

        await pagina.SynchroniseerCommand.ExecuteAsync(null);

        Assert.Equal(2, _server.Kopie!.Leden.Count);
        Assert.Contains("✓", pagina.Synchronisatietekst, StringComparison.Ordinal);
        Assert.Contains("2 leden", pagina.Synchronisatietekst, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Onbereikbaar_geeft_een_waarschuwing_en_houdt_de_koppeling()
    {
        var pagina = await MaakAsync();
        _server.Fout = new WebappOnbereikbaarException("Geen verbinding met de webapp.");

        await pagina.SynchroniseerCommand.ExecuteAsync(null);

        Assert.True(pagina.IsGekoppeld);
        Assert.Equal("⚠ Geen verbinding met de webapp.", pagina.Foutmelding);
    }

    [Fact]
    public async Task Voorstellen_worden_geladen_met_huidige_waarde()
    {
        _server.Voorstellen.Add(new Voorstel(Guid.NewGuid(), Guid.Empty, "email", null, "x@example.org", "Akela", Nu.UtcDateTime));
        var pagina = await MaakAsync();
        _server.Voorstellen.Clear();
        _server.Voorstellen.Add(new Voorstel(Guid.NewGuid(), LidId("Kees"), "email", "kees@example.org", "k@example.org", "Akela", Nu.UtcDateTime));

        await pagina.VernieuwCommand.ExecuteAsync(null);

        var regel = Assert.Single(pagina.Voorstellen);
        Assert.Equal("Kees Jansen", regel.Lidnaam);
        Assert.Equal("E-mail", regel.Omschrijving);
        Assert.Equal("kees@example.org", regel.Oud);
        Assert.Equal("k@example.org", regel.Nieuw);
        Assert.Null(regel.Waarschuwing);
    }

    [Fact]
    public async Task Goedkeuren_past_het_lid_aan_en_haalt_het_voorstel_weg()
    {
        var pagina = await MaakAsync();
        var id = Guid.NewGuid();
        _server.Voorstellen.Add(new Voorstel(id, LidId("Kees"), "email", "kees@example.org", "k@example.org", "Akela", Nu.UtcDateTime));
        await pagina.VernieuwCommand.ExecuteAsync(null);

        await pagina.GoedkeurenCommand.ExecuteAsync(pagina.Voorstellen[0]);

        Assert.Equal("k@example.org", _beheer.Leden.Stand.Personen.Single(p => p.Voornaam == "Kees").Email);
        Assert.Equal((id, true), _server.Afgehandeld);
        Assert.Empty(pagina.Voorstellen);
        Assert.StartsWith("✓", pagina.Melding, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Goedkeuren_bij_afwijkende_waarde_vraagt_eerst_bevestiging()
    {
        var pagina = await MaakAsync();
        _server.Voorstellen.Add(new Voorstel(Guid.NewGuid(), LidId("Kees"), "email", "oud@example.org", "k@example.org", "Akela", Nu.UtcDateTime));
        await pagina.VernieuwCommand.ExecuteAsync(null);
        Assert.NotNull(pagina.Voorstellen[0].Waarschuwing);
        _dialogen.BevestigAntwoord = false;

        await pagina.GoedkeurenCommand.ExecuteAsync(pagina.Voorstellen[0]);

        Assert.Null(_server.Afgehandeld);
        Assert.Single(_dialogen.Bevestigingsvragen);
    }

    [Fact]
    public async Task Afwijzen_laat_het_lid_ongewijzigd()
    {
        var pagina = await MaakAsync();
        var id = Guid.NewGuid();
        _server.Voorstellen.Add(new Voorstel(id, LidId("Kees"), "email", "kees@example.org", "k@example.org", "Akela", Nu.UtcDateTime));
        await pagina.VernieuwCommand.ExecuteAsync(null);

        await pagina.AfwijzenCommand.ExecuteAsync(pagina.Voorstellen[0]);

        Assert.Equal("kees@example.org", _beheer.Leden.Stand.Personen.Single(p => p.Voornaam == "Kees").Email);
        Assert.Equal((id, false), _server.Afgehandeld);
    }

    [Fact]
    public async Task Leiding_uitnodigen_met_speltakken_uit_de_groepsindeling()
    {
        var pagina = await MaakAsync();
        Assert.Equal(["Scouts", "Welpen"], pagina.Speltakken.Select(s => s.Naam));

        pagina.NieuweNaam = "Akela";
        pagina.NieuwEmail = "akela@example.org";
        Assert.False(pagina.NodigUitCommand.CanExecute(null)); // eerst een speltak kiezen
        pagina.Speltakken.Single(s => s.Naam == "Welpen").IsGekozen = true;
        await pagina.NodigUitCommand.ExecuteAsync(null);

        Assert.Equal(("akela@example.org", "Akela", "Welpen"), _server.Uitnodiging);
        Assert.Single(pagina.Leiding);
        Assert.Equal(string.Empty, pagina.NieuweNaam);
    }

    [Fact]
    public async Task Een_leider_kiezen_toont_zijn_speltakken_en_intrekken_vraagt_bevestiging()
    {
        _server.Leiding.Add(new Leider(Guid.NewGuid(), "Hathi", "hathi@example.org", ["Scouts"]));
        var pagina = await MaakAsync();

        pagina.GekozenLeider = pagina.Leiding[0];
        Assert.Equal(["Scouts"], pagina.Speltakken.Where(s => s.IsGekozen).Select(s => s.Naam));
        Assert.Equal("Scouts", pagina.Leiding[0].Speltakkentekst);

        await pagina.TrekInCommand.ExecuteAsync(pagina.Leiding[0]);

        Assert.Single(_dialogen.Bevestigingsvragen);
        Assert.Empty(pagina.Leiding);
    }

    [Fact]
    public async Task Een_verlopen_sessie_vraagt_opnieuw_in_te_loggen()
    {
        var pagina = await MaakAsync();
        _server.Fout = new WebappToegangException("De sessie is verlopen. Log opnieuw in.");

        await pagina.SynchroniseerCommand.ExecuteAsync(null);

        Assert.False(pagina.IsGekoppeld);
        Assert.Equal(WebappSubtab.Synchroniseren, pagina.Subtab);
    }
}

internal sealed class NepWebappServer : IWebappServer
{
    public string? Code { get; private set; }
    public Ledenkopie? Kopie { get; private set; }
    public List<Voorstel> Voorstellen { get; } = [];
    public List<Leider> Leiding { get; } = [];
    public (Guid, bool)? Afgehandeld { get; private set; }
    public (string, string, string)? Uitnodiging { get; private set; }
    public WebappException? Fout { get; set; }

    private void Controleer()
    {
        if (Fout is not null)
        {
            throw Fout;
        }
    }

    public Task<WebappSessie> InloggenAsync(Webappproject project, string email, string wachtwoord, string code, CancellationToken annulering = default)
    {
        Code = code;
        return Task.FromResult(new WebappSessie("toegang", "vernieuw-login"));
    }

    public Task<WebappSessie> VernieuwAsync(Webappproject project, string vernieuwingssleutel, CancellationToken annulering = default)
    {
        Controleer();
        return Task.FromResult(new WebappSessie("toegang", vernieuwingssleutel + "+"));
    }

    public Task<Synchronisatie> SynchroniseerAsync(Webappproject project, WebappSessie sessie, Ledenkopie kopie, CancellationToken annulering = default)
    {
        Kopie = kopie;
        return Task.FromResult(new Synchronisatie(kopie.Leden.Count, 0));
    }

    public Task<IReadOnlyList<Voorstel>> HaalOpenVoorstellenAsync(Webappproject project, WebappSessie sessie, CancellationToken annulering = default) =>
        Task.FromResult<IReadOnlyList<Voorstel>>([.. Voorstellen]);

    public Task HandelAfAsync(Webappproject project, WebappSessie sessie, Guid voorstel, bool goedgekeurd, CancellationToken annulering = default)
    {
        Afgehandeld = (voorstel, goedgekeurd);
        Voorstellen.RemoveAll(v => v.Id == voorstel);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Leider>> HaalLeidingAsync(Webappproject project, WebappSessie sessie, CancellationToken annulering = default) =>
        Task.FromResult<IReadOnlyList<Leider>>([.. Leiding]);

    public Task<Guid> NodigUitAsync(Webappproject project, WebappSessie sessie, string email, string naam, IReadOnlyList<string> speltakken, CancellationToken annulering = default)
    {
        Uitnodiging = (email, naam, string.Join(",", speltakken));
        var id = Guid.NewGuid();
        Leiding.Add(new Leider(id, naam, email, speltakken));
        return Task.FromResult(id);
    }

    public Task ZetSpeltakkenAsync(Webappproject project, WebappSessie sessie, Guid leider, IReadOnlyList<string> speltakken, CancellationToken annulering = default) => Task.CompletedTask;

    public Task TrekInAsync(Webappproject project, WebappSessie sessie, Guid leider, CancellationToken annulering = default)
    {
        Leiding.RemoveAll(l => l.Id == leider);
        return Task.CompletedTask;
    }
}
