using Verenigd.App.Diensten;
using Verenigd.Application.Administraties;
using Verenigd.Application.Beveiliging;
using Verenigd.Application.Database;
using Verenigd.Application.Import;
using Verenigd.Application.Instellingen;
using Verenigd.Application.Leden;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Leden;

namespace Verenigd.App.Tests;

internal sealed class FakeDialoogService : IDialoogService
{
    public string? NieuwDatabasebestand { get; set; }
    public string? BestaandDatabasebestand { get; set; }
    public string? BackupDoel { get; set; }
    public string? BackupBron { get; set; }
    public string? NieuwWachtwoord { get; set; }
    public HerstelInvoer? Herstel { get; set; }
    public bool BevestigAntwoord { get; set; } = true;
    public OpslaanKeuze OpslaanAntwoord { get; set; } = OpslaanKeuze.Annuleren;
    public List<string> OpslaanVragen { get; } = [];
    public Queue<WachtwoordInvoer?> WachtwoordAntwoorden { get; } = new();
    public List<string?> MeldingenBijWachtwoordvraag { get; } = [];
    public List<string> Fouten { get; } = [];
    public List<string> Bevestigingsvragen { get; } = [];
    public Herstelsleutel? GetoondeHerstelsleutel { get; private set; }
    public IReadOnlyList<string> Importbestanden { get; set; } = [];
    public string? CsvDoel { get; set; }

    /// <summary>Wat de "gebruiker" in de importwizard doet; standaard annuleren.</summary>
    public Func<ViewModels.Leden.ImportWizardViewModel, Task<bool>>? ImportWizard { get; set; }

    public IReadOnlyList<string> KiesImportbestanden() => Importbestanden;

    public string? KiesCsvDoel(string voorgesteldeNaam) => CsvDoel;

    public bool ToonImportWizard(ViewModels.Leden.ImportWizardViewModel wizard) =>
        ImportWizard is not null && ImportWizard(wizard).GetAwaiter().GetResult();

    public string? KiesNieuwDatabasebestand(string titel) => NieuwDatabasebestand;

    public OpslaanKeuze VraagOmOpslaan(string documentnaam)
    {
        OpslaanVragen.Add(documentnaam);
        return OpslaanAntwoord;
    }
    public string? KiesBestaandDatabasebestand() => BestaandDatabasebestand;
    public string? KiesBackupDoel(string voorgesteldeNaam) => BackupDoel;
    public string? KiesBackupBron() => BackupBron;
    public string? VraagNieuwWachtwoord() => NieuwWachtwoord;
    public HerstelInvoer? VraagHerstel(string databaseNaam) => Herstel;

    public WachtwoordInvoer? VraagWachtwoord(string databaseNaam, string? foutmelding)
    {
        MeldingenBijWachtwoordvraag.Add(foutmelding);
        return WachtwoordAntwoorden.Count > 0 ? WachtwoordAntwoorden.Dequeue() : null;
    }

    public void ToonHerstelsleutel(Herstelsleutel herstelsleutel) => GetoondeHerstelsleutel = herstelsleutel;

    public bool Bevestig(string titel, string vraag)
    {
        Bevestigingsvragen.Add(vraag);
        return BevestigAntwoord;
    }

    public void ToonFout(string titel, string melding) => Fouten.Add(melding);

    public void ToonInformatie(string titel, string melding)
    {
    }
}

internal sealed class FakeDatabaseBeheer : IDatabaseBeheer
{
    private readonly Dictionary<string, string> _wachtwoorden = new(StringComparer.OrdinalIgnoreCase);

    public Herstelsleutel Herstelsleutel { get; } = Herstelsleutel.Genereer();
    public InMemoryAdministratieRepository Repository { get; } = new();
    public InMemoryLedenOpslag Leden { get; } = new();
    public FakeSessie? LaatsteSessie { get; private set; }
    public List<(string Backup, string Doel)> Teruggezet { get; } = [];

    public void Voeg(string pad, string wachtwoord) => _wachtwoorden[pad] = wachtwoord;

    public bool Bestaat(string pad) => _wachtwoorden.ContainsKey(pad);

    public Task<Herstelsleutel> MaakAanAsync(string pad, string wachtwoord, CancellationToken annulering = default)
    {
        if (Bestaat(pad))
        {
            throw new DatabaseBestandException("bestaat al");
        }

        Voeg(pad, wachtwoord);
        return Task.FromResult(Herstelsleutel);
    }

    public Task<IDatabaseSessie> OpenAsync(string pad, string wachtwoord, CancellationToken annulering = default)
    {
        if (!_wachtwoorden.TryGetValue(pad, out var juist))
        {
            throw new DatabaseBestandException("De database bestaat niet.");
        }

        if (juist != wachtwoord)
        {
            throw new OngeldigeSleutelException();
        }

        LaatsteSessie = new FakeSessie(pad, Repository, Leden);
        return Task.FromResult<IDatabaseSessie>(LaatsteSessie);
    }

    public Task<IDatabaseSessie> OpenMetHerstelsleutelAsync(
        string pad, Herstelsleutel herstelsleutel, string nieuwWachtwoord, CancellationToken annulering = default)
    {
        if (herstelsleutel.ToString() != Herstelsleutel.ToString())
        {
            throw new OngeldigeSleutelException();
        }

        Voeg(pad, nieuwWachtwoord);
        return OpenAsync(pad, nieuwWachtwoord, annulering);
    }

    public Task HerstelBackupAsync(string backupPad, string doelPad, CancellationToken annulering = default)
    {
        Teruggezet.Add((backupPad, doelPad));
        return Task.CompletedTask;
    }
}

internal sealed class FakeSessie(string pad, IAdministratieRepository administraties, InMemoryLedenOpslag leden) : IDatabaseSessie
{
    public string Pad { get; } = pad;
    public IAdministratieRepository Administraties { get; } = administraties;
    public ILedenRepository Leden { get; } = leden;
    public ILedenImportOpslag LedenImport { get; } = leden;
    public List<string> Backups { get; } = [];
    public bool Gesloten { get; private set; }

    public Task MaakBackupAsync(string doelPad, CancellationToken annulering = default)
    {
        Backups.Add(doelPad);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Gesloten = true;
        return ValueTask.CompletedTask;
    }
}

internal sealed class InMemoryAdministratieRepository : IAdministratieRepository
{
    public List<Administratie> Opgeslagen { get; } =
    [
        new(AdministratieSoort.Vereniging, "Scouting Vereniging", Administratie.Standaardkleur(AdministratieSoort.Vereniging)),
        new(AdministratieSoort.Stichting, "Scouting Stichting", Administratie.Standaardkleur(AdministratieSoort.Stichting)),
    ];

    public int AantalKeerBewaard { get; private set; }

    public Task<IReadOnlyList<Administratie>> HaalAlleAsync(CancellationToken annulering = default) =>
        Task.FromResult<IReadOnlyList<Administratie>>(Opgeslagen.ToList());

    public Task BewaarAsync(Administratie administratie, CancellationToken annulering = default)
    {
        AantalKeerBewaard++;
        return Task.CompletedTask;
    }
}

/// <summary>Ledengegevens in het geheugen; dient zowel het tabblad Leden als de import.</summary>
internal sealed class InMemoryLedenOpslag : ILedenRepository, ILedenImportOpslag
{
    public ImportStand Stand { get; private set; } = ImportStand.Leeg;
    public List<(Guid Id, string? Notities)> BewaardeNotities { get; } = [];
    public int AantalKeerGeladen { get; private set; }

    public Task<IReadOnlyList<Persoon>> HaalAlleAsync(CancellationToken annulering = default)
    {
        AantalKeerGeladen++;
        return Task.FromResult<IReadOnlyList<Persoon>>(Stand.Personen.ToList());
    }

    public Task<IReadOnlyList<Groep>> HaalGroepenAsync(CancellationToken annulering = default) =>
        Task.FromResult<IReadOnlyList<Groep>>(Stand.Groepen.ToList());

    public Task BewaarNotitiesAsync(Guid persoonId, string? notities, DateTime nuUtc, CancellationToken annulering = default)
    {
        var persoon = Stand.Personen.SingleOrDefault(p => p.Id == persoonId) ?? throw new KeyNotFoundException();
        persoon.WijzigNotities(notities, nuUtc);
        BewaardeNotities.Add((persoonId, notities));
        return Task.CompletedTask;
    }

    public Task<ImportStand> LaadAsync(CancellationToken annulering = default) => Task.FromResult(Stand);

    public Task PasToeAsync(Func<ImportStand, ImportWijzigingen> wijziging, CancellationToken annulering = default)
    {
        var w = wijziging(Stand);
        Stand = new ImportStand(
            [.. Stand.Personen, .. w.NieuwePersonen], [.. Stand.Groepen, .. w.NieuweGroepen],
            [.. Stand.Betalers, .. w.NieuweBetalers], [.. Stand.Koppelingen, .. w.NieuweKoppelingen]);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryInstellingenOpslag : IInstellingenOpslag
{
    public GebruikersInstellingen Instellingen { get; set; } = new();

    public GebruikersInstellingen Laad() => Instellingen;

    public void Bewaar(GebruikersInstellingen instellingen) => Instellingen = instellingen;
}

internal sealed class FakeThemaService : IThemaService
{
    public List<Thema> Toegepast { get; } = [];

    public void PasToe(Thema thema) => Toegepast.Add(thema);
}

internal sealed class VasteTijd(DateTimeOffset nu) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => nu;
}
