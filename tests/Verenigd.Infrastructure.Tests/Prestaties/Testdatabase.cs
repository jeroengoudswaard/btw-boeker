using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Infrastructure.Database;

namespace Verenigd.Infrastructure.Tests.Prestaties;

/// <summary>
/// Besluit 234: een kunstmatige database van realistische omvang (een rekeningschema, twee boekjaren en een paar duizend
/// geboekte posten), om de snelheid van laden, boeken en overzichten te meten. Geen echte gegevens.
/// </summary>
internal static class Testdatabase
{
    public const string Wachtwoord = "scouting is fantastisch";
    public static readonly DateTime Nu = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    private static readonly (int Nummer, string Naam, RekeningSoort Soort)[] Rekeningen =
    [
        (600, "Algemene reserve", RekeningSoort.Balans),
        (1000, "Kas", RekeningSoort.Balans),
        (1100, "Bank", RekeningSoort.Balans),
        (1150, "Spaarrekening", RekeningSoort.Balans),
        (1300, "Debiteuren contributie", RekeningSoort.Balans),
        (4000, "Afdracht Scouting Nederland", RekeningSoort.Resultaat),
        (4100, "Opkomsten", RekeningSoort.Resultaat),
        (4110, "Kampen", RekeningSoort.Resultaat),
        (4200, "Huur", RekeningSoort.Resultaat),
        (4210, "Energie", RekeningSoort.Resultaat),
        (4300, "Bestuur", RekeningSoort.Resultaat),
        (4310, "Bankkosten", RekeningSoort.Resultaat),
        (8010, "Contributie Bevers", RekeningSoort.Resultaat),
        (8020, "Contributie Welpen", RekeningSoort.Resultaat),
        (8030, "Contributie Scouts", RekeningSoort.Resultaat),
        (8040, "Contributie Explorers", RekeningSoort.Resultaat),
        (8400, "Acties", RekeningSoort.Resultaat),
        (8500, "Subsidies", RekeningSoort.Resultaat),
    ];

    /// <summary>Maakt de database aan en vult de Vereniging met <paramref name="posten"/> geboekte posten (2025 en 2026).</summary>
    public static async Task MaakAsync(string pad, int posten)
    {
        var beheer = new DatabaseBeheer();
        await beheer.MaakAanAsync(pad, Wachtwoord);
        await using var sessie = await beheer.OpenAsync(pad, Wachtwoord);
        var dienst = new BoekhoudingDienst(sessie, new VasteTijd(Nu));
        const AdministratieSoort ver = AdministratieSoort.Vereniging;

        await dienst.RichtInAsync(ver);
        await dienst.OpenBoekjaarAsync(ver, 2025);
        foreach (var (nummer, naam, soort) in Rekeningen)
        {
            await dienst.BewaarRekeningAsync(ver, new RekeningInvoer(null, nummer, naam, null, soort, RekeningRol.Geen, true));
        }

        await sessie.Boekhouding.PasToeAsync(ver, stand =>
        {
            var memoriaal = stand.Dagboeken.Single(d => d.Soort == DagboekSoort.Memoriaal);
            var bank = stand.Rekeningen.Single(r => r.Nummer == 1100);
            var tegen = stand.Rekeningen.Where(r => r.Soort == RekeningSoort.Resultaat).ToList();
            var wijziging = new BoekhoudingWijziging<bool>(true);
            var willekeurig = new Random(42);
            for (var i = 0; i < posten; i++)
            {
                var datum = new DateOnly(2025, 1, 1).AddDays(i * 670 / posten);
                var rekening = tegen[willekeurig.Next(tegen.Count)];
                var bedrag = new Bedrag(willekeurig.Next(100, 50_000));
                var ontvangst = rekening.Nummer >= 8000;
                var post = new Journaalpost(ver, memoriaal, datum, $"Testpost {i + 1}");
                post.Wijzig(memoriaal, datum, $"Testpost {i + 1}",
                [
                    new RegelInvoer(ontvangst ? bank : rekening, null, bedrag, Bedrag.Nul),
                    new RegelInvoer(ontvangst ? rekening : bank, null, Bedrag.Nul, bedrag),
                ]);
                post.Boek(i + 1, Nu);
                wijziging.Toevoegen.Add(post);
            }

            return wijziging;
        });
    }

    public static Task<IDatabaseSessie> OpenAsync(string pad) => new DatabaseBeheer().OpenAsync(pad, Wachtwoord);

    private sealed class VasteTijd(DateTimeOffset nu) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nu;
    }
}
