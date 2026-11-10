using BtwBoeker.Application.Instellingen;
using BtwBoeker.Application.Modules;
using BtwBoeker.Domain.Administraties;
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
            LaatstePagina = new LaatstePagina(ModuleSoort.Boekhouding, AdministratieSoort.Vereniging, 1),
            StartwizardGetoond = true,
        };

        opslag.Bewaar(origineel);
        var gelezen = opslag.Laad();

        Assert.Equal(origineel.Thema, gelezen.Thema);
        Assert.Equal(origineel.LaatsteDatabase, gelezen.LaatsteDatabase);
        Assert.Equal(origineel.LaatstePagina, gelezen.LaatstePagina);
        Assert.True(gelezen.StartwizardGetoond);
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
