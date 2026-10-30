using Verenigd.Application.Import;

namespace Verenigd.Application.Tests.Import;

/// <summary>Besluiten 185 en 186: de uitleg over de vier exports komt uit dezelfde bron als de import en blijft daarmee gelijk.</summary>
public sealed class Sol3ExportuitlegTests
{
    public static TheoryData<Sol3Export, string> Koppen => new()
    {
        { Sol3Export.LedenBasis, Sol3Voorbeeld.KopLeden },
        { Sol3Export.Huishouden, Sol3Voorbeeld.KopHuishouden },
        { Sol3Export.Voogden, Sol3Voorbeeld.KopVoogden },
        { Sol3Export.Functies, Sol3Voorbeeld.KopFuncties },
    };

    [Fact]
    public void Er_zijn_vier_exports_in_de_volgorde_van_besluit_20()
    {
        Assert.Equal([Sol3Export.LedenBasis, Sol3Export.Huishouden, Sol3Export.Voogden, Sol3Export.Functies], Sol3Exportuitleg.Alle.Select(e => e.Export));
        Assert.Equal("Export 1 · Leden-basis", Sol3Exportuitleg.Alle[0].Titel);
        Assert.All(Sol3Exportuitleg.Alle, e => Assert.False(string.IsNullOrWhiteSpace(e.Doel)));
    }

    [Theory]
    [MemberData(nameof(Koppen))]
    public void Verplicht_is_wat_de_import_nodig_heeft_en_samen_met_optioneel_precies_de_sol3_export(Sol3Export export, string kop)
    {
        var uitleg = Sol3Exportuitleg.Van(export);

        Assert.Equal(ExportHerkenning.VerplichteKolommen(export).Order(), uitleg.Kolommen.Where(k => k.IsVerplicht).Select(k => k.Naam).Order());
        Assert.Equal(kop.Split(';'), uitleg.Kolommen.Select(k => k.Naam)); // de volgorde van SOL3
        Assert.Equal(export, ExportHerkenning.Herken([.. uitleg.Kolommen.Select(k => k.Naam)]));
    }

    [Fact]
    public void Kolomnamen_kopieren_geeft_een_naam_per_regel()
    {
        var functies = Sol3Exportuitleg.Van(Sol3Export.Functies);

        var regels = functies.Kopieertekst.Split(Environment.NewLine);

        Assert.Equal(functies.Kolommen.Select(k => k.Naam), regels);
        Assert.Equal("Lidnummer", regels[^1]);
    }

    [Fact]
    public void Het_formaat_noemt_exportkeuze_tekenset_en_datum()
    {
        Assert.Contains("Alleen details", Sol3Exportuitleg.Formaat, StringComparison.Ordinal);
        Assert.Contains("Windows-1252", Sol3Exportuitleg.Formaat, StringComparison.Ordinal);
        Assert.Contains("dag-maand-jaar", Sol3Exportuitleg.Formaat, StringComparison.Ordinal);
    }

    [Fact]
    public void De_klikroute_volgt_de_stappen_van_de_gebruiker_en_elke_export_heeft_een_eigen_rapportnaam()
    {
        Assert.Equal(8, Sol3Exportuitleg.Klikroute.Count);
        Assert.StartsWith("1. Log in op SOL3", Sol3Exportuitleg.Klikroute[0], StringComparison.Ordinal);
        Assert.Contains("Meer", Sol3Exportuitleg.Klikroute[1], StringComparison.Ordinal);
        Assert.Contains("Voorbeeld automatisch bijwerken", Sol3Exportuitleg.Klikroute[3], StringComparison.Ordinal);
        Assert.Contains("Exporteren", Sol3Exportuitleg.Klikroute[^1], StringComparison.Ordinal);
        Assert.Contains("Alleen details", Sol3Exportuitleg.Klikroute[^1], StringComparison.Ordinal);
        Assert.Contains("CSV, door komma's gescheiden", Sol3Exportuitleg.Klikroute[^1], StringComparison.Ordinal);
        Assert.Equal(
            ["invoer voor Verenigd, output #1", "invoer voor Verenigd, output #2", "invoer voor Verenigd, output #3", "invoer voor Verenigd, output #4"],
            Sol3Exportuitleg.Alle.Select(e => e.Rapportnaam));
    }
}
