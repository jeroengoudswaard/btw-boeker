namespace BtwBoeker.App.Tests.UI;

public sealed class SnelheidsrapportTests
{
    private const string Log = """
        2026-11-11 19:00:00.100 +01:00 [INF] Database geopend: meerjarig.btwdb
        2026-11-11 19:00:00.200 +01:00 [INF] Duur Openen database (sleutel en migraties): 812 ms
        2026-11-11 19:00:01.000 +01:00 [INF] Duur Navigeren naar Boekhouding: 120 ms
        2026-11-11 19:00:02.000 +01:00 [INF] Duur Boekhouding laden Standaard: 300 ms
        2026-11-11 19:00:03.000 +01:00 [INF] Duur Navigeren naar Boekhouding: 20 ms
        2026-11-11 19:00:04.000 +01:00 [INF] Duur Boekhouding laden Standaard: 40 ms
        2026-11-11 19:00:05.000 +01:00 [INF] Duur Navigeren naar Boekhouding: 30 ms
        """;

    [Fact]
    public void Leest_de_tijden_per_handeling_in_volgorde()
    {
        var metingen = Snelheidsrapport.Lees(Log.Split('\n'));

        Assert.Equal([812], metingen["Openen database (sleutel en migraties)"]);
        Assert.Equal([120, 20, 30], metingen["Navigeren naar Boekhouding"]);
        Assert.Equal([300, 40], metingen["Boekhouding laden Standaard"]);
        Assert.Equal(3, metingen.Count);
    }

    [Fact]
    public void Tabel_toont_eerste_keer_gemiddelde_daarna_en_het_hoogste()
    {
        var tabel = Snelheidsrapport.Tabel(Snelheidsrapport.Lees(Log.Split('\n')));

        Assert.Contains("| Navigeren naar Boekhouding | 120 | 25 | 120 | 3 |", tabel, StringComparison.Ordinal);
        Assert.Contains("| Openen database (sleutel en migraties) | 812 | – | 812 | 1 |", tabel, StringComparison.Ordinal);
        // Gesorteerd op de eerste keer, de traagste bovenaan.
        Assert.True(tabel.IndexOf("Openen database", StringComparison.Ordinal) < tabel.IndexOf("Boekhouding laden", StringComparison.Ordinal));
    }
}
