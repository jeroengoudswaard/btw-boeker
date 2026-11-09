using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Domain.Tests.Financien.Boekhouding;

public class JournaalpostTests
{
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private static readonly DateTime Nu = new(2026, 12, 31, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Rubriek Tussen = new(Ver, "Tussenrekeningen", 1);
    private static readonly Grootboekrekening Vooruit = new(Ver, 1400, "Vooruitbetaald", RekeningSoort.Balans, Tussen);
    private static readonly Grootboekrekening Verzekering = new(Ver, 4140, "Verzekering", RekeningSoort.Resultaat, null);
    private static readonly Grootboekrekening Kas = new(Ver, 1000, "Kas", RekeningSoort.Balans, null, RekeningRol.Kas);
    private static readonly Dagboek Memoriaal = new(Ver, DagboekSoort.Memoriaal, "MEM", "Memoriaal", null);

    private static Journaalpost Post(params (Grootboekrekening Rekening, long Debet, long Credit)[] regels)
    {
        var post = new Journaalpost(Ver, Memoriaal, new DateOnly(2026, 12, 31), "Verzekering 2027");
        post.Wijzig(Memoriaal, new DateOnly(2026, 12, 31), "Verzekering 2027",
            regels.Select(r => new RegelInvoer(r.Rekening, null, new Bedrag(r.Debet), new Bedrag(r.Credit))));
        return post;
    }

    [Fact]
    public void Nieuwe_post_is_concept_zonder_nummer()
    {
        var post = Post((Vooruit, 25000, 0), (Verzekering, 0, 25000));

        Assert.Equal(PostStatus.Concept, post.Status);
        Assert.Null(post.Volgnummer);
        Assert.Equal("(concept)", post.Nummer);
        Assert.True(post.IsInEvenwicht);
        Assert.Empty(post.Controleer());
    }

    [Fact]
    public void Niet_in_evenwicht_kan_niet_worden_geboekt()
    {
        var post = Post((Vooruit, 25000, 0), (Verzekering, 0, 20000));

        Assert.Equal(new Bedrag(5000), post.Verschil);
        Assert.Contains(post.Controleer(), f => f.Contains("evenwicht", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => post.Boek(1, Nu));
    }

    [Fact]
    public void Een_regel_of_nulbedrag_is_onvoldoende()
    {
        Assert.Throws<ArgumentException>(() => new RegelInvoer(Vooruit, null, new Bedrag(100), new Bedrag(100)).Valideer());
        Assert.Throws<ArgumentException>(() => new RegelInvoer(Vooruit, null, Bedrag.Nul, Bedrag.Nul).Valideer());
        Assert.Throws<ArgumentException>(() => new RegelInvoer(Vooruit, null, new Bedrag(-1), Bedrag.Nul).Valideer());
        Assert.Contains(Post((Vooruit, 100, 0)).Controleer(), f => f.Contains("twee regels", StringComparison.Ordinal));
    }

    [Fact]
    public void Inactieve_rekening_of_andere_administratie_wordt_gemeld()
    {
        var oud = new Grootboekrekening(Ver, 4999, "Oud", RekeningSoort.Resultaat, null);
        oud.Deactiveer();
        var stichting = new Grootboekrekening(AdministratieSoort.Stichting, 4000, "Stichting", RekeningSoort.Resultaat, null);

        Assert.Contains(Post((oud, 100, 0), (Verzekering, 0, 100)).Controleer(), f => f.Contains("inactief", StringComparison.Ordinal));
        Assert.Contains(Post((stichting, 100, 0), (Verzekering, 0, 100)).Controleer(), f => f.Contains("administratie", StringComparison.Ordinal));
    }

    [Fact]
    public void Kasboekpost_moet_de_kasrekening_bevatten()
    {
        var kasboek = new Dagboek(Ver, DagboekSoort.Kas, "KAS", "Kas", Kas);
        var post = new Journaalpost(Ver, kasboek, new DateOnly(2026, 11, 28), "Omzet borrel");
        post.Wijzig(kasboek, new DateOnly(2026, 11, 28), "Omzet borrel",
            [new RegelInvoer(Vooruit, null, new Bedrag(100), Bedrag.Nul), new RegelInvoer(Verzekering, null, Bedrag.Nul, new Bedrag(100))]);

        Assert.Contains(post.Controleer(), f => f.Contains("1000", StringComparison.Ordinal));
    }

    [Fact]
    public void Geboekte_post_heeft_nummer_en_is_onveranderlijk()
    {
        var post = Post((Vooruit, 25000, 0), (Verzekering, 0, 25000));

        post.Boek(7, Nu);

        Assert.Equal(PostStatus.Geboekt, post.Status);
        Assert.Equal("MEM-2026-0007", post.Nummer);
        Assert.Equal(Nu, post.GeboektUtc);
        Assert.Throws<InvalidOperationException>(() => post.Wijzig(Memoriaal, post.Datum, "x", []));
        Assert.Throws<InvalidOperationException>(() => post.Boek(8, Nu));
    }

    [Fact]
    public void Tegenboeking_is_spiegelbeeld_en_verwijst_naar_origineel()
    {
        var post = Post((Vooruit, 25000, 0), (Verzekering, 0, 25000));
        Assert.Throws<InvalidOperationException>(() => post.MaakTegenboeking(new DateOnly(2027, 1, 5)));
        post.Boek(1, Nu);

        var tegen = post.MaakTegenboeking(new DateOnly(2027, 1, 5));

        Assert.Equal(PostSoort.Tegenboeking, tegen.Soort);
        Assert.Equal(post.Id, tegen.TegenboekingVan);
        Assert.Equal(PostStatus.Concept, tegen.Status);
        Assert.Equal("Tegenboeking MEM-2026-0001: Verzekering 2027", tegen.Omschrijving);
        Assert.Equal(new Bedrag(25000), tegen.Regels.Single(r => r.Rekening == Vooruit).Credit);
        Assert.Equal(new Bedrag(25000), tegen.Regels.Single(r => r.Rekening == Verzekering).Debet);
    }

    [Fact]
    public void Totalen()
    {
        var post = Post((Vooruit, 10000, 0), (Kas, 5000, 0), (Verzekering, 0, 15000));

        Assert.Equal(new Bedrag(15000), post.TotaalDebet);
        Assert.Equal(new Bedrag(15000), post.TotaalCredit);
        Assert.Equal(Bedrag.Nul, post.Verschil);
    }
}
