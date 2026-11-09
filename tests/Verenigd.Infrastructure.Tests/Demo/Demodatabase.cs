using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Infrastructure.Database;

namespace Verenigd.Infrastructure.Tests.Demo;

/// <summary>
/// Een demodatabase met alleen verzonnen boekhoudgegevens voor de schermafbeeldingen op de website.
/// </summary>
internal static class Demodatabase
{
    public const string Wachtwoord = "demo voorbeeldgroep";
    public const string NaamVereniging = "Scouting De Voorbeeldgroep";
    public const string NaamStichting = "Stichting Het Voorbeeldhonk";

    private static readonly (int Nummer, string Naam, RekeningSoort Soort)[] Rekeningen =
    [
        (600, "Algemene reserve", RekeningSoort.Balans),
        (1000, "Kas", RekeningSoort.Balans),
        (1100, "Bank", RekeningSoort.Balans),
        (4000, "Afdracht Scouting Nederland", RekeningSoort.Resultaat),
        (4100, "Opkomsten", RekeningSoort.Resultaat),
        (4110, "Zomerkamp", RekeningSoort.Resultaat),
        (4200, "Huur clubhuis", RekeningSoort.Resultaat),
        (4310, "Bankkosten", RekeningSoort.Resultaat),
        (8400, "Huuropbrengsten", RekeningSoort.Resultaat),
    ];

    /// <summary>Maakt de database; alle posten vallen in het jaar van <paramref name="nu"/>.</summary>
    public static async Task MaakAsync(string pad, DateTimeOffset nu)
    {
        var tijd = new VasteTijd(nu);
        var beheer = new DatabaseBeheer();
        await beheer.MaakAanAsync(pad, Wachtwoord);
        await using var sessie = await beheer.OpenAsync(pad, Wachtwoord);

        foreach (var administratie in await sessie.Administraties.HaalAlleAsync())
        {
            administratie.WijzigNaam(administratie.Soort == AdministratieSoort.Vereniging ? NaamVereniging : NaamStichting);
            await sessie.Administraties.BewaarAsync(administratie);
        }

        await VulBoekhoudingAsync(sessie, tijd, nu);
    }

    public static Task<IDatabaseSessie> OpenAsync(string pad) => new DatabaseBeheer().OpenAsync(pad, Wachtwoord);

    private static async Task VulBoekhoudingAsync(IDatabaseSessie sessie, TimeProvider tijd, DateTimeOffset nu)
    {
        const AdministratieSoort ver = AdministratieSoort.Vereniging;
        var dienst = new BoekhoudingDienst(sessie, tijd);
        await dienst.RichtInAsync(ver);
        var id = new Dictionary<int, int>();
        foreach (var (nummer, naam, soort) in Rekeningen)
        {
            id[nummer] = await dienst.BewaarRekeningAsync(ver, new RekeningInvoer(null, nummer, naam, null, soort, RekeningRol.Geen, true));
        }

        var memoriaal = (await dienst.LaadAsync(ver)).Dagboeken.First(d => d.Soort == DagboekSoort.Memoriaal).Id;
        var jaar = nu.Year;
        var laatsteMaand = Math.Max(1, nu.Month - 1);

        PostInvoer Post(DateOnly datum, string omschrijving, int debet, int credit, long centen) =>
            new(null, memoriaal, datum, omschrijving,
            [
                new RegelInvoerId(id[debet], null, new Bedrag(centen), Bedrag.Nul),
                new RegelInvoerId(id[credit], null, Bedrag.Nul, new Bedrag(centen)),
            ]);

        for (var maand = 1; maand <= laatsteMaand; maand++)
        {
            var dag = new DateOnly(jaar, maand, 1);
            await dienst.BoekAsync(ver, Post(dag.AddDays(4), "Huuropbrengsten", 1100, 8400, 22_000));
            await dienst.BoekAsync(ver, Post(dag.AddDays(9), "Huur clubhuis", 4200, 1100, 15_000));
            await dienst.BoekAsync(ver, Post(dag.AddDays(14), "Materiaal opkomsten", 4100, 1000, 2_350 + maand * 115));
        }

        await dienst.BoekAsync(ver, Post(new DateOnly(jaar, 1, 1), "Beginsaldo spaarpot", 1100, 600, 450_000));
        await dienst.BoekAsync(ver, Post(new DateOnly(jaar, Math.Min(laatsteMaand, 3), 20), "Afdracht Scouting Nederland", 4000, 1100, 86_400));
        await dienst.BoekAsync(ver, Post(new DateOnly(jaar, Math.Min(laatsteMaand, 5), 12), "Oliebollenactie", 1000, 8400, 64_250));
        await dienst.BewaarConceptAsync(ver, Post(new DateOnly(jaar, laatsteMaand, 25), "Zomerkamp: kampterrein", 4110, 1100, 120_000));
    }

    private sealed class VasteTijd(DateTimeOffset nu) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nu;
    }
}
