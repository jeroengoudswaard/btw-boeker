using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using BtwBoeker.App.ViewModels;
using BtwBoeker.App.ViewModels.Boekhouding;

namespace BtwBoeker.App.Tests.UI;

/// <summary>
/// Alleen rondkijken in Boekhouding is geen wijziging: bij het sluiten mag de app dan niet vragen om op te slaan.
/// Gevonden met de snelheidsmeting op de meerjarige demodatabase.
/// </summary>
public sealed class BekijkenTests : IDisposable
{
    private readonly string _map = Path.Combine(Path.GetTempPath(), "btw-boeker-bekijken", Guid.NewGuid().ToString("N"));

    public BekijkenTests() => Directory.CreateDirectory(_map);

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
    public async Task Alle_onderdelen_en_rapporten_bekijken_is_geen_wijziging()
    {
        var pad = Path.Combine(_map, "meerjarig.btwdb");
        await Meerjarendemo.MaakAsync(pad, new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        await using var sessie = await Meerjarendemo.OpenAsync(pad);
        var administratie = new AdministratieViewModel(await sessie.Administraties.HaalAsync());
        var vm = new BoekhoudingViewModel(sessie, new FakeDialoogService(), TimeProvider.System, administratie, FakeBoekhouding.Hulpmiddelen(), NullLogger.Instance);
        await vm.VernieuwCommand.ExecuteAsync(null);
        Assert.False(vm.HeeftOnopgeslagenWijzigingen, "na laden");

        var gewijzigd = new List<string>();
        foreach (var subtab in Enum.GetValues<BoekhoudingSubtab>())
        {
            vm.Subtab = subtab;
            await Task.Delay(50);
            if (vm.HeeftOnopgeslagenWijzigingen)
            {
                gewijzigd.Add(subtab.ToString());
            }

            if (subtab == BoekhoudingSubtab.Rapporten)
            {
                foreach (var keuze in vm.Rapportkeuzes)
                {
                    vm.Rapportsoort = keuze.Soort;
                    await Task.Delay(50);
                    if (vm.HeeftOnopgeslagenWijzigingen)
                    {
                        gewijzigd.Add("Rapport " + keuze.Soort);
                    }
                }
            }
        }

        foreach (var post in vm.Posten.Take(30).ToList())
        {
            vm.GeselecteerdePost = post;
            if (vm.HeeftOnopgeslagenWijzigingen)
            {
                gewijzigd.Add($"Post {post.Nummer} {post.Omschrijving} ({post.Status})");
                break;
            }
        }

        Assert.Empty(gewijzigd);
    }

    /// <summary>
    /// Elke rij met een keuzelijst draagt de lijst zelf. De weergave bindt daaraan in plaats van via de UserControl, die
    /// pas later gevonden wordt; anders maakte een keuzelijst zonder items de rekening of rubriek leeg en telde de regel als
    /// gewijzigd (gevonden met de snelheidsmeting).
    /// </summary>
    [Fact]
    public async Task Rijen_kiezen_uit_hun_eigen_lijst()
    {
        var pad = Path.Combine(_map, "meerjarig.btwdb");
        await Meerjarendemo.MaakAsync(pad, new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));
        await using var sessie = await Meerjarendemo.OpenAsync(pad);
        var administratie = new AdministratieViewModel(await sessie.Administraties.HaalAsync());
        var vm = new BoekhoudingViewModel(sessie, new FakeDialoogService(), TimeProvider.System, administratie, FakeBoekhouding.Hulpmiddelen(), NullLogger.Instance);
        await vm.VernieuwCommand.ExecuteAsync(null);

        Assert.NotEmpty(vm.Boekingsregels);
        Assert.All(vm.Boekingsregels, r =>
        {
            Assert.Same(vm.Toewijsrekeningen, r.Rekeningen);
            Assert.Contains(r.GekozenRekening!, r.Rekeningen);
        });
        Assert.All(vm.Rekeningen.Where(r => r.GekozenRubriek is not null), r =>
        {
            Assert.Same(vm.Rubrieken, r.Rubrieken);
            Assert.Contains(r.GekozenRubriek!, r.Rubrieken);
        });

        vm.NieuweRegelCommand.Execute(null);
        Assert.Same(vm.Toewijsrekeningen, vm.Boekingsregels[^1].Rekeningen);
    }
}
