using System.Text;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Infrastructure.Tests.Demo;

namespace BtwBoeker.Infrastructure.Tests.Boekhouding;

/// <summary>Besluiten 197 en 255–259: de auditfile uit een echte (versleutelde) database, met verzonnen gegevens.</summary>
public sealed class XafDienstTests : IDisposable
{
    private static readonly DateTimeOffset Nu = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private readonly string _map = Path.Combine(Path.GetTempPath(), "btw-boeker-xaf-" + Guid.NewGuid().ToString("N"));

    public XafDienstTests() => Directory.CreateDirectory(_map);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_map, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task Maakt_een_geldige_auditfile_van_de_demodatabase()
    {
        var pad = Path.Combine(_map, "demo.btwdb");
        await Demodatabase.MaakAsync(pad, Nu);
        await using var sessie = await Demodatabase.OpenAsync(pad);

        var resultaat = await new XafDienst(sessie, new VasteTijd(Nu)).MaakAsync(AdministratieSoort.Vereniging, 2026, "0.1.0");

        Assert.Empty(XafSchrijver.Valideer(resultaat.Inhoud));
        Assert.True(resultaat.AantalPosten >= 20);
        Assert.Equal(1, resultaat.OvergeslagenConcepten);
        var xml = Encoding.UTF8.GetString(resultaat.Inhoud);
        Assert.Contains(Demodatabase.NaamVereniging, xml, StringComparison.Ordinal);
        Assert.DoesNotContain("<bankAccNr>", xml, StringComparison.Ordinal);
        Assert.Contains("<dateCreated>2026-11-04</dateCreated>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Een_administratie_zonder_dat_boekjaar_geeft_een_duidelijke_melding()
    {
        var pad = Path.Combine(_map, "demo.btwdb");
        await Demodatabase.MaakAsync(pad, Nu);
        await using var sessie = await Demodatabase.OpenAsync(pad);

        var fout = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new XafDienst(sessie, new VasteTijd(Nu)).MaakAsync(((AdministratieSoort)2), 2026, "0.1.0"));
        Assert.Contains("2026", fout.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void De_bestandsnaam_bevat_administratie_en_jaar()
    {
        Assert.Equal("auditfile-scouting-de-voorbeeldgroep-2026.xaf", XafDienst.Bestandsnaam("Scouting De Voorbeeldgroep", 2026));
    }

    private sealed class VasteTijd(DateTimeOffset nu) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nu;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
