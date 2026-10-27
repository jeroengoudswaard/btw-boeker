using System.Text;
using System.Xml.Linq;
using Verenigd.Application.Incasso;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Gedeeld;

namespace Verenigd.Application.Tests.Incasso;

public class Pain008SchrijverTests
{
    private static readonly XNamespace Ns = "urn:iso:std:iso:20022:tech:xsd:pain.008.001.08";

    private static readonly Incassoinstellingen Instellingen = new(
        IncassantId.Maak("DE98ZZZ09999999999"), "Scouting Testgroep & Co", Iban.Maak("NL91ABNA0417164300"), 1, new Maand(2026, 1));

    private static Incassorun Run()
    {
        var run = new Incassorun(
            Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"), new Maand(2027, 3), new DateOnly(2027, 3, 1),
            new DateTime(2027, 2, 20, 9, 30, 0, DateTimeKind.Utc));
        var post1 = run.VoegPostToe(Guid.NewGuid(), Iban.Maak("NL91ABNA0417164300"), "P. de Vries", "VER-0001",
            new DateOnly(2019, 7, 1), "Contributie Scouting maart 2027 - Anna, Zoë");
        post1.VoegRegelToe(Lidnummer.Parse("1"), new Maand(2027, 3), "Jeugdlid Welpen", new Bedrag(1250));
        post1.VoegRegelToe(Lidnummer.Parse("2"), new Maand(2027, 3), "Jeugdlid Junioren", new Bedrag(1500));
        var post2 = run.VoegPostToe(Guid.NewGuid(), Iban.Maak("BE68539007547034"), "Jansen-Ærø", "MANDAAT/2020-07",
            new DateOnly(2020, 7, 15), "Contributie Scouting maart 2027 - Cas");
        post2.VoegRegelToe(Lidnummer.Parse("3"), new Maand(2027, 3), "Jeugdlid Welpen", new Bedrag(1250));
        return run;
    }

    private static string Bronmap([System.Runtime.CompilerServices.CallerFilePath] string pad = "") => Path.GetDirectoryName(pad)!;

    private static XDocument Lees(byte[] xml) => XDocument.Parse(Encoding.UTF8.GetString(xml));

    [Fact]
    public void Bestand_is_geldig_volgens_het_officiele_schema()
    {
        var xml = Pain008Schrijver.Schrijf(Run(), Instellingen);

        Assert.Empty(Pain008Schrijver.Valideer(xml));
    }

    [Fact]
    public void Kopgegevens_aantallen_en_controlesom()
    {
        var document = Lees(Pain008Schrijver.Schrijf(Run(), Instellingen));
        var kop = document.Descendants(Ns + "GrpHdr").Single();
        var batch = document.Descendants(Ns + "PmtInf").Single();

        Assert.Equal("2", kop.Element(Ns + "NbOfTxs")!.Value);
        Assert.Equal("40.00", kop.Element(Ns + "CtrlSum")!.Value);
        Assert.Equal("2027-02-20T09:30:00Z", kop.Element(Ns + "CreDtTm")!.Value);
        Assert.Equal("Scouting Testgroep + Co", kop.Element(Ns + "InitgPty")!.Element(Ns + "Nm")!.Value);
        Assert.Equal("DD", batch.Element(Ns + "PmtMtd")!.Value);
        Assert.Equal("2", batch.Element(Ns + "NbOfTxs")!.Value);
        Assert.Equal("40.00", batch.Element(Ns + "CtrlSum")!.Value);
        Assert.Equal("2027-03-01", batch.Element(Ns + "ReqdColltnDt")!.Value);
    }

    [Fact]
    public void Core_rcur_en_incassant_id()
    {
        var batch = Lees(Pain008Schrijver.Schrijf(Run(), Instellingen)).Descendants(Ns + "PmtInf").Single();

        Assert.Equal("SEPA", batch.Descendants(Ns + "SvcLvl").Single().Element(Ns + "Cd")!.Value);
        Assert.Equal("CORE", batch.Descendants(Ns + "LclInstrm").Single().Element(Ns + "Cd")!.Value);
        Assert.Equal("RCUR", batch.Descendants(Ns + "SeqTp").Single().Value);
        Assert.Equal("SLEV", batch.Element(Ns + "ChrgBr")!.Value);
        Assert.Equal("DE98ZZZ09999999999", batch.Element(Ns + "CdtrSchmeId")!.Descendants(Ns + "Id").Last().Value);
        Assert.Equal("NL91ABNA0417164300", batch.Element(Ns + "CdtrAcct")!.Descendants(Ns + "IBAN").Single().Value);
    }

    [Fact]
    public void Transacties_bevatten_machtiging_rekening_en_omschrijving_in_sepa_tekens()
    {
        var run = Run();
        var transacties = Lees(Pain008Schrijver.Schrijf(run, Instellingen)).Descendants(Ns + "DrctDbtTxInf").ToList();

        Assert.Equal(2, transacties.Count);
        var eerste = transacties[0];
        Assert.Equal(run.Posten[0].EndToEndId, eerste.Descendants(Ns + "EndToEndId").Single().Value);
        Assert.Equal("27.50", eerste.Element(Ns + "InstdAmt")!.Value);
        Assert.Equal("EUR", eerste.Element(Ns + "InstdAmt")!.Attribute("Ccy")!.Value);
        Assert.Equal("VER-0001", eerste.Descendants(Ns + "MndtId").Single().Value);
        Assert.Equal("2019-07-01", eerste.Descendants(Ns + "DtOfSgntr").Single().Value);
        Assert.Equal("Contributie Scouting maart 2027 - Anna, Zoe", eerste.Descendants(Ns + "Ustrd").Single().Value);
        Assert.Equal("AEro", transacties[1].Element(Ns + "Dbtr")!.Element(Ns + "Nm")!.Value.Split('-')[1]);
        Assert.Equal("BE68539007547034", transacties[1].Element(Ns + "DbtrAcct")!.Descendants(Ns + "IBAN").Single().Value);
    }

    [Fact]
    public void Zelfde_run_geeft_exact_hetzelfde_bestand()
    {
        var run = Run();

        Assert.Equal(Pain008Schrijver.Schrijf(run, Instellingen), Pain008Schrijver.Schrijf(run, Instellingen));
        Assert.Equal(64, Pain008Schrijver.Hash(Pain008Schrijver.Schrijf(run, Instellingen)).Length);
    }

    /// <summary>
    /// Vergelijkt met een vast, gecontroleerd voorbeeldbestand. Bewuste wijziging? Draai de test met
    /// <c>VERENIGD_VOORBEELDEN_BIJWERKEN=1</c> en controleer het nieuwe bestand voordat u het commit.
    /// </summary>
    [Fact]
    public void Komt_overeen_met_het_vaste_voorbeeldbestand()
    {
        var xml = Encoding.UTF8.GetString(Pain008Schrijver.Schrijf(Run(), Instellingen));
        if (Environment.GetEnvironmentVariable("VERENIGD_VOORBEELDEN_BIJWERKEN") == "1")
        {
            File.WriteAllText(Path.Combine(Bronmap(), "Voorbeeld-pain008.xml"), xml + "\n");
        }

        var verwacht = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Incasso", "Voorbeeld-pain008.xml"));

        Assert.Equal(verwacht.ReplaceLineEndings("\n").Trim(), xml.ReplaceLineEndings("\n").Trim());
    }

    [Fact]
    public void Ongeldig_bestand_geeft_schemafouten()
    {
        var xml = Encoding.UTF8.GetString(Pain008Schrijver.Schrijf(Run(), Instellingen)).Replace("<SeqTp>RCUR</SeqTp>", "<SeqTp>XXXX</SeqTp>", StringComparison.Ordinal);

        Assert.NotEmpty(Pain008Schrijver.Valideer(Encoding.UTF8.GetBytes(xml)));
    }

    [Fact]
    public void Lege_run_wordt_geweigerd()
    {
        var run = new Incassorun(new Maand(2027, 3), new DateOnly(2027, 3, 1), DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => Pain008Schrijver.Schrijf(run, Instellingen));
    }
}
