using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Database;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Boekhouding;
using BtwBoeker.Infrastructure.Database;

namespace BtwBoeker.Infrastructure.Tests.Boekhouding;

/// <summary>Jaarrekeningindeling in de echte, versleutelde database.</summary>
public sealed class JaarrekeningOpslagTests : IAsyncLifetime, IDisposable
{
    private const string Wachtwoord = "zonnig vakantiehuis 2026";
    private const AdministratieSoort Adm = AdministratieSoort.Standaard;
    private readonly TijdelijkeMap _map = new();
    private readonly DatabaseBeheer _beheer = new();
    private IDatabaseSessie _sessie = null!;

    public async Task InitializeAsync()
    {
        var pad = _map.Bestand("test.btwdb");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        _sessie = await _beheer.OpenAsync(pad, Wachtwoord);
        var dienst = new BoekhoudingDienst(_sessie, TimeProvider.System);
        await dienst.RichtInAsync(Adm);
        await dienst.BewaarRubriekAsync(Adm, new RubriekInvoer(null, "Verhuur", 1));
        await dienst.BewaarRubriekAsync(Adm, new RubriekInvoer(null, "Activiteiten", 2));
    }

    public async Task DisposeAsync() => await _sessie.DisposeAsync();

    public void Dispose() => _map.Dispose();

    [Fact]
    public async Task Indeling_wordt_opgeslagen_vervangen_en_teruggezet()
    {
        var dienst = new JaarrekeningDienst(_sessie);
        var stand = await _sessie.Boekhouding.LaadAsync(Adm);
        var verhuur = stand.Rubrieken.Single(r => r.Naam == "Verhuur").Id;
        var activiteiten = stand.Rubrieken.Single(r => r.Naam == "Activiteiten").Id;

        await dienst.BewaarAsync(Adm, JaarrekeningRapport.BatenEnLasten,
        [
            new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Kop, "Opbrengsten", []),
            new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Regel, "Verhuur en schoonmaak", [verhuur, activiteiten]),
            new(JaarrekeningAfdeling.Lasten, IndelingsregelSoort.Regel, "Schoonmaak", [activiteiten]),
            new(JaarrekeningAfdeling.Lasten, IndelingsregelSoort.Subtotaal, "Subtotaal", []),
        ]);

        stand = await _sessie.Boekhouding.LaadAsync(Adm);
        var indeling = Assert.Single(stand.Jaarrekeningindelingen);
        Assert.Equal(["Opbrengsten", "Verhuur en schoonmaak", "Schoonmaak", "Subtotaal"], indeling.Regels.Select(r => r.Tekst));
        Assert.Equal(["Activiteiten", "Verhuur"], indeling.Regels[1].Rubrieken.Select(r => r.Naam).Order(StringComparer.Ordinal));

        await dienst.BewaarAsync(Adm, JaarrekeningRapport.BatenEnLasten, [new(JaarrekeningAfdeling.Baten, IndelingsregelSoort.Regel, "Alleen verhuur", [verhuur])]);
        stand = await _sessie.Boekhouding.LaadAsync(Adm);
        Assert.Equal("Alleen verhuur", Assert.Single(Assert.Single(stand.Jaarrekeningindelingen).Regels).Tekst);

        await dienst.BewaarAsync(Adm, JaarrekeningRapport.BatenEnLasten, []);
        stand = await _sessie.Boekhouding.LaadAsync(Adm);
        Assert.Empty(Assert.Single(stand.Jaarrekeningindelingen).Regels);
        Assert.True(Jaarrekeningrekenaar.Bereken(stand, JaarrekeningRapport.BatenEnLasten, 2026).IsAutomatisch);
        Assert.Empty((await _sessie.Boekhouding.LaadAsync(((AdministratieSoort)2))).Jaarrekeningindelingen);
    }
}
