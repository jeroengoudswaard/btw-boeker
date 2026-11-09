using System.Diagnostics;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Infrastructure.Database;

namespace BtwBoeker.Infrastructure.Tests.Prestaties;

/// <summary>
/// Besluit 236: verbindingen worden hergebruikt, zodat SQLite3MC de sleutel niet bij elke query opnieuw afleidt; na sluiten
/// is het bestand weer vrij. De tijdsbudgetten zijn ruim (ook voor trage CI-machines) maar vangen een terugval naar
/// "één nieuwe verbinding per query" (gemeten: 1,0 s per keer boekhouding laden, met pooling 0,1 s).
/// </summary>
public sealed class VerbindingTests : IDisposable
{
    private const string Wachtwoord = "scouting is fantastisch";
    private readonly TijdelijkeMap _map = new();
    private readonly DatabaseBeheer _beheer = new();

    public void Dispose() => _map.Dispose();

    [Fact]
    public async Task Na_aanmaken_en_na_sluiten_is_het_bestand_vrij()
    {
        var pad = _map.Bestand("vrij.btwdb");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        AssertVrij(pad);

        await using (var sessie = await _beheer.OpenAsync(pad, Wachtwoord))
        {
            await sessie.Administraties.HaalAlleAsync();
            await sessie.Boekhouding.LaadAsync(AdministratieSoort.Vereniging);
        }

        AssertVrij(pad);
        File.Move(pad, pad + ".verplaatst");
        Assert.True(File.Exists(pad + ".verplaatst"));
    }

    [Fact]
    public async Task Een_back_up_maken_laat_geen_bestanden_open()
    {
        var pad = _map.Bestand("bron.btwdb");
        var backup = _map.Bestand("bron.btwbackup");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        await using var sessie = await _beheer.OpenAsync(pad, Wachtwoord);

        await sessie.MaakBackupAsync(backup);

        // Op Windows faalt het opruimen van de werkmap als de kopie nog open is; dan was MaakBackupAsync al mislukt.
        AssertVrij(backup);
    }

    [Fact]
    [Trait("Categorie", "Prestaties")]
    public async Task Boekhouding_laden_opent_niet_steeds_een_nieuwe_versleutelde_verbinding()
    {
        var pad = _map.Bestand("snel.btwdb");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        await using var sessie = await _beheer.OpenAsync(pad, Wachtwoord);
        await sessie.Boekhouding.LaadAsync(AdministratieSoort.Vereniging); // opwarmen

        var klok = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++)
        {
            await sessie.Boekhouding.LaadAsync(AdministratieSoort.Vereniging);
        }

        Assert.True(klok.ElapsedMilliseconds < 4000, $"10x boekhouding laden duurde {klok.ElapsedMilliseconds} ms (budget 4000 ms; zonder pooling ±10.000 ms).");
    }

    [Fact]
    [Trait("Categorie", "Prestaties")]
    public async Task Een_nieuwe_database_aanmaken_blijft_onder_het_budget()
    {
        var klok = Stopwatch.StartNew();
        await _beheer.MaakAanAsync(_map.Bestand("nieuw.btwdb"), Wachtwoord);

        // Inclusief de bewust trage afleiding van de sleutel uit het wachtwoord (±0,35 s); zonder pooling ±3 s extra.
        Assert.True(klok.ElapsedMilliseconds < 2500, $"Aanmaken duurde {klok.ElapsedMilliseconds} ms (budget 2500 ms).");
    }

    private static void AssertVrij(string pad)
    {
        using var stroom = new FileStream(pad, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(stroom.Length > 0);
    }
}
