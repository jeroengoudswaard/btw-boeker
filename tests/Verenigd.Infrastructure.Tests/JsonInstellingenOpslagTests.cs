using Verenigd.Application.Instellingen;
using Verenigd.Application.Modules;
using Verenigd.Domain.Administraties;
using Verenigd.Infrastructure.Instellingen;

namespace Verenigd.Infrastructure.Tests;

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
        Assert.Empty(instellingen.OpenTabbladen);
    }

    [Fact]
    public void Opslaan_en_laden_geeft_dezelfde_instellingen()
    {
        var opslag = new JsonInstellingenOpslag(_map.Bestand("sub/instellingen.json"));
        var origineel = new GebruikersInstellingen
        {
            Thema = Thema.Donker,
            LaatsteDatabase = @"C:\Data\verenigd.vdb",
            OpenTabbladen =
            [
                new OpenTabblad(ModuleSoort.Leden, null),
                new OpenTabblad(ModuleSoort.Boekhouding, AdministratieSoort.Stichting),
            ],
        };

        opslag.Bewaar(origineel);
        var gelezen = opslag.Laad();

        Assert.Equal(origineel.Thema, gelezen.Thema);
        Assert.Equal(origineel.LaatsteDatabase, gelezen.LaatsteDatabase);
        Assert.Equal(origineel.OpenTabbladen, gelezen.OpenTabbladen);
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
