using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;

namespace Verenigd.Infrastructure.Tests.Prestaties;

/// <summary>
/// Besluit 234: de boekhouding wordt als momentopname bewaard en pas opnieuw geladen als er sinds de vorige keer iets in de
/// database is opgeslagen (door welke opslag dan ook). Zo laden Start, kerncijfers en de pagina Boekhouding niet steeds opnieuw.
/// </summary>
public sealed class MomentopnameTests : IDisposable
{
    private readonly TijdelijkeMap _map = new();

    public void Dispose() => _map.Dispose();

    [Fact]
    public async Task Zonder_wijziging_dezelfde_momentopname_en_na_boeken_een_nieuwe()
    {
        var pad = _map.Bestand("moment.vdb");
        await Testdatabase.MaakAsync(pad, 20);
        await using var sessie = await Testdatabase.OpenAsync(pad);
        var dienst = new BoekhoudingDienst(sessie, TimeProvider.System);

        var eerste = await dienst.LaadAsync(AdministratieSoort.Vereniging);
        var tweede = await dienst.LaadAsync(AdministratieSoort.Vereniging);
        Assert.Same(eerste, tweede);
        Assert.NotSame(eerste, await dienst.LaadAsync(AdministratieSoort.Stichting));

        var memoriaal = eerste.Dagboeken.Single(d => d.Code == "MEM");
        await dienst.BoekAsync(AdministratieSoort.Vereniging, new PostInvoer(null, memoriaal.Id, new DateOnly(2026, 11, 3), "Na de momentopname",
        [
            new RegelInvoerId(eerste.Rekeningen.Single(r => r.Nummer == 4210).Id, null, new Bedrag(500), Bedrag.Nul),
            new RegelInvoerId(eerste.Rekeningen.Single(r => r.Nummer == 1100).Id, null, Bedrag.Nul, new Bedrag(500)),
        ]));

        var derde = await dienst.LaadAsync(AdministratieSoort.Vereniging);
        Assert.NotSame(eerste, derde);
        Assert.Contains(derde.Posten, p => p.Omschrijving == "Na de momentopname");
        Assert.Equal(eerste.Posten.Count + 1, derde.Posten.Count);
    }

    [Fact]
    public async Task Een_wijziging_via_een_andere_opslag_maakt_de_momentopname_ongeldig()
    {
        var pad = _map.Bestand("ander.vdb");
        await Testdatabase.MaakAsync(pad, 5);
        await using var sessie = await Testdatabase.OpenAsync(pad);

        var eerste = await sessie.Boekhouding.LaadAsync(AdministratieSoort.Vereniging);
        var vereniging = (await sessie.Administraties.HaalAlleAsync()).Single(a => a.Soort == AdministratieSoort.Vereniging);
        vereniging.WijzigNaam("Scouting Proef");
        await sessie.Administraties.BewaarAsync(vereniging);

        Assert.NotSame(eerste, await sessie.Boekhouding.LaadAsync(AdministratieSoort.Vereniging));
    }

    [Fact]
    public async Task Elke_opslag_geeft_een_seintje_en_laden_niet()
    {
        var pad = _map.Bestand("seintje.vdb");
        await Testdatabase.MaakAsync(pad, 5);
        await using var sessie = await Testdatabase.OpenAsync(pad);
        var aantal = 0;
        object? afzender = null;
        sessie.Gewijzigd += (s, _) =>
        {
            afzender = s;
            Interlocked.Increment(ref aantal);
        }; // besluit 277

        var stand = await sessie.Boekhouding.LaadAsync(AdministratieSoort.Vereniging);
        await sessie.Leden.HaalAlleAsync();
        Assert.Equal(0, Volatile.Read(ref aantal));

        var vereniging = (await sessie.Administraties.HaalAlleAsync()).Single(a => a.Soort == AdministratieSoort.Vereniging);
        vereniging.WijzigNaam("Scouting Proef");
        await sessie.Administraties.BewaarAsync(vereniging);
        Assert.True(Volatile.Read(ref aantal) > 0);
        Assert.Same(sessie, afzender); // de shell herkent zo zijn eigen sessie

        Volatile.Write(ref aantal, 0);
        var dienst = new BoekhoudingDienst(sessie, TimeProvider.System);
        var memoriaal = stand.Dagboeken.Single(d => d.Code == "MEM");
        await dienst.BoekAsync(AdministratieSoort.Vereniging, new PostInvoer(null, memoriaal.Id, new DateOnly(2026, 11, 3), "Seintje",
        [
            new RegelInvoerId(stand.Rekeningen.Single(r => r.Nummer == 4210).Id, null, new Bedrag(500), Bedrag.Nul),
            new RegelInvoerId(stand.Rekeningen.Single(r => r.Nummer == 1100).Id, null, Bedrag.Nul, new Bedrag(500)),
        ]));
        Assert.True(Volatile.Read(ref aantal) > 0);
    }

    [Fact]
    public async Task Een_nieuwe_sessie_begint_zonder_momentopname()
    {
        var pad = _map.Bestand("sessies.vdb");
        await Testdatabase.MaakAsync(pad, 5);
        Application.Boekhouding.BoekhoudingStand eerste;
        await using (var sessie = await Testdatabase.OpenAsync(pad))
        {
            eerste = await sessie.Boekhouding.LaadAsync(AdministratieSoort.Vereniging);
        }

        await using var opnieuw = await Testdatabase.OpenAsync(pad);
        Assert.NotSame(eerste, await opnieuw.Boekhouding.LaadAsync(AdministratieSoort.Vereniging));
    }
}
