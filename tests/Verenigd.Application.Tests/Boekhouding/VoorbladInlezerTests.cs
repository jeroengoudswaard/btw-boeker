using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Tests.Boekhouding;

/// <summary>
/// Synthetisch Voorblad met dezelfde opbouw als de huidige Excel-boekhouding (kolom A nummer, B naam, I/J balans,
/// G/H W&amp;V, totaalregel met SUM, daaronder de jaarrekening). Alle bedragen zijn verzonnen.
/// </summary>
public class VoorbladInlezerTests
{
    private static Werkblad Blad(Action<Werkblad>? extra = null)
    {
        var blad = new Werkblad();
        blad.Zet(1, 1, 2025.0);
        blad.Zet(1, 2, "Omschrijving");
        blad.Zet(1, 9, "Balans");
        blad.Zet(3, 2, "Vermogen");
        blad.Zet(4, 1, 600.0); blad.Zet(4, 2, "Algemene Reserve"); blad.Zet(4, 10, 1000.0, "=D4+G96");
        blad.Zet(5, 1, 800.0); blad.Zet(5, 2, "Rekening Courant St."); blad.Zet(5, 5, 0.0, "=Vermogen!I30");
        blad.Zet(7, 2, "Kas Bank");
        blad.Zet(8, 1, 1000.0); blad.Zet(8, 2, "Kas"); blad.Zet(8, 9, 200.5, "=C8+F8-E8");
        blad.Zet(9, 1, 1100.0); blad.Zet(9, 2, "Bank"); blad.Zet(9, 9, 799.5, "=C9+E9-F9");
        blad.Zet(11, 1, 1298.0); blad.Zet(11, 2, "Kruisposten"); blad.Zet(11, 9, " ");
        blad.Zet(13, 2, "Terreinkosten");
        blad.Zet(14, 1, 4115.0); blad.Zet(14, 2, "Energie"); blad.Zet(14, 7, 300.0, "=E14");
        blad.Zet(15, 1, 5001.0);
        blad.Zet(17, 2, "Contributie");
        blad.Zet(18, 1, 8020.0); blad.Zet(18, 2, "Contributie Welpen"); blad.Zet(18, 8, 500.0, "=F18");
        blad.Zet(20, 1, 9990.0); blad.Zet(20, 2, "Memoriaal");
        blad.Zet(22, 3, 0.0, "=SUM(C3:C21)"); blad.Zet(22, 9, 1000.0, "=SUM(I3:I21)");
        blad.Zet(25, 1, "Vereniging Scouting");
        blad.Zet(28, 1, 6.0); blad.Zet(28, 2, "bevers");
        extra?.Invoke(blad);
        return blad;
    }

    [Fact]
    public void Leest_rekeningen_met_rubriek_soort_en_rol()
    {
        var voorstel = VoorbladInlezer.Lees(Blad());

        Assert.Equal(2025, voorstel.Jaar);
        Assert.Equal([600, 800, 1000, 1100, 1298, 4115, 8020, 9990], voorstel.Rekeningen.Select(r => r.Nummer));
        var energie = voorstel.Rekeningen.Single(r => r.Nummer == 4115);
        Assert.Equal("Terreinkosten", energie.Rubriek);
        Assert.Equal(RekeningSoort.Resultaat, energie.Soort);
        Assert.Equal(RekeningSoort.Balans, voorstel.Rekeningen.Single(r => r.Nummer == 800).Soort); // geen saldo: op nummer
        Assert.Equal(RekeningRol.Reserve, voorstel.Rekeningen.Single(r => r.Nummer == 600).Rol);
        Assert.Equal(RekeningRol.Kas, voorstel.Rekeningen.Single(r => r.Nummer == 1000).Rol);
        Assert.Equal(RekeningRol.Kruisposten, voorstel.Rekeningen.Single(r => r.Nummer == 1298).Rol);
    }

    [Fact]
    public void Jaarrekening_onder_de_totaalregel_wordt_genegeerd()
    {
        Assert.DoesNotContain(VoorbladInlezer.Lees(Blad()).Rekeningen, r => r.Nummer == 6);
    }

    [Fact]
    public void Eindsaldi_worden_beginbalans_en_zijn_in_evenwicht()
    {
        var voorstel = VoorbladInlezer.Lees(Blad());

        Assert.Equal(new Bedrag(20050), voorstel.Rekeningen.Single(r => r.Nummer == 1000).Debet);
        Assert.Equal(new Bedrag(100000), voorstel.Rekeningen.Single(r => r.Nummer == 600).Credit);
        Assert.Equal(Bedrag.Nul, voorstel.Rekeningen.Single(r => r.Nummer == 4115).Debet); // W&V: geen beginbalans
        Assert.True(voorstel.IsInEvenwicht);
        Assert.Equal(new Bedrag(100000), voorstel.TotaalDebet);
    }

    [Fact]
    public void Memoriaal_wordt_overgeslagen_en_rekening_zonder_naam_gemeld()
    {
        var voorstel = VoorbladInlezer.Lees(Blad());

        Assert.True(voorstel.Rekeningen.Single(r => r.Nummer == 9990).Overslaan);
        Assert.Contains(voorstel.Meldingen, m => m.Contains("5001", StringComparison.Ordinal));
    }

    [Fact]
    public void Memoriaal_met_saldo_geeft_waarschuwing()
    {
        var voorstel = VoorbladInlezer.Lees(Blad(b => b.Zet(20, 9, 12.0)));

        Assert.Contains(voorstel.Meldingen, m => m.Contains("Memoriaal", StringComparison.Ordinal));
    }

    [Fact]
    public void Niet_in_evenwicht_wordt_gemeld()
    {
        var voorstel = VoorbladInlezer.Lees(Blad(b => b.Zet(9, 9, 800.0)));

        Assert.False(voorstel.IsInEvenwicht);
        Assert.Contains(voorstel.Meldingen, m => m.Contains("evenwicht", StringComparison.Ordinal));
    }

    [Fact]
    public void Onbekend_blad_geeft_fout()
    {
        var blad = new Werkblad();
        blad.Zet(1, 1, "iets anders");

        Assert.Throws<InvalidOperationException>(() => VoorbladInlezer.Lees(blad));
    }
}
