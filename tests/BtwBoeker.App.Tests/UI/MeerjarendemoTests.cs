using Microsoft.Data.Sqlite;
using BtwBoeker.Application.Bank;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.Tests.UI;

/// <summary>De demodatabase voor de snelheidsmeting klopt: alle jaren aanwezig, oude jaren afgesloten, alles in evenwicht.</summary>
public sealed class MeerjarendemoTests : IDisposable
{
    private readonly string _map = Path.Combine(Path.GetTempPath(), "btw-boeker-demo", Guid.NewGuid().ToString("N"));

    public MeerjarendemoTests() => Directory.CreateDirectory(_map);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_map, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Vijf_jaar_verhuur_met_afgesloten_oude_jaren_en_werk_in_het_lopende_jaar()
    {
        var pad = Path.Combine(_map, "meerjarig.btwdb");
        var nu = new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

        var (posten, transacties) = await Meerjarendemo.MaakAsync(pad, nu);

        await using var sessie = await Meerjarendemo.OpenAsync(pad);
        Assert.Equal(Meerjarendemo.Administratienaam, (await sessie.Administraties.HaalAsync()).Naam);
        var stand = await new BoekhoudingDienst(sessie, TimeProvider.System).LaadAsync(AdministratieSoort.Standaard);
        Assert.Equal([2022, 2023, 2024, 2025, 2026], stand.Boekjaren.Select(b => b.Jaar).Order());
        Assert.Equal([2022, 2023, 2024], stand.Boekjaren.Where(b => !b.IsOpen).Select(b => b.Jaar).Order());
        Assert.True(posten >= 300, $"{posten} geboekte posten");
        Assert.True(transacties >= 350, $"{transacties} banktransacties");
        Assert.All(stand.Posten, p => Assert.Equal(p.Regels.Sum(r => r.Debet.Centen), p.Regels.Sum(r => r.Credit.Centen)));
        Assert.All(stand.Posten.Where(p => p.Datum.Year < 2026), p => Assert.True(p.IsGeboekt, p.Omschrijving));
        Assert.Contains(stand.Posten, p => p.Status == PostStatus.Concept && p.Datum.Year == 2026);
        Assert.Contains(stand.Bankmutaties, m => Bankoverzicht.Status(stand, m) == MutatieStatus.TeVerwerken);
    }
}
