using Verenigd.Application.Begrotingen;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Infrastructure.Database;

namespace Verenigd.Infrastructure.Tests.Begrotingen;

/// <summary>Meerjarenplan van de Stichting in de echte, versleutelde database (besluiten 159–164). Bedragen zijn verzonnen.</summary>
public sealed class MeerjarenplanOpslagTests : IAsyncLifetime, IDisposable
{
    private const string Wachtwoord = "scouting is fantastisch";
    private const AdministratieSoort Sti = AdministratieSoort.Stichting;
    private readonly TijdelijkeMap _map = new();
    private readonly DatabaseBeheer _beheer = new();
    private IDatabaseSessie _sessie = null!;

    public async Task InitializeAsync()
    {
        var pad = _map.Bestand("test.vdb");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        _sessie = await _beheer.OpenAsync(pad, Wachtwoord);
        var dienst = new BoekhoudingDienst(_sessie, TimeProvider.System);
        await dienst.RichtInAsync(Sti);
        await dienst.BewaarRekeningAsync(Sti, new RekeningInvoer(null, 8100, "Huur van de Vereniging", null, RekeningSoort.Resultaat, RekeningRol.Geen, true));
        await dienst.BewaarRekeningAsync(Sti, new RekeningInvoer(null, 4200, "Onderhoud gebouw", null, RekeningSoort.Resultaat, RekeningRol.Geen, true));
    }

    public async Task DisposeAsync() => await _sessie.DisposeAsync();

    public void Dispose() => _map.Dispose();

    [Fact]
    public async Task Meerjarenplan_met_versies_bedragen_en_toelichting_wordt_opgeslagen()
    {
        var dienst = new MeerjarenplanDienst(_sessie, TimeProvider.System);
        var stand = await _sessie.Boekhouding.LaadAsync(Sti);
        var huur = stand.Rekeningen.Single(r => r.Nummer == 8100);
        var onderhoud = stand.Rekeningen.Single(r => r.Nummer == 4200);
        var huurbedragen = Enumerable.Repeat(new Bedrag(600000), 10).ToArray();
        var onderhoudbedragen = new Bedrag[10];
        Array.Fill(onderhoudbedragen, Bedrag.Nul);
        onderhoudbedragen[3] = new Bedrag(300000);

        var id = await dienst.MaakAsync(Sti, 2027);
        await dienst.BewaarAsync(Sti, id, [new MeerjarenInvoer(huur.Id, huurbedragen, "Huurcontract"), new MeerjarenInvoer(onderhoud.Id, onderhoudbedragen, null)]);
        await dienst.StelVastAsync(Sti, id, new DateOnly(2026, 11, 20), "Bestuur 20-11-2026");
        var herziening = await dienst.HerzieAsync(Sti, id);
        onderhoudbedragen[3] = new Bedrag(350000);
        await dienst.BewaarAsync(Sti, herziening, [new MeerjarenInvoer(onderhoud.Id, onderhoudbedragen, "Offerte")]);

        stand = await _sessie.Boekhouding.LaadAsync(Sti);
        Assert.Equal(2, stand.Meerjarenplannen.Count);
        var eerste = stand.Meerjarenplannen.Single(p => p.Versie == 1);
        Assert.True(eerste.IsVastgesteld);
        Assert.Equal(new Bedrag(600000), eerste.Bedrag(huur, 2036));
        Assert.Equal(new Bedrag(300000), eerste.Bedrag(onderhoud, 2030));
        Assert.Equal("Huurcontract", eerste.Toelichting(huur));
        var tweede = stand.Meerjarenplannen.Single(p => p.Versie == 2);
        Assert.Equal(new Bedrag(350000), tweede.Bedrag(onderhoud, 2030));
        Assert.Equal("Offerte", tweede.Toelichting(onderhoud));
        Assert.Empty((await _sessie.Boekhouding.LaadAsync(AdministratieSoort.Vereniging)).Meerjarenplannen);
    }
}
