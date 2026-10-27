using Verenigd.Application.Import;

namespace Verenigd.Application.Tests.Import;

public class ExportHerkenningTests
{
    [Theory]
    [InlineData(Sol3Voorbeeld.KopLeden, Sol3Export.LedenBasis)]
    [InlineData(Sol3Voorbeeld.KopHuishouden, Sol3Export.Huishouden)]
    [InlineData(Sol3Voorbeeld.KopVoogden, Sol3Export.Voogden)]
    [InlineData(Sol3Voorbeeld.KopFuncties, Sol3Export.Functies)]
    public void Echte_kopregels_worden_herkend(string kopregel, Sol3Export verwacht)
    {
        Assert.Equal(verwacht, ExportHerkenning.Herken(kopregel.Split(';')));
    }

    [Fact]
    public void Kolommen_in_andere_volgorde_worden_herkend()
    {
        Assert.Equal(Sol3Export.Functies, ExportHerkenning.Herken(Sol3Voorbeeld.KopFuncties.Split(';').Reverse().ToList()));
    }

    [Fact]
    public void Onbekende_kopregel_wordt_niet_herkend()
    {
        Assert.Null(ExportHerkenning.Herken(["Naam", "Adres"]));
    }

    [Fact]
    public void Ontbrekende_verplichte_kolom_wordt_gemeld()
    {
        var kolommen = Sol3Voorbeeld.KopLeden.Split(';').Where(k => k != "Geboortedatum").ToList();

        Assert.Equal(["Geboortedatum"], ExportHerkenning.OntbrekendeKolommen(Sol3Export.LedenBasis, kolommen));
    }
}
