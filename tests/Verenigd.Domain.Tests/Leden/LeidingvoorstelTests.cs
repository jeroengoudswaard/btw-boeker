using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.Domain.Tests.Leden;

/// <summary>Besluiten 34 en 38: een goedgekeurd voorstel van de leiding wijzigt het veld met bron "leiding".</summary>
public class LeidingvoorstelTests
{
    private static readonly DateTime Aangemaakt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Nu = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    private static Persoon Lid()
    {
        var persoon = new Persoon(Lidnummer.Parse("100000001"),
            new LidGegevens("Bram", "de", "Jong", new DateOnly(2018, 3, 1), null, "bram@example.org", "+31612345678", null,
                "Actief", null, null, null, null, null, null), Aangemaakt);
        persoon.WerkVoogdenBij([new VoogdGegevens(1, "Voogd Een", false, "een@example.org", null, "+31611111111")], Aangemaakt);
        return persoon;
    }

    [Fact]
    public void Email_van_het_lid_wordt_overgenomen_met_bron_leiding()
    {
        var lid = Lid();

        lid.NeemVoorstelOver(Leidingveld.Email, "  nieuw@example.org ", Nu);

        Assert.Equal("nieuw@example.org", lid.Email);
        Assert.Contains(lid.Herkomst, h => h.Veld == "E-mail" && h.Bron == Veldbron.Leiding && h.GewijzigdUtc == Nu);
        Assert.Equal(Nu, lid.LaatstGewijzigdUtc);
    }

    [Fact]
    public void Telefoon_wordt_genormaliseerd_besluit_32()
    {
        var lid = Lid();

        lid.NeemVoorstelOver(Leidingveld.Telefoon, "06 87654321", Nu);

        Assert.Equal("+31687654321", lid.Telefoon);
    }

    [Fact]
    public void Leeg_maakt_het_veld_leeg()
    {
        var lid = Lid();

        lid.NeemVoorstelOver(Leidingveld.Email, " ", Nu);

        Assert.Null(lid.Email);
    }

    [Fact]
    public void Mobiel_van_voogd_1()
    {
        var lid = Lid();

        lid.NeemVoorstelOver("voogd1_mobiel", "06-87654321", Nu);

        var voogd = Assert.Single(lid.Voogden);
        Assert.Equal("+31687654321", voogd.Mobiel);
        Assert.Equal("een@example.org", voogd.Email);
        Assert.Contains(lid.Herkomst, h => h.Veld == "Voogden" && h.Bron == Veldbron.Leiding);
    }

    [Fact]
    public void Naam_van_voogd_maakt_naam_bekend()
    {
        var lid = Lid();
        lid.WerkVoogdenBij([new VoogdGegevens(1, null, true, "een@example.org", null, null)], Aangemaakt);

        lid.NeemVoorstelOver("voogd1_naam", "Petra Jong", Nu);

        Assert.Equal("Petra Jong", lid.Voogden[0].Naam);
        Assert.False(lid.Voogden[0].NaamOnbekend);
    }

    [Fact]
    public void Een_ontbrekende_tweede_voogd_wordt_aangemaakt()
    {
        var lid = Lid();

        lid.NeemVoorstelOver("voogd2_email", "twee@example.org", Nu);

        var tweede = Assert.Single(lid.Voogden, v => v.Volgnummer == 2);
        Assert.Equal("twee@example.org", tweede.Email);
        Assert.True(tweede.NaamOnbekend);
    }

    [Theory]
    [InlineData("geboortedatum")]
    [InlineData("voogd3_email")]
    [InlineData("iban")]
    public void Alleen_de_velden_die_de_leiding_mag_voorstellen(string veld)
    {
        Assert.Throws<ArgumentException>(() => Lid().NeemVoorstelOver(veld, "x", Nu));
    }

    [Fact]
    public void Huidige_waarde_voor_vergelijking_met_oud()
    {
        var lid = Lid();

        Assert.Equal("bram@example.org", Leidingveld.HuidigeWaarde(lid, Leidingveld.Email));
        Assert.Equal("+31611111111", Leidingveld.HuidigeWaarde(lid, "voogd1_mobiel"));
        Assert.Null(Leidingveld.HuidigeWaarde(lid, "voogd2_naam"));
    }

    [Theory]
    [InlineData("email", "E-mail")]
    [InlineData("telefoon", "Telefoon")]
    [InlineData("voogd1_naam", "Voogd 1 · naam")]
    [InlineData("voogd2_mobiel", "Voogd 2 · mobiel")]
    public void Omschrijving_voor_de_wachtrij(string veld, string omschrijving)
    {
        Assert.Equal(omschrijving, Leidingveld.Omschrijving(veld));
    }

    [Fact]
    public void De_lijst_velden_is_gelijk_aan_die_van_de_server()
    {
        // Dezelfde lijst staat in de check op public.voorstellen.veld (supabase/migrations).
        Assert.Equal(
            ["email", "telefoon", "voogd1_naam", "voogd1_email", "voogd1_telefoon", "voogd1_mobiel", "voogd2_naam", "voogd2_email", "voogd2_telefoon", "voogd2_mobiel"],
            Leidingveld.Alle);
    }
}
