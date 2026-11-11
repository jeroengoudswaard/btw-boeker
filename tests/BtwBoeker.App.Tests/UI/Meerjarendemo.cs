using BtwBoeker.Application.Bank;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Database;
using BtwBoeker.Application.Tests.Bank;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Bank;
using BtwBoeker.Domain.Financien.Boekhouding;
using BtwBoeker.Infrastructure.Database;

namespace BtwBoeker.App.Tests.UI;

/// <summary>
/// Een demodatabase met een paar jaar verzonnen huurinkomsten en kosten van een vakantiehuis, voor de snelheidsmeting.
/// Per jaar: een bankafschrift per maand (CAMT.053) dat met boekingsregels wordt verwerkt, een memoriaalpost per maand
/// voor de beheerkosten van het park, en de afsluiting van het jaar. Het vorige en het lopende jaar blijven open; in de
/// laatste maand blijven concepten en onbekende transacties staan, zodat Start en de bank werk tonen.
/// Alle namen en bedragen zijn verzonnen en voorspelbaar (geen toeval), zodat een meting herhaalbaar is.
/// </summary>
internal static class Meerjarendemo
{
    public const string Wachtwoord = "demo meerdere jaren";
    public const string Administratienaam = "Vakantiehuis Het Voorbeeld";
    private const AdministratieSoort Adm = AdministratieSoort.Standaard;

    private static readonly (int Nummer, string Naam, RekeningSoort Soort, RekeningRol Rol)[] Rekeningen =
    [
        (500, "Algemene reserve", RekeningSoort.Balans, RekeningRol.Reserve),
        (600, "Eigen vermogen", RekeningSoort.Balans, RekeningRol.Geen),
        (1100, "Bank", RekeningSoort.Balans, RekeningRol.Geen),
        (4000, "Parkbijdrage", RekeningSoort.Resultaat, RekeningRol.Geen),
        (4010, "Beheerkosten park", RekeningSoort.Resultaat, RekeningRol.Geen),
        (4100, "Klein onderhoud", RekeningSoort.Resultaat, RekeningRol.Geen),
        (4110, "Groot onderhoud", RekeningSoort.Resultaat, RekeningRol.Geen),
        (4150, "Schoonmaak", RekeningSoort.Resultaat, RekeningRol.Geen),
        (4200, "Energie", RekeningSoort.Resultaat, RekeningRol.Geen),
        (4210, "Water", RekeningSoort.Resultaat, RekeningRol.Geen),
        (4300, "Verzekering", RekeningSoort.Resultaat, RekeningRol.Geen),
        (4320, "Internet en tv", RekeningSoort.Resultaat, RekeningRol.Geen),
        (4400, "Gemeentelijke belastingen", RekeningSoort.Resultaat, RekeningRol.Geen),
        (4900, "Bankkosten", RekeningSoort.Resultaat, RekeningRol.Geen),
        (8400, "Omzet verhuur", RekeningSoort.Resultaat, RekeningRol.Geen),
    ];

    // Boekingsregel: deel van de naam van de tegenpartij -> rekening.
    private static readonly (string Naam, string NaamBevat, int Rekening)[] Regels =
    [
        ("Verhuur", "parkbeheer", 8400),
        ("Energie", "energie", 4200),
        ("Water", "waterbedrijf", 4210),
        ("Internet", "kabel", 4320),
        ("Bankkosten", "bank", 4900),
        ("Parkbijdrage", "parkbijdrage", 4000),
        ("Verzekering", "verzekeraar", 4300),
        ("Gemeente", "gemeente", 4400),
        ("Schoonmaak", "schoonmaak", 4150),
        ("Bouwmarkt", "bouwmarkt", 4100),
        ("Schilder", "schilder", 4110),
    ];

    /// <summary>Verhuurde weken per maand: rustig in de winter, vol in de zomer.</summary>
    private static readonly int[] Weken = [2, 2, 3, 3, 4, 4, 5, 5, 4, 3, 2, 3];

    /// <summary>
    /// Maakt de database met <paramref name="jaren"/> jaar gegevens tot en met de maand vóór <paramref name="nu"/>.
    /// Geeft het aantal geboekte posten en banktransacties terug.
    /// </summary>
    public static async Task<(int Posten, int Transacties)> MaakAsync(string pad, DateTimeOffset nu, int jaren = 5)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(jaren, 2);
        var tijd = new VasteTijd(nu);
        var beheer = new DatabaseBeheer();
        await beheer.MaakAanAsync(pad, Wachtwoord);
        await using var sessie = await beheer.OpenAsync(pad, Wachtwoord);

        var administratie = await sessie.Administraties.HaalAsync();
        administratie.WijzigNaam(Administratienaam);
        await sessie.Administraties.BewaarAsync(administratie);

        var boekhouding = new BoekhoudingDienst(sessie, tijd);
        var bank = new BankDienst(sessie, tijd);
        await boekhouding.RichtInAsync(Adm);
        var id = new Dictionary<int, int>();
        foreach (var (nummer, naam, soort, rol) in Rekeningen)
        {
            id[nummer] = await boekhouding.BewaarRekeningAsync(Adm, new RekeningInvoer(null, nummer, naam, null, soort, rol, true));
        }

        foreach (var (naam, bevat, rekening) in Regels)
        {
            await bank.BewaarRegelAsync(Adm, new BoekingsregelInvoer(null, naam, id[rekening], new BoekingsregelVoorwaarden(NaamBevat: bevat), null, true));
        }

        var eerste = nu.Year - jaren + 1;
        await boekhouding.OpenBoekjaarAsync(Adm, eerste);
        var memoriaal = (await boekhouding.LaadAsync(Adm)).Dagboeken.First(d => d.Soort == DagboekSoort.Memoriaal).Id;
        await boekhouding.BoekAsync(Adm, Memoriaalpost(memoriaal, new DateOnly(eerste, 1, 1), "Beginsaldo bank", id[1100], id[600], 1_500_000));

        var saldo = 1_500_000L;
        var transacties = 0;
        for (var jaar = eerste; jaar <= nu.Year; jaar++)
        {
            var laatsteMaand = jaar == nu.Year ? Math.Max(1, nu.Month - 1) : 12;
            var afschriften = new List<VbAfschrift>();
            for (var maand = 1; maand <= laatsteMaand; maand++)
            {
                var regels = Maandtransacties(jaar, maand, onbekendErbij: jaar == nu.Year && maand == laatsteMaand);
                var afschrift = new VbAfschrift(new DateOnly(jaar, maand, 28), saldo, [.. regels]);
                saldo = afschrift.Eind;
                afschriften.Add(afschrift);
                transacties += regels.Count;

                var omzet = Weken[maand - 1] * Weekprijs(jaar, maand);
                await boekhouding.BoekAsync(Adm, Memoriaalpost(memoriaal, new DateOnly(jaar, maand, 28),
                    $"Beheerkosten park {maand:00}-{jaar}", id[4010], id[8400], omzet / 5));
            }

            var bestand = Camt053Lezer.Lees(CamtVoorbeeld.Stroom(CamtVoorbeeld.Xml(CamtVoorbeeld.Betaal, "08", [.. afschriften])));
            var stand = await boekhouding.LaadAsync(Adm);
            await bank.ImporteerAsync(Adm, BankimportPlanner.Plan(stand, [bestand]), new Dictionary<string, NieuweBankrekening>
            {
                [CamtVoorbeeld.Betaal] = new(id[1100], "BNK", "Bank"),
            });

            if (jaar < nu.Year)
            {
                await bank.BoekConceptenAsync(Adm, null);
            }

            // Het vorige jaar blijft open (jaarrekening nog niet klaar); het lopende jaar bestaat al via RichtIn.
            if (jaar < nu.Year - 1)
            {
                await boekhouding.SluitBoekjaarAsync(Adm, jaar);
            }
        }

        var eind = await boekhouding.LaadAsync(Adm);
        return (eind.Posten.Count(p => p.IsGeboekt), transacties);
    }

    public static Task<IDatabaseSessie> OpenAsync(string pad) => new DatabaseBeheer().OpenAsync(pad, Wachtwoord);

    /// <summary>De weekprijs stijgt elk jaar 4% en is in de zomer hoger.</summary>
    private static long Weekprijs(int jaar, int maand)
    {
        var basis = 62_500L + (maand is >= 6 and <= 8 ? 20_000 : 0);
        for (var j = 2020; j < jaar; j++)
        {
            basis = basis * 104 / 100;
        }

        return basis;
    }

    private static List<VbTransactie> Maandtransacties(int jaar, int maand, bool onbekendErbij)
    {
        var t = new List<VbTransactie>();
        var volgnummer = 0;
        void Voeg(long centen, string naam, string omschrijving)
        {
            volgnummer++;
            var kenmerk = $"{jaar}{maand:00}{volgnummer:00}";
            t.Add(new VbTransactie(centen, centen < 0 ? "ICDT" : "RCDT", "ESCT", centen < 0 ? "520" : "541",
                Ref: "R" + kenmerk, Naam: naam, Omschrijving: omschrijving, E2e: "E" + kenmerk, NtryRef: kenmerk));
        }

        var omzet = Weken[maand - 1] * Weekprijs(jaar, maand);
        Voeg(omzet - (omzet / 5), "Parkbeheer De Voorbeeld BV", $"Afrekening verhuur {maand:00}-{jaar}");
        Voeg(-(14_500 + (jaar - 2020) * 900), "Energie Voorbeeld NV", $"Voorschot energie {maand:00}-{jaar}");
        Voeg(-4_995, "Kabel Voorbeeld BV", "Internet en tv");
        Voeg(-1_250, "Bank: kosten betaalrekening", "Kosten betaalrekening");
        Voeg(-Weken[maand - 1] * 4_500, "Schoonmaak Voorbeeld", $"Schoonmaak {Weken[maand - 1]} wisselingen");
        if (maand % 3 == 0)
        {
            Voeg(-6_850, "Waterbedrijf Voorbeeld", "Water kwartaal");
        }

        if (maand % 2 == 1)
        {
            Voeg(-(2_350 + maand * 315), "Bouwmarkt Voorbeeld", "Klein onderhoud");
        }

        switch (maand)
        {
            case 1:
                Voeg(-48_000, "Verzekeraar Voorbeeld", "Opstal- en inboedelverzekering");
                break;
            case 2:
                Voeg(-(240_000 + (jaar - 2020) * 6_000), "Parkbijdrage VvE Voorbeeld", $"Parkbijdrage {jaar}");
                break;
            case 3:
                Voeg(-61_200, "Gemeente Voorbeeld", "Gemeentelijke belastingen");
                break;
            case 9 when jaar % 2 == 0:
                Voeg(-185_000, "Schilder Voorbeeld", "Schilderwerk buitenkant");
                break;
        }

        if (onbekendErbij)
        {
            Voeg(12_500, "Familie Gast", "Borg retour onbekend");
        }

        return t;
    }

    private static PostInvoer Memoriaalpost(int dagboek, DateOnly datum, string omschrijving, int debet, int credit, long centen) =>
        new(null, dagboek, datum, omschrijving,
        [
            new RegelInvoerId(debet, null, new Bedrag(centen), Bedrag.Nul),
            new RegelInvoerId(credit, null, Bedrag.Nul, new Bedrag(centen)),
        ]);

    private sealed class VasteTijd(DateTimeOffset nu) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nu;
    }
}
