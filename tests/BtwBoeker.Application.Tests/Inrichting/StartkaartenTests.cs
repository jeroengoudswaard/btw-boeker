using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Inrichting;
using BtwBoeker.Application.Tests.Bijlagen;
using BtwBoeker.Application.Tests.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Bank;
using BtwBoeker.Domain.Financien.Bijlagen;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Tests.Inrichting;

/// <summary>Besluit 183: kaarten op de startpagina, alleen als er iets te doen is.</summary>
public sealed class StartkaartenTests
{
    private static readonly DateOnly Vandaag = new(2026, 11, 3);
    private static readonly DateTime Nu = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Inrichtingsstatus AllesKlaar = new(true, true, true, true, true, true);

    private static BoekhoudingStand Leeg(AdministratieSoort soort) => new(soort, [], [], [], [], []);

    [Fact]
    public void Niets_te_doen_geeft_geen_kaarten()
    {
        var kaarten = Startkaarten.Bepaal(AllesKlaar, Startwizardvoortgang.Nieuw, new BoekhoudingTestdata().Stand, Leeg(AdministratieSoort.Stichting), Vandaag);

        Assert.Empty(kaarten);
    }

    [Fact]
    public void Inrichting_afmaken_toont_het_aantal_klare_stappen_tot_de_wizard_is_voltooid()
    {
        var status = AllesKlaar with { NamenIngevuld = false, BankVereniging = false };

        var kaart = Assert.Single(Startkaarten.Bepaal(status, Startwizardvoortgang.Nieuw, Leeg(AdministratieSoort.Vereniging), Leeg(AdministratieSoort.Stichting), Vandaag));

        Assert.Equal(Startkaartsoort.Inrichting, kaart.Soort);
        Assert.Equal("2 van 6 stappen klaar", kaart.Tekst);
        Assert.Empty(Startkaarten.Bepaal(status, new Startwizardvoortgang(null, IsAfgerond: true), Leeg(AdministratieSoort.Vereniging), Leeg(AdministratieSoort.Stichting), Vandaag));
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

        var kaarten = Startkaarten.Bepaal(AllesKlaar, Startwizardvoortgang.Nieuw, data.Stand, Leeg(AdministratieSoort.Stichting), Vandaag);

        Assert.Equal(2, Kaart(kaarten, Startkaartsoort.Bank).Aantal);
        Assert.Equal(1, Kaart(kaarten, Startkaartsoort.Concepten).Aantal);
        Assert.Equal(1, Kaart(kaarten, Startkaartsoort.Postvak).Aantal);
        Assert.Equal(1, Kaart(kaarten, Startkaartsoort.ZonderBon).Aantal);
        Assert.All(kaarten, k => Assert.Equal(AdministratieSoort.Vereniging, k.Administratie));
        Assert.Equal("2 banktransacties te verwerken", Kaart(kaarten, Startkaartsoort.Bank).Titel);
    }

    [Fact]
    public void Zonder_bon_gebruikt_de_drempel_van_de_administratie()
    {
        var data = new BoekhoudingTestdata();
        var bank = data.Bankdagboek("BNK", "NL91ABNA0417164300");
        var uitgave = new Journaalpost(AdministratieSoort.Vereniging, bank, new DateOnly(2026, 10, 1), "Bouwmarkt");
        uitgave.Wijzig(bank, uitgave.Datum, "Bouwmarkt",
            [new RegelInvoer(data.Energie, null, new Bedrag(2500), Bedrag.Nul), new RegelInvoer(data.Bank, null, Bedrag.Nul, new Bedrag(2500))]);
        uitgave.Boek(1, Nu);
        data.Posten.Add(uitgave);
        var drempels = new Dictionary<AdministratieSoort, Bedrag> { [AdministratieSoort.Vereniging] = new(5000) };

        var kaarten = Startkaarten.Bepaal(AllesKlaar, Startwizardvoortgang.Nieuw, data.Stand, Leeg(AdministratieSoort.Stichting), Vandaag, drempels);
        Assert.DoesNotContain(kaarten, k => k.Soort == Startkaartsoort.ZonderBon); // € 25 ligt onder de drempel van € 50

        drempels[AdministratieSoort.Vereniging] = new(2000);
        var kaart = Kaart(Startkaarten.Bepaal(AllesKlaar, Startwizardvoortgang.Nieuw, data.Stand, Leeg(AdministratieSoort.Stichting), Vandaag, drempels),
            Startkaartsoort.ZonderBon);
        Assert.Contains("€ 20,00", kaart.Tekst, StringComparison.Ordinal);
    }

    private static Startkaart Kaart(IEnumerable<Startkaart> kaarten, Startkaartsoort soort) => Assert.Single(kaarten, k => k.Soort == soort);
}
