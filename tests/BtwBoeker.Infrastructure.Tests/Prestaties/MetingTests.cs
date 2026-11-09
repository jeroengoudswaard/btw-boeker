using System.Diagnostics;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Inrichting;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using Xunit.Abstractions;

namespace BtwBoeker.Infrastructure.Tests.Prestaties;

/// <summary>
/// Besluit 234: meet de handelingen die de gebruiker traag vindt (besluit 235) op de kunstmatige testdatabase en schrijft de
/// tijden in de testuitvoer, voor de tabel vóór en na in de pull request. Met ruime budgetten tegen terugval.
/// </summary>
[Trait("Categorie", "Prestaties")]
public sealed class MetingTests(ITestOutputHelper uitvoer) : IDisposable
{
    private const int Posten = 3000;
    private static readonly DateOnly Vandaag = new(2026, 11, 3);
    private readonly TijdelijkeMap _map = new();

    public void Dispose() => _map.Dispose();

    private long Meet(string naam, Action actie)
    {
        var klok = Stopwatch.StartNew();
        actie();
        uitvoer.WriteLine($"{naam}: {klok.ElapsedMilliseconds} ms");
        return klok.ElapsedMilliseconds;
    }

    private async Task<long> MeetAsync(string naam, Func<Task> actie)
    {
        var klok = Stopwatch.StartNew();
        await actie();
        uitvoer.WriteLine($"{naam}: {klok.ElapsedMilliseconds} ms");
        return klok.ElapsedMilliseconds;
    }

    [Fact]
    public async Task Laden_boeken_en_overzichten_op_een_gevulde_database()
    {
        var pad = _map.Bestand("meting.btwdb");
        await MeetAsync($"Testdatabase vullen ({Posten} posten)", () => Testdatabase.MaakAsync(pad, Posten));
        await using var sessie = await Testdatabase.OpenAsync(pad);
        var dienst = new BoekhoudingDienst(sessie, TimeProvider.System);

        BoekhoudingStand stand = null!;
        var laden = await MeetAsync("Boekhouding laden", async () => stand = await dienst.LaadAsync(AdministratieSoort.Vereniging));
        var startkaarten = await MeetAsync("Startkaarten bepalen", () => Startkaarten.BepaalAsync(sessie, Vandaag));
        var saldibalans = Meet("Overzicht saldibalans 2026", () => Grootboek.Saldibalans(stand, 2026));
        var balans = Meet("Overzicht balans 2026", () => Grootboek.Balans(stand, 2026));
        var wenv = Meet("Overzicht W&V 2026", () => Grootboek.WinstEnVerlies(stand, 2026));
        var saldi = Meet("Saldo per rekening (alle rekeningen, zoals de pagina)", () =>
        {
            foreach (var rekening in stand.Rekeningen)
            {
                Grootboek.Saldo(stand, rekening, 2026);
            }
        });

        var memoriaal = stand.Dagboeken.Single(d => d.Code == "MEM");
        var post = new PostInvoer(null, memoriaal.Id, Vandaag, "Nog één post",
        [
            new RegelInvoerId(stand.Rekeningen.Single(r => r.Nummer == 4210).Id, null, new Bedrag(1234), Bedrag.Nul),
            new RegelInvoerId(stand.Rekeningen.Single(r => r.Nummer == 1100).Id, null, Bedrag.Nul, new Bedrag(1234)),
        ]);
        var boeken = await MeetAsync("Eén post boeken", () => dienst.BoekAsync(AdministratieSoort.Vereniging, post));

        // Ruime budgetten (ook voor CI); de tabel vóór en na staat in de pull request.
        Assert.True(laden < 5000, $"Laden {laden} ms");
        Assert.True(startkaarten < 8000, $"Startkaarten {startkaarten} ms");
        Assert.True(saldibalans + balans + wenv + saldi < 5000, $"Overzichten {saldibalans + balans + wenv + saldi} ms");
        Assert.True(boeken < 5000, $"Boeken {boeken} ms");
    }
}
