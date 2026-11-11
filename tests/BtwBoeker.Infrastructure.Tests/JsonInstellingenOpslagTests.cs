using BtwBoeker.Application.Instellingen;
using BtwBoeker.Application.Modules;
using BtwBoeker.Infrastructure.Instellingen;

namespace BtwBoeker.Infrastructure.Tests;

public sealed class JsonInstellingenOpslagTests : IDisposable
{
    private readonly TijdelijkeMap _map = new();

    public void Dispose() => _map.Dispose();

    [Fact]
    public void Ontbrekend_bestand_geeft_standaardinstellingen()
    {
        var opslag = new JsonInstellingenOpslag(_map.Bestand("sub/instellingen.json"));

        var instellingen = opslag.Laad();

        Assert.Equal(Thema.Systeem, instellingen.Thema);
        Assert.Null(instellingen.LaatsteDatabase);
        Assert.Null(instellingen.LaatstePagina);
    }

    [Fact]
    public void Opslaan_en_laden_geeft_dezelfde_instellingen()
    {
        var opslag = new JsonInstellingenOpslag(_map.Bestand("sub/instellingen.json"));
        var origineel = new GebruikersInstellingen
        {
            Thema = Thema.Donker,
            LaatsteDatabase = @"C:\Data\btw-boeker.btwdb",
            LaatstePagina = new LaatstePagina(ModuleSoort.Boekhouding, 1),
            StartwizardGetoond = true,
        };

        opslag.Bewaar(origineel);
        var gelezen = opslag.Laad();

        Assert.Equal(origineel.Thema, gelezen.Thema);
        Assert.Equal(origineel.LaatsteDatabase, gelezen.LaatsteDatabase);
        Assert.Equal(origineel.LaatstePagina, gelezen.LaatstePagina);
        Assert.True(gelezen.StartwizardGetoond);
    }

    /// <summary>
    /// Een instellingenbestand van v0.19.0 bewaarde bij de laatste pagina ook de administratie, met de oude interne naam.
    /// Dat veld bestaat niet meer; de database, het thema en de pagina blijven bewaard.
    /// </summary>
    [Fact]
    public void Instellingen_van_versie_0_19_blijven_leesbaar()
    {
        var pad = _map.Bestand("instellingen.json");
        var oudeNaam = "Veren" + "iging"; // samengesteld: zie ProductnaamTests
        File.WriteAllText(pad, "{ \"Thema\": \"Donker\", \"LaatsteDatabase\": \"C:\\\\Data\\\\vakantiehuis.btwdb\", "
            + "\"LaatstePagina\": { \"Module\": \"Boekhouding\", \"Administratie\": \"" + oudeNaam + "\", \"Onderdeel\": 3 }, "
            + "\"StartwizardGetoond\": true }");

        var instellingen = new JsonInstellingenOpslag(pad).Laad();

        Assert.Equal(Thema.Donker, instellingen.Thema);
        Assert.Equal(@"C:\Data\vakantiehuis.btwdb", instellingen.LaatsteDatabase);
        Assert.Equal(new LaatstePagina(ModuleSoort.Boekhouding, 3), instellingen.LaatstePagina);
        Assert.True(instellingen.StartwizardGetoond);
    }

    [Fact]
    public void Beschadigd_bestand_geeft_standaardinstellingen()
    {
        var pad = _map.Bestand("instellingen.json");
        File.WriteAllText(pad, "{ kapot");

        var instellingen = new JsonInstellingenOpslag(pad).Laad();

        Assert.Equal(Thema.Systeem, instellingen.Thema);
    }
}
