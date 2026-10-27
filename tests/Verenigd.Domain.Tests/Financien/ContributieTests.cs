using Verenigd.Domain.Financien;
using Verenigd.Domain.Gedeeld;

namespace Verenigd.Domain.Tests.Financien;

public class ContributieTests
{
    private static readonly Lidnummer Lid = Lidnummer.Parse("100000001");

    [Fact]
    public void Leiding_en_vrijgesteld_hebben_altijd_tarief_nul()
    {
        var leiding = new Contributiecategorie("Leiding", CategorieSoort.Leiding, null);

        Assert.False(leiding.HeeftTarief);
        Assert.Throws<InvalidOperationException>(() => new Tarief(leiding, 2027, new Bedrag(500)));
        Assert.Equal(Bedrag.Nul, new Tarief(leiding, 2027, Bedrag.Nul).Maandbedrag);
    }

    [Fact]
    public void Jeugdlidcategorie_kent_speltak()
    {
        var welpen = new Contributiecategorie("Jeugdlid Welpen", CategorieSoort.Jeugdlid, "Welpen");

        Assert.True(welpen.HeeftTarief);
        Assert.Equal("Welpen", welpen.Speltak);
    }

    [Fact]
    public void Categorie_op_datum_volgt_ingangsdatum()
    {
        var welpen = new Contributiecategorie("Jeugdlid Welpen", CategorieSoort.Jeugdlid, "Welpen") ;
        var junioren = new Contributiecategorie("Jeugdlid Junioren", CategorieSoort.Jeugdlid, "Junioren");
        LidCategorie[] historie =
        [
            new(Lid, welpen, new DateOnly(2024, 9, 1)),
            new(Lid, junioren, new DateOnly(2027, 3, 15)),
        ];

        Assert.Null(LidCategorie.Op(historie, Lid, new DateOnly(2024, 8, 1)));
        Assert.Same(welpen, LidCategorie.Op(historie, Lid, new DateOnly(2027, 3, 1))?.Categorie);
        Assert.Same(junioren, LidCategorie.Op(historie, Lid, new DateOnly(2027, 4, 1))?.Categorie);
        Assert.Null(LidCategorie.Op(historie, Lidnummer.Parse("2"), new DateOnly(2027, 4, 1)));
    }

    [Fact]
    public void Machtiging_actief_tussen_ondertekening_en_intrekking()
    {
        var betaler = new Betaler(Iban.Maak("NL91ABNA0417164300"), "P. de Vries");
        Assert.False(betaler.HeeftActieveMachtiging(new DateOnly(2027, 1, 1)));

        betaler.RegistreerMachtiging("VER-0001", new DateOnly(2019, 7, 1));
        Assert.True(betaler.HeeftActieveMachtiging(new DateOnly(2027, 1, 1)));
        Assert.False(betaler.HeeftActieveMachtiging(new DateOnly(2019, 6, 30)));

        betaler.TrekMachtigingIn(new DateOnly(2027, 2, 1));
        Assert.True(betaler.HeeftActieveMachtiging(new DateOnly(2027, 1, 31)));
        Assert.False(betaler.HeeftActieveMachtiging(new DateOnly(2027, 2, 1)));
    }

    [Fact]
    public void Ongeldig_mandaatkenmerk_wordt_geweigerd()
    {
        var betaler = new Betaler(Iban.Maak("NL91ABNA0417164300"), null);

        Assert.Throws<ArgumentException>(() => betaler.RegistreerMachtiging("a//b", new DateOnly(2020, 1, 1)));
    }

    [Fact]
    public void Run_is_definitief_en_kan_eenmalig_geannuleerd_worden()
    {
        var run = new Incassorun(new Maand(2027, 3), new DateOnly(2027, 3, 1), new DateTime(2027, 2, 20, 10, 0, 0, DateTimeKind.Utc));
        var post = run.VoegPostToe(Guid.NewGuid(), Iban.Maak("NL91ABNA0417164300"), "P. de Vries", "VER-0001",
            new DateOnly(2019, 7, 1), "Contributie Scouting maart 2027 - Anna");
        post.VoegRegelToe(Lid, new Maand(2027, 3), "Jeugdlid Welpen", new Bedrag(1250));
        post.VoegRegelToe(Lid, new Maand(2027, 2), "Jeugdlid Welpen", new Bedrag(1250));

        Assert.Equal(new Bedrag(2500), run.Totaal);
        Assert.Equal(1, run.AantalPosten);
        Assert.Equal(IncassorunStatus.Definitief, run.Status);
        Assert.Matches("^[A-Za-z0-9-]{1,35}$", post.EndToEndId);

        run.Annuleer(new DateTime(2027, 2, 21, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(IncassorunStatus.Geannuleerd, run.Status);
        Assert.All(post.Regels, r => Assert.True(r.IsGeannuleerd));
        Assert.Throws<InvalidOperationException>(() => run.Annuleer(DateTime.UtcNow));
    }

    [Fact]
    public void Post_zonder_regels_of_met_nulbedrag_wordt_geweigerd()
    {
        var run = new Incassorun(new Maand(2027, 3), new DateOnly(2027, 3, 1), DateTime.UtcNow);
        var post = run.VoegPostToe(Guid.NewGuid(), Iban.Maak("NL91ABNA0417164300"), "x", "VER-1", new DateOnly(2020, 1, 1), "x");

        Assert.Throws<ArgumentOutOfRangeException>(() => post.VoegRegelToe(Lid, new Maand(2027, 3), "Leiding", Bedrag.Nul));
    }
}
