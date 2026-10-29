using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Begrotingen;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Domain.Tests.Financien.Begrotingen;

/// <summary>Meerjarenplan van de Stichting: per rekening een bedrag per jaar, 10 jaar (besluiten 159–167).</summary>
public sealed class MeerjarenplanTests
{
    private const AdministratieSoort Sti = AdministratieSoort.Stichting;
    private static readonly DateTime Nu = new(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Grootboekrekening Huur = new(Sti, 8100, "Huur van de Vereniging", RekeningSoort.Resultaat, null);
    private static readonly Grootboekrekening Onderhoud = new(Sti, 4200, "Onderhoud gebouw", RekeningSoort.Resultaat, null);
    private static readonly Grootboekrekening Gebouw = new(Sti, 200, "Gebouw", RekeningSoort.Balans, null);

    [Fact]
    public void Een_nieuw_plan_beslaat_10_jaar_vanaf_het_startjaar_als_concept()
    {
        var plan = new Meerjarenplan(Sti, 2027, Nu);

        Assert.Equal(2027, plan.Startjaar);
        Assert.Equal(10, plan.AantalJaren);
        Assert.Equal(2036, plan.Eindjaar);
        Assert.Equal(Enumerable.Range(2027, 10), plan.Jaren);
        Assert.Equal(1, plan.Versie);
        Assert.Equal(BegrotingStatus.Concept, plan.Status);
        Assert.Equal("Oorspronkelijk", plan.Omschrijving);
    }

    [Fact]
    public void Bedragen_per_rekening_per_jaar_een_nul_verwijdert_het_bedrag()
    {
        var plan = new Meerjarenplan(Sti, 2027, Nu);

        plan.ZetBedrag(Huur, 2027, new Bedrag(600000));
        plan.ZetBedrag(Huur, 2028, new Bedrag(600000));
        plan.ZetBedrag(Onderhoud, 2030, new Bedrag(300000));
        plan.ZetBedrag(Huur, 2028, Bedrag.Nul);

        Assert.Equal(new Bedrag(600000), plan.Bedrag(Huur, 2027));
        Assert.Equal(Bedrag.Nul, plan.Bedrag(Huur, 2028));
        Assert.Equal(new Bedrag(300000), plan.Bedrag(Onderhoud, 2030));
        Assert.Equal(2, plan.Posten.Count);
        Assert.Single(plan.Posten.Single(p => p.Rekening == Huur).Bedragen);
    }

    [Fact]
    public void Jaar_buiten_het_plan_balansrekening_of_negatief_bedrag_kan_niet()
    {
        var plan = new Meerjarenplan(Sti, 2027, Nu);

        Assert.Throws<ArgumentOutOfRangeException>(() => plan.ZetBedrag(Huur, 2026, new Bedrag(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.ZetBedrag(Huur, 2037, new Bedrag(1)));
        Assert.Throws<ArgumentException>(() => plan.ZetBedrag(Gebouw, 2027, new Bedrag(1)));
        Assert.Throws<ArgumentException>(() => plan.ZetBedrag(Huur, 2027, new Bedrag(-1)));
    }

    [Fact]
    public void Kopieren_naar_de_volgende_jaren_vult_alle_latere_jaren()
    {
        var plan = new Meerjarenplan(Sti, 2027, Nu);
        plan.ZetBedrag(Huur, 2029, new Bedrag(650000));
        plan.ZetBedrag(Huur, 2031, new Bedrag(1));

        plan.KopieerNaarVolgendeJaren(Huur, 2029);

        Assert.Equal(Bedrag.Nul, plan.Bedrag(Huur, 2028)); // eerdere jaren blijven
        Assert.All(Enumerable.Range(2029, 8), j => Assert.Equal(new Bedrag(650000), plan.Bedrag(Huur, j)));
    }

    [Fact]
    public void Toelichting_per_rekening_en_een_lege_post_verdwijnt()
    {
        var plan = new Meerjarenplan(Sti, 2027, Nu);
        plan.ZetToelichting(Onderhoud, "  Schilderwerk elke 7 jaar ");
        Assert.Equal("Schilderwerk elke 7 jaar", plan.Toelichting(Onderhoud));

        plan.ZetToelichting(Onderhoud, "");
        Assert.Empty(plan.Posten);
    }

    [Fact]
    public void Vastgesteld_is_alleen_lezen_en_een_herziening_kopieert_alles()
    {
        var plan = new Meerjarenplan(Sti, 2027, Nu);
        plan.ZetBedrag(Huur, 2027, new Bedrag(600000));
        plan.ZetToelichting(Huur, "Huurcontract");
        Assert.Throws<InvalidOperationException>(() => plan.Herzie(Nu));

        plan.StelVast(new DateOnly(2026, 11, 20), "Bestuur 20-11-2026");

        Assert.Throws<InvalidOperationException>(() => plan.ZetBedrag(Huur, 2028, new Bedrag(1)));
        Assert.Throws<InvalidOperationException>(() => plan.KopieerNaarVolgendeJaren(Huur, 2027));
        var herziening = plan.Herzie(Nu);
        Assert.Equal(2, herziening.Versie);
        Assert.Equal(2027, herziening.Startjaar);
        Assert.Equal("Herziening 1", herziening.Omschrijving);
        Assert.Equal(new Bedrag(600000), herziening.Bedrag(Huur, 2027));
        Assert.Equal("Huurcontract", herziening.Toelichting(Huur));
        herziening.ZetBedrag(Huur, 2027, new Bedrag(1));
        Assert.Equal(new Bedrag(600000), plan.Bedrag(Huur, 2027));
    }
}
