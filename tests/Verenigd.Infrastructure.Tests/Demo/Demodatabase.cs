using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Application.Import;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Infrastructure.Database;
using static Verenigd.Application.Tests.Import.Sol3Voorbeeld;

namespace Verenigd.Infrastructure.Tests.Demo;

/// <summary>
/// Besluit 252: een demodatabase met alleen verzonnen gegevens, voor de schermafbeeldingen op de website.
/// Gedeeld met de UI-test <c>Schermafbeeldingen</c> (Verenigd.App.Tests). Alle e-mailadressen eindigen op example.org.
/// </summary>
internal static class Demodatabase
{
    public const string Wachtwoord = "demo voorbeeldgroep";
    public const string NaamVereniging = "Scouting De Voorbeeldgroep";
    public const string NaamStichting = "Stichting Het Voorbeeldhonk";

    /// <summary>Verzonnen leden: lidnummer, voornaam, tussenvoegsel, achternaam, groep, rol, lidtype.</summary>
    public static readonly IReadOnlyList<(string Nummer, string Voornaam, string Tussen, string Achternaam, string Groep, string Rol, string Type)> Leden =
    [
        ("100000101", "Sanne", "", "Visser", "Bevers", "Leider", "Kaderlid"),
        ("100000102", "Mees", "de", "Boer", "Bevers", "Lid", "Jeugdlid"),
        ("100000103", "Lotte", "", "Jansen", "Bevers", "Lid", "Jeugdlid"),
        ("100000104", "Daan", "van", "Dijk", "Bevers", "Lid", "Jeugdlid"),
        ("100000201", "Eva", "", "Smit", "Welpen", "Akela", "Kaderlid"),
        ("100000202", "Gijs", "", "Meijer", "Welpen", "Leider", "Kaderlid"),
        ("100000203", "Anna", "", "Bakker", "Welpen", "Lid", "Jeugdlid"),
        ("100000204", "Bram", "de", "Wit", "Welpen", "Lid", "Jeugdlid"),
        ("100000205", "Finn", "", "Mulder", "Welpen", "Lid", "Jeugdlid"),
        ("100000206", "Hanna", "de", "Groot", "Welpen", "Lid", "Jeugdlid"),
        ("100000207", "Ilse", "", "Peters", "Welpen", "Lid", "Jeugdlid"),
        ("100000301", "Thijs", "", "Bos", "Scouts", "Leider", "Kaderlid"),
        ("100000302", "Noor", "", "Vos", "Scouts", "Lid", "Jeugdlid"),
        ("100000303", "Sem", "", "Hendriks", "Scouts", "Lid", "Jeugdlid"),
        ("100000304", "Julia", "van der", "Berg", "Scouts", "Lid", "Jeugdlid"),
        ("100000305", "Luuk", "", "Dekker", "Scouts", "Lid", "Jeugdlid"),
        ("100000401", "Femke", "", "Brouwer", "Explorers", "Leider", "Kaderlid"),
        ("100000402", "Ruben", "", "Kok", "Explorers", "Lid", "Jeugdlid"),
        ("100000403", "Maud", "", "Jacobs", "Explorers", "Lid", "Jeugdlid"),
        ("100000501", "Pieter", "", "Willems", "Bestuur", "Penningmeester", "Kaderlid"),
    ];

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
        (8010, "Contributie Bevers", RekeningSoort.Resultaat),
        (8020, "Contributie Welpen", RekeningSoort.Resultaat),
        (8030, "Contributie Scouts", RekeningSoort.Resultaat),
        (8040, "Contributie Explorers", RekeningSoort.Resultaat),
        (8400, "Acties", RekeningSoort.Resultaat),
    ];

    /// <summary>Maakt de database (met <paramref name="werkmap"/> voor de tijdelijke SOL3-bestanden). Alle posten vallen in het jaar van <paramref name="nu"/>.</summary>
    public static async Task MaakAsync(string pad, string werkmap, DateTimeOffset nu)
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

        await ImporteerLedenAsync(sessie, werkmap, tijd);
        await VulBoekhoudingAsync(sessie, tijd, nu);
    }

    public static Task<IDatabaseSessie> OpenAsync(string pad) => new DatabaseBeheer().OpenAsync(pad, Wachtwoord);

    private static async Task ImporteerLedenAsync(IDatabaseSessie sessie, string werkmap, TimeProvider tijd)
    {
        string Schrijf(string naam, byte[] inhoud)
        {
            var bestand = Path.Combine(werkmap, naam);
            File.WriteAllBytes(bestand, inhoud);
            return bestand;
        }

        static string Mail(string voornaam, string achternaam) => $"{voornaam}.{achternaam}@example.org".ToLowerInvariant().Replace(' ', '-');

        var leden = Leden.Select(l => Lid(l.Nummer, l.Voornaam, l.Achternaam, l.Tussen, geboortedatum: l.Type == "Jeugdlid" ? "12-05-2015" : "03-02-1995",
            email: Mail(l.Voornaam, l.Achternaam), telefoon: "06-12345678", eenheid: l.Groep)).ToArray();
        var huishoudens = Leden.Select(l => Huishouden(l.Nummer)).ToArray();
        var voogden = Leden.Where(l => l.Type == "Jeugdlid")
            .Select(l => Voogden(l.Nummer, naam1: "Ouder " + l.Achternaam, email1: Mail("ouder", l.Achternaam))).ToArray();
        var functies = Leden.Select(l => Functie(l.Nummer, l.Rol, l.Groep, type: l.Type)).ToArray();

        var dienst = new Sol3ImportDienst(sessie, tijd);
        var analyse = await dienst.AnalyseerAsync(new Dictionary<Sol3Export, string>
        {
            [Sol3Export.LedenBasis] = Schrijf("demo-1.csv", Csv(KopLeden, leden)),
            [Sol3Export.Huishouden] = Schrijf("demo-2.csv", Csv(KopHuishouden, huishoudens)),
            [Sol3Export.Voogden] = Schrijf("demo-3.csv", Csv(KopVoogden, voogden)),
            [Sol3Export.Functies] = Schrijf("demo-4.csv", Csv(KopFuncties, functies)),
        });
        await dienst.VoerUitAsync(analyse, ImportKeuzes.Standaard(analyse.Voorstel));
    }

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
            await dienst.BoekAsync(ver, Post(dag.AddDays(4), "Contributie Welpen", 1100, 8020, 12_000));
            await dienst.BoekAsync(ver, Post(dag.AddDays(4), "Contributie Scouts", 1100, 8030, 10_000));
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
