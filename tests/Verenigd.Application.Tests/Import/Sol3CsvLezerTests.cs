using System.Text;
using Verenigd.Application.Import;

namespace Verenigd.Application.Tests.Import;

public class Sol3CsvLezerTests
{
    private static CsvTabel Lees(byte[] inhoud)
    {
        using var stroom = new MemoryStream(inhoud);
        return Sol3CsvLezer.Lees(stroom);
    }

    [Fact]
    public void Leest_kolommen_en_regels_met_aanhalingstekens()
    {
        var tabel = Lees(Sol3Voorbeeld.Csv("A;B", ["1", "x"], ["2", "y"]));

        Assert.Equal(["A", "B"], tabel.Kolommen);
        Assert.Equal(2, tabel.Regels.Count);
        Assert.Equal("y", tabel.Regels[1]["B"]);
        Assert.Equal(3, tabel.Regels[1].Regelnummer);
    }

    [Fact]
    public void Windows1252_tekens_worden_goed_gelezen()
    {
        var tabel = Lees(Sol3Voorbeeld.Csv("Rol", ["Coördinator"]));

        Assert.Equal("Coördinator", tabel.Regels[0]["Rol"]);
    }

    [Fact]
    public void Utf8_met_bom_wordt_herkend()
    {
        var inhoud = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("\"Rol\"\r\n\"Coördinator\"\r\n")).ToArray();

        Assert.Equal("Coördinator", Lees(inhoud).Regels[0]["Rol"]);
    }

    [Fact]
    public void Komma_puntkomma_dubbele_aanhalingstekens_en_regeleinde_binnen_veld()
    {
        var tabel = Lees(Sol3Voorbeeld.Csv("A;B", ["Jansen, P.", "zegt \"hoi\"; dag\nnieuwe regel"]));

        Assert.Equal("Jansen, P.", tabel.Regels[0]["A"]);
        Assert.Equal("zegt \"hoi\"; dag\nnieuwe regel", tabel.Regels[0]["B"]);
    }

    [Fact]
    public void Velden_zonder_aanhalingstekens_en_lege_regels_aan_het_eind()
    {
        var inhoud = Sol3Voorbeeld.Windows1252.GetBytes("A;B\n1; 2 \n\n\n");

        var tabel = Lees(inhoud);

        Assert.Single(tabel.Regels);
        Assert.Equal("2", tabel.Regels[0]["B"]);
    }

    [Fact]
    public void Kolommen_worden_op_naam_gezocht_ongeacht_volgorde_en_hoofdletters()
    {
        var tabel = Lees(Sol3Voorbeeld.Csv("Lidnummer;Rol", ["123", "Leider"]));

        Assert.True(tabel.HeeftKolom("rol"));
        Assert.Equal("Leider", tabel.Regels[0]["ROL"]);
    }

    [Fact]
    public void Regel_met_verkeerd_aantal_velden_geeft_fout_met_regelnummer()
    {
        var fout = Assert.Throws<Sol3ImportException>(() => Lees(Sol3Voorbeeld.Csv("A;B", ["1", "2"], ["3"])));

        Assert.Contains("regel 3", fout.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Lege_inhoud_geeft_fout()
    {
        Assert.Throws<Sol3ImportException>(() => Lees([]));
    }

    [Fact]
    public void Dubbele_kolomnaam_geeft_fout()
    {
        Assert.Throws<Sol3ImportException>(() => Lees(Sol3Voorbeeld.Csv("A;A", ["1", "2"])));
    }

    // ---- Scheidingsteken herkennen (besluit 190): SOL3 noemt het "door komma's gescheiden", maar levert puntkomma's ----

    [Fact]
    public void Kommagescheiden_bestand_wordt_herkend_en_een_komma_tussen_aanhalingstekens_blijft_in_het_veld()
    {
        var inhoud = Encoding.Latin1.GetBytes("\"Lidnummer\",\"Achternaam\",\"Rol\"\r\n\"123\",\"Vries, de\",\"Leiding\"\r\n");

        var tabel = Lees(inhoud);

        Assert.Equal(["Lidnummer", "Achternaam", "Rol"], tabel.Kolommen);
        Assert.Equal("Vries, de", tabel.Regels[0]["Achternaam"]);
    }

    [Fact]
    public void Puntkommabestand_met_komma_in_een_veld_blijft_puntkomma()
    {
        var tabel = Lees(Sol3Voorbeeld.Csv("Lidnummer;Achternaam", ["123", "Vries, de"]));

        Assert.Equal(["Lidnummer", "Achternaam"], tabel.Kolommen);
        Assert.Equal("Vries, de", tabel.Regels[0]["Achternaam"]);
    }

    [Fact]
    public void Kolomnaam_met_komma_tussen_aanhalingstekens_telt_niet_mee_voor_het_scheidingsteken()
    {
        var inhoud = Encoding.Latin1.GetBytes("\"Lid, nummer\";\"Naam, volledig\";\"Rol\"\r\n\"1\";\"A\";\"B\"\r\n");

        Assert.Equal(["Lid, nummer", "Naam, volledig", "Rol"], Lees(inhoud).Kolommen);
    }
}
