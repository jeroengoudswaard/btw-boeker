using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Infrastructure.Tests.Demo;

/// <summary>De demodatabase voor de schermafbeeldingen van de website bevat alleen verzonnen gegevens.</summary>
public sealed class DemodatabaseTests : IDisposable
{
    private readonly string _map = Path.Combine(Path.GetTempPath(), "btw-boeker-demo-" + Guid.NewGuid().ToString("N"));

    public DemodatabaseTests() => Directory.CreateDirectory(_map);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_map, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Bevat_een_ingerichte_boekhouding_en_eigen_namen()
    {
        var pad = Path.Combine(_map, "demo.btwdb");
        await Demodatabase.MaakAsync(pad, new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));

        await using var sessie = await Demodatabase.OpenAsync(pad);
        var administraties = await sessie.Administraties.HaalAlleAsync();
        Assert.Equal(Demodatabase.NaamVereniging, Assert.Single(administraties).Naam);

        var stand = await new BoekhoudingDienst(sessie, TimeProvider.System).LaadAsync(AdministratieSoort.Vereniging);
        Assert.True(stand.IsIngericht);
        Assert.True(stand.Posten.Count(p => p.Status == PostStatus.Geboekt) >= 20, $"{stand.Posten.Count} posten");
        Assert.Contains(stand.Posten, p => p.Status == PostStatus.Concept); // een startkaart "conceptposten"
        Assert.All(stand.Posten, p => Assert.Equal(p.Regels.Sum(r => r.Debet.Centen), p.Regels.Sum(r => r.Credit.Centen)));
        Assert.All(stand.Posten, p => Assert.Equal(2026, p.Datum.Year));
    }
}
