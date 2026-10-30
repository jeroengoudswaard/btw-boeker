using Verenigd.Application.Inrichting;
using Verenigd.Infrastructure.Database;

namespace Verenigd.Infrastructure.Tests.Inrichting;

/// <summary>De voortgang van de startwizard staat in de database zelf (besluit 173) en blijft bewaard na opnieuw openen.</summary>
public sealed class StartwizardOpslagTests : IDisposable
{
    private const string Wachtwoord = "scouting is fantastisch";
    private readonly TijdelijkeMap _map = new();
    private readonly DatabaseBeheer _beheer = new();

    public void Dispose() => _map.Dispose();

    [Fact]
    public async Task Voortgang_is_nieuw_en_blijft_bewaard_na_opnieuw_openen()
    {
        var pad = _map.Bestand("test.vdb");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        await using (var sessie = await _beheer.OpenAsync(pad, Wachtwoord))
        {
            Assert.Equal(Startwizardvoortgang.Nieuw, await sessie.Startwizard.LaadAsync());
            await sessie.Startwizard.BewaarAsync(new Startwizardvoortgang(Startstap.Bank, IsAfgerond: false));
            await sessie.Startwizard.BewaarAsync(new Startwizardvoortgang(Startstap.Contributie, IsAfgerond: false)); // overschrijft
        }

        await using (var sessie = await _beheer.OpenAsync(pad, Wachtwoord))
        {
            var voortgang = await sessie.Startwizard.LaadAsync();
            Assert.Equal(new Startwizardvoortgang(Startstap.Contributie, false), voortgang);
            Assert.True(voortgang.KanHervatten);

            await sessie.Startwizard.BewaarAsync(new Startwizardvoortgang(null, IsAfgerond: true));
            Assert.False((await sessie.Startwizard.LaadAsync()).KanHervatten);
        }
    }
}
