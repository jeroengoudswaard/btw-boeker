using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Begrotingen;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Domain.Tests.Financien.Begrotingen;

/// <summary>Begroting per grootboekrekening, met versies en vaststellen (besluiten 146–152).</summary>
public sealed class BegrotingTests
{
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private static readonly DateTime Nu = new(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Grootboekrekening Energie = new(Ver, 4115, "Energie", RekeningSoort.Resultaat, null);
    private static readonly Grootboekrekening Welpen = new(Ver, 8020, "Contributie Welpen", RekeningSoort.Resultaat, null);
    private static readonly Grootboekrekening Bank = new(Ver, 1100, "Bank", RekeningSoort.Balans, null);

    // ---- Baten of lasten (besluit 152) ------------------------------------------------------

    [Fact]
    public void Een_resultaatrekening_is_standaard_baten_vanaf_8000_anders_lasten()
    {
        Assert.Equal(ResultaatKant.Lasten, Energie.Kant);
        Assert.Equal(ResultaatKant.Baten, Welpen.Kant);
        Assert.Equal(ResultaatKant.Baten, new Grootboekrekening(Ver, 9100, "Rente", RekeningSoort.Resultaat, null).Kant);
        Assert.Equal(ResultaatKant.Geen, Bank.Kant);
    }

    [Fact]
    public void De_kant_is_aan_te_passen_maar_niet_bij_een_balansrekening()
    {
        var rekening = new Grootboekrekening(Ver, 4900, "Overige baten", RekeningSoort.Resultaat, null);
        rekening.WijzigKant(ResultaatKant.Baten);
        Assert.Equal(ResultaatKant.Baten, rekening.Kant);

        Assert.Throws<ArgumentException>(() => rekening.WijzigKant(ResultaatKant.Geen));
        var balans = new Grootboekrekening(Ver, 1000, "Kas", RekeningSoort.Balans, null);
        Assert.Throws<ArgumentException>(() => balans.WijzigKant(ResultaatKant.Baten));
    }

    [Fact]
    public void Van_soort_wisselen_past_de_kant_aan()
    {
        var rekening = new Grootboekrekening(Ver, 8500, "Acties", RekeningSoort.Resultaat, null);
        rekening.Wijzig("Acties", null, RekeningSoort.Balans);
        Assert.Equal(ResultaatKant.Geen, rekening.Kant);
        rekening.Wijzig("Acties", null, RekeningSoort.Resultaat);
        Assert.Equal(ResultaatKant.Baten, rekening.Kant);
    }

    // ---- Begroting ------------------------------------------------------------------------

    [Fact]
    public void Een_nieuwe_begroting_is_een_concept_met_versie_1()
    {
        var begroting = new Begroting(Ver, 2027, Nu);

        Assert.Equal(2027, begroting.Jaar);
        Assert.Equal(1, begroting.Versie);
        Assert.Equal(BegrotingStatus.Concept, begroting.Status);
        Assert.Equal("Oorspronkelijk", begroting.Omschrijving);
        Assert.Empty(begroting.Regels);
    }

    [Fact]
    public void Bedragen_per_rekening_een_nul_verwijdert_de_regel()
    {
        var begroting = new Begroting(Ver, 2027, Nu);

        begroting.ZetBedrag(Energie, new Bedrag(130000), "Hogere tarieven");
        begroting.ZetBedrag(Welpen, new Bedrag(440000), null);
        begroting.ZetBedrag(Energie, new Bedrag(120000), null);

        Assert.Equal(2, begroting.Regels.Count);
        Assert.Equal(new Bedrag(120000), begroting.Bedrag(Energie));
        Assert.Null(begroting.Regels.Single(r => r.Rekening == Energie).Toelichting);

        begroting.ZetBedrag(Energie, Bedrag.Nul, null);
        Assert.Equal(Bedrag.Nul, begroting.Bedrag(Energie));
        Assert.Single(begroting.Regels);
    }

    [Fact]
    public void Alleen_resultaatrekeningen_en_geen_negatieve_bedragen()
    {
        var begroting = new Begroting(Ver, 2027, Nu);

        Assert.Throws<ArgumentException>(() => begroting.ZetBedrag(Bank, new Bedrag(100), null));
        Assert.Throws<ArgumentException>(() => begroting.ZetBedrag(Energie, new Bedrag(-100), null));
    }

    [Fact]
    public void Een_vastgestelde_begroting_is_alleen_lezen()
    {
        var begroting = new Begroting(Ver, 2027, Nu);
        begroting.ZetBedrag(Energie, new Bedrag(120000), null);

        begroting.StelVast(new DateOnly(2026, 12, 12), "Vastgesteld ALV 12-12-2026");

        Assert.Equal(BegrotingStatus.Vastgesteld, begroting.Status);
        Assert.Equal(new DateOnly(2026, 12, 12), begroting.VastgesteldOp);
        Assert.Equal("Vastgesteld ALV 12-12-2026", begroting.Omschrijving);
        Assert.Throws<InvalidOperationException>(() => begroting.ZetBedrag(Energie, new Bedrag(1), null));
        Assert.Throws<InvalidOperationException>(() => begroting.StelVast(new DateOnly(2026, 12, 13), null));
    }

    [Fact]
    public void Een_herziening_is_een_nieuw_concept_met_een_kopie_van_de_bedragen()
    {
        var begroting = new Begroting(Ver, 2027, Nu);
        begroting.ZetBedrag(Energie, new Bedrag(120000), "Tarief");
        Assert.Throws<InvalidOperationException>(() => begroting.Herzie(Nu)); // alleen een vastgestelde versie
        begroting.StelVast(new DateOnly(2026, 12, 12), null);

        var herziening = begroting.Herzie(Nu);

        Assert.Equal(2, herziening.Versie);
        Assert.Equal(BegrotingStatus.Concept, herziening.Status);
        Assert.Equal("Herziening 1", herziening.Omschrijving);
        Assert.Equal(new Bedrag(120000), herziening.Bedrag(Energie));
        Assert.Equal("Tarief", herziening.Regels.Single().Toelichting);
        herziening.ZetBedrag(Energie, new Bedrag(150000), null);
        Assert.Equal(new Bedrag(120000), begroting.Bedrag(Energie)); // het origineel blijft
    }
}
