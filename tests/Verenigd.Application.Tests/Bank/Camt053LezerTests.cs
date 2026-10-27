using Verenigd.Application.Bank;

namespace Verenigd.Application.Tests.Bank;

public class Camt053LezerTests
{
    private static CamtBestand Lees(string xml) => Camt053Lezer.Lees(CamtVoorbeeld.Stroom(xml));

    [Theory]
    [InlineData("08")]
    [InlineData("02")]
    public void Leest_afschriften_saldi_en_transacties(string versie)
    {
        var xml = CamtVoorbeeld.Xml(CamtVoorbeeld.Betaal, versie,
            new VbAfschrift(new DateOnly(2026, 1, 1), 100000),
            new VbAfschrift(new DateOnly(2026, 1, 2), 100000,
                new VbTransactie(-4500, "ICDT", "ESCT", "520", Ref: "R1", Naam: "Energie & Co", Omschrijving: "Factuur 12"),
                new VbTransactie(2550, Ref: "R2", NtryRef: "2")));

        var bestand = Lees(xml);

        Assert.Equal(versie, bestand.Versie);
        Assert.Equal(2, bestand.Afschriften.Count);
        var dag2 = bestand.Afschriften[1];
        Assert.Equal(CamtVoorbeeld.Betaal, dag2.Iban.Waarde);
        Assert.Equal(new DateOnly(2026, 1, 2), dag2.Datum);
        Assert.Equal(100000, dag2.Beginsaldo);
        Assert.Equal(98050, dag2.Eindsaldo);
        var af = dag2.Transacties[0];
        Assert.Equal(4500, af.Bedrag.Centen);
        Assert.False(af.IsBij);
        Assert.Equal("R1", af.Bankreferentie);
        Assert.Equal(CamtVoorbeeld.Tegen, af.TegenIban);
        Assert.Equal("Energie & Co", af.Tegennaam);
        Assert.Equal("Factuur 12", af.Omschrijving);
        Assert.Equal("PMNT-ICDT-ESCT", af.Code.Iso);
        Assert.Equal("RABOBANK 520", af.Code.EigenSleutel);
        Assert.True(dag2.Transacties[1].IsBij);
        Assert.Empty(bestand.Meldingen);
    }

    [Fact]
    public void Afschriftdatum_is_de_datum_van_het_eindsaldo()
    {
        var bestand = Lees(CamtVoorbeeld.Xml(CamtVoorbeeld.Betaal, "08", new VbAfschrift(new DateOnly(2026, 1, 1), 5000)));

        Assert.Equal(new DateOnly(2026, 1, 1), bestand.Afschriften.Single().Datum);
    }

    [Fact]
    public void Batch_en_eigen_code_zonder_iso_en_zonder_referentie()
    {
        var xml = CamtVoorbeeld.Xml(CamtVoorbeeld.Spaar, "08", new VbAfschrift(new DateOnly(2026, 3, 1), 0,
            new VbTransactie(105000, "IDDT", "ESDD", "680", Ref: "B1", Batch: 42, Omschrijving: null),
            new VbTransactie(1234, Familie: null, Eigen: "1025", Ref: null, TegenIban: null, Naam: null, Omschrijving: "Rente", NtryRef: "7")));

        var t = Lees(xml).Afschriften.Single().Transacties;

        Assert.Equal(42, t[0].BatchAantal);
        Assert.Null(t[0].TegenIban);
        Assert.Null(t[1].Code.Iso);
        Assert.Equal("RABOBANK 1025", t[1].Code.EigenSleutel);
        Assert.Null(t[1].Bankreferentie);
        Assert.Equal("7", t[1].Regelreferentie);
    }

    [Fact]
    public void Niet_geboekte_transacties_worden_overgeslagen_met_melding()
    {
        var xml = CamtVoorbeeld.Xml(CamtVoorbeeld.Betaal, "08", new VbAfschrift(new DateOnly(2026, 3, 1), 0,
            new VbTransactie(1000, Ref: "A"), new VbTransactie(500, Ref: "B", Status: "PDNG")));

        var bestand = Lees(xml);

        Assert.Single(bestand.Afschriften.Single().Transacties);
        Assert.Contains(bestand.Meldingen, m => m.Contains("niet geboekt", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("geen xml")]
    [InlineData("<Document xmlns=\"urn:iso:std:iso:20022:tech:xsd:pain.008.001.08\"/>")]
    [InlineData("<?xml version=\"1.0\"?><!DOCTYPE d [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><Document>&x;</Document>")]
    public void Ander_of_onveilig_bestand_wordt_geweigerd(string inhoud)
    {
        var fout = Assert.Throws<InvalidOperationException>(() => Lees(inhoud));
        Assert.Contains("CAMT.053", fout.Message, StringComparison.Ordinal);
    }
}
