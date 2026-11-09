using System.Text;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Bijlagen;

namespace BtwBoeker.Domain.Tests.Financien.Bijlagen;

/// <summary>Bijlagen bij journaalposten (besluiten 83, 84, 97, 136 en 137).</summary>
public sealed class BijlageTests
{
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private static readonly DateTime Nu = new(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);

    internal static byte[] Pdf(string tekst = "bon") => Encoding.ASCII.GetBytes("%PDF-1.7\n" + tekst);

    internal static byte[] Jpg() => [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    internal static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2];

    [Fact]
    public void Het_soort_wordt_herkend_aan_de_inhoud_en_niet_aan_de_extensie()
    {
        Assert.Equal(BijlageSoort.Pdf, new Bijlage(Ver, "bon.jpg", Pdf(), Nu).Soort);
        Assert.Equal(BijlageSoort.Jpg, new Bijlage(Ver, "foto.pdf", Jpg(), Nu).Soort);
        Assert.Equal(BijlageSoort.Png, new Bijlage(Ver, "scherm.png", Png(), Nu).Soort);
    }

    [Fact]
    public void Andere_bestanden_worden_geweigerd()
    {
        var fout = Assert.Throws<ArgumentException>(() => new Bijlage(Ver, "virus.exe", [0x4D, 0x5A, 0x90, 0x00], Nu));
        Assert.Contains("PDF, JPG of PNG", fout.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => new Bijlage(Ver, "leeg.pdf", [], Nu));
    }

    [Fact]
    public void Een_bijlage_is_maximaal_10_MB()
    {
        var precies = new byte[Bijlage.MaximaleGrootte];
        Pdf().CopyTo(precies, 0);
        Assert.Equal(Bijlage.MaximaleGrootte, new Bijlage(Ver, "groot.pdf", precies, Nu).Grootte);

        var teGroot = new byte[Bijlage.MaximaleGrootte + 1];
        Pdf().CopyTo(teGroot, 0);
        var fout = Assert.Throws<ArgumentException>(() => new Bijlage(Ver, "te-groot.pdf", teGroot, Nu));
        Assert.Contains("10 MB", fout.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void De_bestandsnaam_is_zonder_map_en_de_controlecode_is_de_sha256_van_de_inhoud()
    {
        var a = new Bijlage(Ver, @"C:\Users\x\Downloads\bon hema.pdf", Pdf("a"), Nu);
        var b = new Bijlage(Ver, "andere-naam.pdf", Pdf("a"), Nu);
        var c = new Bijlage(Ver, "bon.pdf", Pdf("c"), Nu);

        Assert.Equal("bon hema.pdf", a.Bestandsnaam);
        Assert.Equal(64, a.Controlecode.Length);
        Assert.Equal(a.Controlecode, b.Controlecode);
        Assert.NotEqual(a.Controlecode, c.Controlecode);
        Assert.Equal(Nu, a.ToegevoegdUtc);
        Assert.NotEqual(Guid.Empty, a.Id);
    }

    [Fact]
    public void Een_nieuwe_bijlage_staat_in_het_postvak_en_kan_worden_gekoppeld_en_ontkoppeld()
    {
        var bijlage = new Bijlage(Ver, "bon.pdf", Pdf(), Nu);
        Assert.True(bijlage.IsInPostvak);

        var post = Guid.NewGuid();
        bijlage.KoppelAan(post);
        Assert.Equal(post, bijlage.PostId);
        Assert.False(bijlage.IsInPostvak);

        bijlage.Ontkoppel();
        Assert.True(bijlage.IsInPostvak);
        Assert.Throws<ArgumentException>(() => bijlage.KoppelAan(Guid.Empty));
    }

    [Fact]
    public void Datum_en_bedrag_kunnen_herkend_of_ingevuld_worden()
    {
        var bijlage = new Bijlage(Ver, "bon.pdf", Pdf(), Nu);
        Assert.Null(bijlage.Datum);
        Assert.Null(bijlage.Bedrag);

        bijlage.ZetGegevens(new DateOnly(2026, 10, 12), new Bedrag(2345));

        Assert.Equal(new DateOnly(2026, 10, 12), bijlage.Datum);
        Assert.Equal(new Bedrag(2345), bijlage.Bedrag);

        bijlage.ZetGegevens(null, new Bedrag(-1500)); // creditnota (besluit 142)
        Assert.Equal(new Bedrag(-1500), bijlage.Bedrag);
    }

    [Fact]
    public void De_inhoud_wordt_apart_bewaard()
    {
        var gegevens = Pdf();
        var bijlage = new Bijlage(Ver, "bon.pdf", gegevens, Nu);
        var inhoud = new BijlageInhoud(bijlage.Id, gegevens);

        Assert.Equal(bijlage.Id, inhoud.BijlageId);
        Assert.Equal(gegevens, inhoud.Gegevens);
    }
}
