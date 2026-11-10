using System.Buffers.Binary;

namespace BtwBoeker.Architectuur.Tests;

/// <summary>Bewaakt het app-icoon: alle maten aanwezig, 32 bits met alfakanaal, en de bron is een transparante 1024-px-PNG.</summary>
public class IcoonTests
{
    private static readonly int[] Maten = [16, 24, 32, 48, 64, 128, 256];

    private static string Wortel()
    {
        var map = new DirectoryInfo(AppContext.BaseDirectory);
        while (map is not null && !Directory.Exists(Path.Combine(map.FullName, ".git")) && map.GetFiles("*.slnx").Length == 0)
        {
            map = map.Parent;
        }

        return map?.FullName ?? throw new InvalidOperationException("De hoofdmap van de opslagplaats is niet gevonden.");
    }

    [Fact]
    public void Het_app_icoon_bevat_alle_maten_als_32_bits_png()
    {
        var bytes = File.ReadAllBytes(Path.Combine(Wortel(), "src", "BtwBoeker.App", "Assets", "BtwBoeker.ico"));
        var aantal = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4, 2));
        var gevonden = new List<int>();
        for (var i = 0; i < aantal; i++)
        {
            var ingang = bytes.AsSpan(6 + (16 * i), 16);
            var breedte = ingang[0] == 0 ? 256 : ingang[0];
            var hoogte = ingang[1] == 0 ? 256 : ingang[1];
            var bits = BinaryPrimitives.ReadUInt16LittleEndian(ingang.Slice(6, 2));
            var offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(ingang.Slice(12, 4));

            Assert.Equal(breedte, hoogte);
            Assert.Equal(32, bits);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes.AsSpan(offset, 4).ToArray());
            gevonden.Add(breedte);
        }

        Assert.Equal(Maten, gevonden.Order().ToArray());
    }

    [Fact]
    public void De_bron_van_het_icoon_is_een_transparante_png_van_1024_pixels()
    {
        var png = File.ReadAllBytes(Path.Combine(Wortel(), "assets", "bron", "btw-boeker-icoon-1024.png"));

        Assert.Equal(1024u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(1024u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4)));
        Assert.Equal(6, png[25]); // kleurtype 6 = RGBA
    }
}
