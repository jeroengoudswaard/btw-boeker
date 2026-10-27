using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.Domain.Tests.Leden;

public class PersoonTests
{
    private static readonly DateTime Nu = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Importdatum = new(2026, 10, 27);
    private static readonly Groep Welpen = new("Welpen", GroepSoort.Speltak);
    private static readonly Groep Migratie = new("Vrijwilligers (Migratie)", GroepSoort.Technisch);

    private static readonly Dictionary<string, Groep> Groepen = new(StringComparer.Ordinal)
    {
        [Welpen.Naam] = Welpen,
        [Migratie.Naam] = Migratie,
    };

    private static LidGegevens Gegevens(string voornaam = "Anna", string? email = "anna@example.org") =>
        new(voornaam, "de", "Vries", new DateOnly(2015, 3, 1), "Vrouw", email, "+31612345678", "003Ab00000AbCdE",
            "Actief", "Dorpsstraat", "1", null, "1234 AB", "Ergens", "Nederland");

    private static Persoon NieuwePersoon() => new(Lidnummer.Parse("100000001"), Gegevens(), Nu);

    [Fact]
    public void Nieuwe_persoon_krijgt_vaste_id_en_herkomst_sol3()
    {
        var persoon = NieuwePersoon();

        Assert.NotEqual(Guid.Empty, persoon.Id);
        Assert.Equal(Nu, persoon.LaatstGewijzigdUtc);
        Assert.Contains(persoon.Herkomst, h => h.Veld == "Voornaam" && h.Bron == Veldbron.Sol3);
    }

    [Fact]
    public void Volledige_naam_slaat_lege_delen_over()
    {
        Assert.Equal("Anna de Vries", NieuwePersoon().VolledigeNaam);
        Assert.Equal("Bo Jansen", new Persoon(Lidnummer.Parse("2"), Gegevens("Bo") with { Tussenvoegsel = "", Achternaam = "Jansen" }, Nu).VolledigeNaam);
    }

    [Fact]
    public void Vergelijk_gelijke_gegevens_geeft_geen_wijzigingen()
    {
        Assert.Empty(NieuwePersoon().VergelijkGegevens(Gegevens()));
    }

    [Fact]
    public void Vergelijk_geeft_oud_en_nieuw_per_veld()
    {
        var wijzigingen = NieuwePersoon().VergelijkGegevens(Gegevens(email: "nieuw@example.org"));

        var wijziging = Assert.Single(wijzigingen);
        Assert.Equal(new Veldwijziging("E-mail", "anna@example.org", "nieuw@example.org"), wijziging);
    }

    [Fact]
    public void Vergelijk_contactpersoon_id_is_hoofdlettergevoelig()
    {
        var wijzigingen = NieuwePersoon().VergelijkGegevens(Gegevens() with { ContactpersoonId = "003AB00000ABCDE" });

        Assert.Equal("Contactpersoon-ID", Assert.Single(wijzigingen).Veld);
    }

    [Fact]
    public void Bijwerken_zet_herkomst_en_tijdstip_alleen_bij_wijziging()
    {
        var persoon = NieuwePersoon();
        var later = Nu.AddDays(1);

        persoon.WerkGegevensBij(Gegevens(), later);
        Assert.Equal(Nu, persoon.LaatstGewijzigdUtc);

        persoon.WerkGegevensBij(Gegevens(email: "nieuw@example.org"), later);
        Assert.Equal("nieuw@example.org", persoon.Email);
        Assert.Equal(later, persoon.LaatstGewijzigdUtc);
        Assert.Equal(later, persoon.Herkomst.Single(h => h.Veld == "E-mail").GewijzigdUtc);
    }

    [Fact]
    public void Notities_hebben_herkomst_verenigd()
    {
        var persoon = NieuwePersoon();

        persoon.WijzigNotities("  Allergie voor noten  ", Nu.AddHours(1));

        Assert.Equal("Allergie voor noten", persoon.Notities);
        Assert.Equal(Veldbron.Verenigd, persoon.Herkomst.Single(h => h.Veld == "Notities").Bron);
    }

    [Fact]
    public void Lege_notities_worden_null()
    {
        var persoon = NieuwePersoon();
        persoon.WijzigNotities("   ", Nu);
        Assert.Null(persoon.Notities);
    }

    [Fact]
    public void Lid_sinds_is_vroegste_startdatum_ook_van_technische_groep()
    {
        var persoon = NieuwePersoon();
        persoon.WerkFunctiesBij(
        [
            new FunctieGegevens("Welpen", "Leider", Lidtype.Kaderlid, new DateOnly(2022, 1, 28), null, "Actief"),
            new FunctieGegevens("Vrijwilligers (Migratie)", "Lid", Lidtype.Kaderlid, new DateOnly(2018, 11, 7), null, "Actief"),
        ], Groepen, Importdatum, Nu);

        Assert.Equal(new DateOnly(2018, 11, 7), persoon.LidSinds);
    }

    [Fact]
    public void Lid_sinds_zonder_functies_is_null()
    {
        Assert.Null(NieuwePersoon().LidSinds);
    }

    [Fact]
    public void Nieuwe_functie_wordt_als_wijziging_getoond_en_toegevoegd()
    {
        var persoon = NieuwePersoon();
        FunctieGegevens[] nieuw = [new("Welpen", "Leider", Lidtype.Kaderlid, new DateOnly(2022, 1, 28), null, "Actief")];

        var wijziging = Assert.Single(persoon.VergelijkFuncties(nieuw, Importdatum));
        Assert.Equal("Functie", wijziging.Veld);
        Assert.Null(wijziging.Oud);
        Assert.Equal("Leider Welpen (vanaf 28-01-2022)", wijziging.Nieuw);

        persoon.WerkFunctiesBij(nieuw, Groepen, Importdatum, Nu);
        var functie = Assert.Single(persoon.Functies);
        Assert.Same(Welpen, functie.Groep);
        Assert.Empty(persoon.VergelijkFuncties(nieuw, Importdatum));
    }

    [Fact]
    public void Verdwenen_open_functie_krijgt_importdatum_als_einddatum()
    {
        var persoon = NieuwePersoon();
        persoon.WerkFunctiesBij(
            [new FunctieGegevens("Welpen", "Leider", Lidtype.Kaderlid, new DateOnly(2022, 1, 28), null, "Actief")],
            Groepen, Importdatum, Nu);

        var wijziging = Assert.Single(persoon.VergelijkFuncties([], Importdatum));
        Assert.Equal("Leider Welpen (vanaf 28-01-2022)", wijziging.Oud);
        Assert.Equal("Leider Welpen (vanaf 28-01-2022, tot 27-10-2026)", wijziging.Nieuw);

        persoon.WerkFunctiesBij([], Groepen, Importdatum, Nu);
        Assert.Equal(Importdatum, Assert.Single(persoon.Functies).Einddatum);

        // Een beëindigde functie blijft bewaard en geeft bij een volgende import geen wijziging meer.
        Assert.Empty(persoon.VergelijkFuncties([], Importdatum.AddDays(7)));
    }

    [Fact]
    public void Gewijzigde_einddatum_wordt_bijgewerkt()
    {
        var persoon = NieuwePersoon();
        var start = new DateOnly(2025, 9, 19);
        persoon.WerkFunctiesBij([new FunctieGegevens("Welpen", "Lid", Lidtype.Jeugdlid, start, null, "Actief")], Groepen, Importdatum, Nu);

        FunctieGegevens[] nieuw = [new("Welpen", "Lid", Lidtype.Jeugdlid, start, new DateOnly(2026, 11, 30), "Actief")];
        Assert.Single(persoon.VergelijkFuncties(nieuw, Importdatum));

        persoon.WerkFunctiesBij(nieuw, Groepen, Importdatum, Nu);
        var functie = Assert.Single(persoon.Functies);
        Assert.Equal(new DateOnly(2026, 11, 30), functie.Einddatum);
        Assert.True(functie.IsActief(Importdatum)); // einddatum in de toekomst
        Assert.False(functie.IsActief(new DateOnly(2026, 12, 1)));
    }

    [Fact]
    public void Uitschrijven_sluit_open_functies_en_herinschrijven_maakt_ongedaan()
    {
        var persoon = NieuwePersoon();
        persoon.WerkFunctiesBij(
            [new FunctieGegevens("Welpen", "Lid", Lidtype.Jeugdlid, new DateOnly(2024, 3, 23), null, "Actief")],
            Groepen, Importdatum, Nu);

        persoon.Uitschrijven(Importdatum, Nu);

        Assert.True(persoon.IsUitgeschreven);
        Assert.Equal(Importdatum, persoon.UitgeschrevenOp);
        Assert.Equal(Importdatum, Assert.Single(persoon.Functies).Einddatum);

        persoon.Herinschrijven(Nu);
        Assert.False(persoon.IsUitgeschreven);
    }

    [Fact]
    public void Voogden_worden_per_volgnummer_vergeleken_en_bijgewerkt()
    {
        var persoon = NieuwePersoon();
        VoogdGegevens[] voogden =
        [
            new(1, "P. de Vries", false, "p@example.org", null, "+31612345678"),
            new(2, null, true, "q@example.org", null, null),
        ];

        Assert.Equal(2, persoon.VergelijkVoogden(voogden).Count);
        persoon.WerkVoogdenBij(voogden, Nu);
        Assert.Equal(2, persoon.Voogden.Count);
        Assert.Empty(persoon.VergelijkVoogden(voogden));

        VoogdGegevens[] alleenEerste = [voogden[0] with { Email = "nieuw@example.org" }];
        var wijzigingen = persoon.VergelijkVoogden(alleenEerste);
        Assert.Contains(new Veldwijziging("Voogd 1 e-mail", "p@example.org", "nieuw@example.org"), wijzigingen);
        Assert.Contains(wijzigingen, w => w.Veld == "Voogd 2" && w.Nieuw is null);

        persoon.WerkVoogdenBij(alleenEerste, Nu);
        Assert.Equal("nieuw@example.org", Assert.Single(persoon.Voogden).Email);
    }

    [Fact]
    public void Groep_stelt_soort_voor_op_basis_van_naam()
    {
        Assert.Equal(GroepSoort.Technisch, Groep.VoorgesteldeSoort("Vrijwilligers (Migratie)"));
        Assert.Equal(GroepSoort.Bestuur, Groep.VoorgesteldeSoort("water bestuur"));
        Assert.Equal(GroepSoort.Bestuur, Groep.VoorgesteldeSoort("water sticht./ver.be"));
        Assert.Equal(GroepSoort.Speltak, Groep.VoorgesteldeSoort("Welpen"));
    }
}
