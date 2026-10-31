using Verenigd.Infrastructure.Database;

namespace Verenigd.Infrastructure.Tests.Webapp;

/// <summary>De instellingen van de webapp in de echte, versleutelde database (fase 7).</summary>
public sealed class WebappOpslagTests : IDisposable
{
    private const string Wachtwoord = "scouting is fantastisch";
    private readonly TijdelijkeMap _map = new();
    private readonly DatabaseBeheer _beheer = new();

    public void Dispose() => _map.Dispose();

    [Fact]
    public async Task Instellingen_en_koppeling_blijven_bewaard()
    {
        var pad = _map.Bestand("test.vdb");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        var nu = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        await using (var sessie = await _beheer.OpenAsync(pad, Wachtwoord))
        {
            var leeg = await sessie.Webapp.LaadAsync();
            Assert.False(leeg.IsIngesteld);

            leeg.WijzigProject("https://abcd.supabase.co/", "sb_publishable_x", "beheer@example.org");
            leeg.Koppel("vernieuw");
            leeg.RegistreerSynchronisatie(nu, true, "12 leden gesynchroniseerd");
            await sessie.Webapp.BewaarAsync(leeg);
            leeg.Koppel("vernieuw-2");
            await sessie.Webapp.BewaarAsync(leeg);
        }

        await using (var sessie = await _beheer.OpenAsync(pad, Wachtwoord))
        {
            var instellingen = await sessie.Webapp.LaadAsync();
            Assert.Equal("https://abcd.supabase.co", instellingen.Projectadres);
            Assert.Equal("vernieuw-2", instellingen.Vernieuwingssleutel);
            Assert.True(instellingen.IsGekoppeld);
            Assert.Equal(nu, instellingen.LaatsteSynchronisatieUtc);
            Assert.Equal("12 leden gesynchroniseerd", instellingen.LaatsteMelding);
        }
    }

    [Fact]
    public async Task Een_onbekend_lid_bij_een_voorstel_geeft_KeyNotFound()
    {
        var pad = _map.Bestand("test.vdb");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        await using var sessie = await _beheer.OpenAsync(pad, Wachtwoord);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sessie.Leden.NeemVoorstelOverAsync(Guid.NewGuid(), "email", "a@example.org", DateTime.UtcNow));
    }
}
