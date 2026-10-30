using Verenigd.Application.Boekhouding;
using Verenigd.Application.Incasso;
using Verenigd.Application.Inrichting;
using Verenigd.Application.Tests.Bijlagen;
using Verenigd.Application.Tests.Boekhouding;
using Verenigd.Application.Tests.Incasso;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Bank;
using Verenigd.Domain.Financien.Bijlagen;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Tests.Inrichting;

/// <summary>Besluit 183: kaarten op de startpagina, alleen als er iets te doen is.</summary>
public sealed class StartkaartenTests
{
    private static readonly DateOnly Vandaag = new(2026, 11, 3);
    private static readonly DateTime Nu = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);
    private static readonly IncassoStand GeenIncasso = new([], [], [], [], [], [], [], null);
    private static readonly Inrichtingsstatus AllesKlaar = new(true, true, true, true, false, true, true, true);

    private static BoekhoudingStand Leeg(AdministratieSoort soort) => new(soort, [], [], [], [], []);

    [Fact]
    public void Niets_te_doen_geeft_geen_kaarten()
    {
        var kaarten = Startkaarten.Bepaal(AllesKlaar, Startwizardvoortgang.Nieuw, new BoekhoudingTestdata().Stand, Leeg(AdministratieSoort.Stichting), GeenIncasso, Vandaag);

        Assert.Empty(kaarten);
    }

    [Fact]
    public void Inrichting_afmaken_toont_het_aantal_klare_stappen_tot_de_wizard_is_voltooid()
    {
        var status = AllesKlaar with { HeeftLeden = false, BankVereniging = false };

        var kaart = Assert.Single(Startkaarten.Bepaal(status, Startwizardvoortgang.Nieuw, Leeg(AdministratieSoort.Vereniging), Leeg(AdministratieSoort.Stichting), GeenIncasso, Vandaag));

        Assert.Equal(Startkaartsoort.Inrichting, kaart.Soort);
        Assert.Equal("4 van 6 stappen klaar", kaart.Tekst);
        Assert.Empty(Startkaarten.Bepaal(status, new Startwizardvoortgang(null, IsAfgerond: true), Leeg(AdministratieSoort.Vereniging), Leeg(AdministratieSoort.Stichting), GeenIncasso, Vandaag));
    }

    [Fact]
    public void Bank_concepten_postvak_en_zonder_bon_per_administratie()
    {
        var data = new BoekhoudingTestdata();
        var bank = data.Bankdagboek("BNK", "NL91ABNA0417164300");
        data.Bankmutaties.Add(new Bankmutatie(AdministratieSoort.Vereniging, bank, "a", new DateOnly(2026, 10, 1), new Bedrag(2500), false, default, null, null, null, null, null, null));
        data.Bankmutaties.Add(new Bankmutatie(AdministratieSoort.Vereniging, bank, "b", new DateOnly(2026, 10, 2), new Bedrag(500), true, default, null, null, null, null, null, null));
        data.Posten.Add(new Journaalpost(AdministratieSoort.Vereniging, data.Memoriaal, new DateOnly(2026, 10, 5), "concept"));
        data.Bijlagen.Add(new Bijlage(AdministratieSoort.Vereniging, "bon.pdf", BijlagenTests.Pdf(), Nu));
        var uitgave = new Journaalpost(AdministratieSoort.Vereniging, bank, new DateOnly(2026, 10, 1), "Bouwmarkt");
        uitgave.Wijzig(bank, uitgave.Datum, "Bouwmarkt",
            [new RegelInvoer(data.Energie, null, new Bedrag(2500), Bedrag.Nul), new RegelInvoer(data.Bank, null, Bedrag.Nul, new Bedrag(2500))]);
        uitgave.Boek(1, Nu);
        data.Posten.Add(uitgave);

        var kaarten = Startkaarten.Bepaal(AllesKlaar, Startwizardvoortgang.Nieuw, data.Stand, Leeg(AdministratieSoort.Stichting), GeenIncasso, Vandaag);

        Assert.Equal(2, Kaart(kaarten, Startkaartsoort.Bank).Aantal);
        Assert.Equal(1, Kaart(kaarten, Startkaartsoort.Concepten).Aantal);
        Assert.Equal(1, Kaart(kaarten, Startkaartsoort.Postvak).Aantal);
        Assert.Equal(1, Kaart(kaarten, Startkaartsoort.ZonderBon).Aantal);
        Assert.All(kaarten, k => Assert.Equal(AdministratieSoort.Vereniging, k.Administratie));
        Assert.Equal("2 banktransacties te verwerken", Kaart(kaarten, Startkaartsoort.Bank).Titel);
    }

    [Fact]
    public void Incassorun_van_deze_maand_ontbreekt_alleen_als_de_incasso_is_ingesteld()
    {
        var incasso = new IncassoTestdata(); // ingesteld vanaf januari 2026, geen runs

        var kaart = Kaart(Startkaarten.Bepaal(AllesKlaar, Startwizardvoortgang.Nieuw, Leeg(AdministratieSoort.Vereniging), Leeg(AdministratieSoort.Stichting), incasso.Stand, Vandaag),
            Startkaartsoort.Incassorun);

        Assert.Equal("Incassorun november 2026 nog niet gemaakt", kaart.Titel);
        Assert.DoesNotContain(Startkaarten.Bepaal(AllesKlaar, Startwizardvoortgang.Nieuw, Leeg(AdministratieSoort.Vereniging), Leeg(AdministratieSoort.Stichting), GeenIncasso, Vandaag),
            k => k.Soort == Startkaartsoort.Incassorun);
    }

    private static Startkaart Kaart(IEnumerable<Startkaart> kaarten, Startkaartsoort soort) => Assert.Single(kaarten, k => k.Soort == soort);
}
