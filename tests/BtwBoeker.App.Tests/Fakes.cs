using BtwBoeker.App.Diensten;
using BtwBoeker.Application.Administraties;
using BtwBoeker.Application.Beveiliging;
using BtwBoeker.Application.Database;
using BtwBoeker.Application.Instellingen;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.App.Tests;

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
    public string? XmlDoel { get; set; }
    public string? ExcelBestand { get; set; }
    public string? XlsxDoel { get; set; }
    public string? CsvDoel { get; set; }

    /// <summary>Wat de "gebruiker" in de importwizard van de boekhouding doet; standaard annuleren.</summary>
    public Func<ViewModels.Boekhouding.BoekhoudingImportViewModel, Task<bool>>? BoekhoudingImport { get; set; }

    public string? KiesExcelBestand() => ExcelBestand;
    public string? KiesCsvDoel(string voorgesteldeNaam) => CsvDoel;

    public IReadOnlyList<string> Bijlagen { get; set; } = [];
    public string? BijlageDoel { get; set; }
    public ViewModels.Boekhouding.BijlageVoorbeeldViewModel? GetoondeBijlage { get; private set; }

    public IReadOnlyList<string> KiesBijlagen() => Bijlagen;

    public string? KiesBijlageDoel(string voorgesteldeNaam) => BijlageDoel;

    public void ToonBijlage(ViewModels.Boekhouding.BijlageVoorbeeldViewModel voorbeeld) => GetoondeBijlage = voorbeeld;

    public IReadOnlyList<string>? CamtBestanden { get; set; }
    public Func<ViewModels.Boekhouding.BankImportViewModel, Task<bool>>? BankImport { get; set; }

    public IReadOnlyList<string>? KiesCamtBestanden() => CamtBestanden;

    public bool ToonBankImport(ViewModels.Boekhouding.BankImportViewModel wizard) =>
        BankImport is not null && BankImport(wizard).GetAwaiter().GetResult();

    public string? KiesXlsxDoel(string voorgesteldeNaam) => XlsxDoel;

    public string? PdfDoel { get; set; }
    public string? PdfVoorstel { get; private set; }
    public string? Afbeelding { get; set; }

    public string? KiesPdfDoel(string voorgesteldeNaam)
    {
        PdfVoorstel = voorgesteldeNaam;
        return PdfDoel;
    }

    public string? KiesAfbeelding() => Afbeelding;

    public string? Sjabloonbestand { get; set; }
    public string? SjabloonDoel { get; set; }
    public string? SjabloonVoorstel { get; private set; }
    public Func<ViewModels.Boekhouding.StandaardinrichtingViewModel, Task<bool>>? Standaardinrichting { get; set; }

    public string? KiesSjabloonbestand() => Sjabloonbestand;

    public string? KiesSjabloonDoel(string voorgesteldeNaam)
    {
        SjabloonVoorstel = voorgesteldeNaam;
        return SjabloonDoel;
    }

    public List<ViewModels.Boekhouding.JaarrekeningIndelingViewModel> Indelingen { get; } = [];

    /// <summary>Wat het venster doet: standaard niets (annuleren).</summary>
    public Func<ViewModels.Boekhouding.JaarrekeningIndelingViewModel, bool> IndelingActie { get; set; } = _ => false;

    public bool ToonJaarrekeningIndeling(ViewModels.Boekhouding.JaarrekeningIndelingViewModel editor)
    {
        Indelingen.Add(editor);
        return IndelingActie(editor);
    }

    public bool ToonStandaardinrichting(ViewModels.Boekhouding.StandaardinrichtingViewModel wizard) =>
        Standaardinrichting is not null && Standaardinrichting(wizard).GetAwaiter().GetResult();

    public bool ToonBoekhoudingImport(ViewModels.Boekhouding.BoekhoudingImportViewModel wizard) =>
        BoekhoudingImport is not null && BoekhoudingImport(wizard).GetAwaiter().GetResult();

    public string? KiesXmlDoel(string voorgesteldeNaam) => XmlDoel;

    public string? XafDoel { get; set; }

    public string? XafVoorstel { get; private set; }

    public string? KiesXafDoel(string voorgesteldeNaam)
    {
        XafVoorstel = voorgesteldeNaam;
        return XafDoel;
    }

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

    public string? Informatie { get; private set; }

    public void ToonInformatie(string titel, string melding) => Informatie = melding;

    public Func<ViewModels.Boekhouding.HistorieImportViewModel, Task<bool>>? HistorieImport { get; set; }

    public bool ToonHistorieImport(ViewModels.Boekhouding.HistorieImportViewModel wizard) =>
        HistorieImport is not null && HistorieImport(wizard).GetAwaiter().GetResult();

    /// <summary>Wat de "gebruiker" in de startwizard doet; standaard direct sluiten (zoals ✕).</summary>
    public Func<ViewModels.Inrichting.StartwizardViewModel, Task>? Startwizard { get; set; }

    public List<ViewModels.Inrichting.StartwizardViewModel> GetoondeStartwizards { get; } = [];

    public void ToonStartwizard(ViewModels.Inrichting.StartwizardViewModel wizard)
    {
        GetoondeStartwizards.Add(wizard);
        if (Startwizard is not null)
        {
            Startwizard(wizard).GetAwaiter().GetResult();
        }

        if (!wizard.IsGesloten)
        {
            wizard.LaterVerderCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        }
    }
}

internal sealed class FakeDatabaseBeheer : IDatabaseBeheer
{
    private readonly Dictionary<string, string> _wachtwoorden = new(StringComparer.OrdinalIgnoreCase);

    public Herstelsleutel Herstelsleutel { get; } = Herstelsleutel.Genereer();
    public InMemoryAdministratieRepository Repository { get; } = new();
    public InMemoryStartwizardOpslag Startwizard { get; } = new();
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

        LaatsteSessie = new FakeSessie(pad, Repository) { Startwizard = Startwizard };
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

internal sealed class FakeSessie(string pad, IAdministratieRepository administraties) : IDatabaseSessie
{
    public BtwBoeker.Application.Boekhouding.IBoekhoudingRepository Boekhouding => field ??= new InMemoryBoekhoudingOpslag();
    public string Pad { get; } = pad;
    public IAdministratieRepository Administraties { get; } = administraties;
    public BtwBoeker.Application.Inrichting.IStartwizardOpslag Startwizard { get; init; } = new InMemoryStartwizardOpslag();
    public List<string> Backups { get; } = [];
    public bool Gesloten { get; private set; }
    public event EventHandler? Gewijzigd;
    public bool HeeftLuisteraar => Gewijzigd is not null;
    public void MeldWijziging() => Gewijzigd?.Invoke(this, EventArgs.Empty);

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

internal sealed class InMemoryStartwizardOpslag : BtwBoeker.Application.Inrichting.IStartwizardOpslag
{
    public BtwBoeker.Application.Inrichting.Startwizardvoortgang Voortgang { get; set; } = BtwBoeker.Application.Inrichting.Startwizardvoortgang.Nieuw;

    public Task<BtwBoeker.Application.Inrichting.Startwizardvoortgang> LaadAsync(CancellationToken annulering = default) => Task.FromResult(Voortgang);

    public Task BewaarAsync(BtwBoeker.Application.Inrichting.Startwizardvoortgang voortgang, CancellationToken annulering = default)
    {
        Voortgang = voortgang;
        return Task.CompletedTask;
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

/// <summary>Grootboek in het geheugen, per administratie; ID's worden uitgedeeld zoals de database dat doet.</summary>
internal sealed class InMemoryBoekhoudingOpslag : BtwBoeker.Application.Boekhouding.IBoekhoudingRepository
{
    private readonly Dictionary<AdministratieSoort, List<object>> _gegevens = [];
    private int _id = 1;

    public List<object> Van(AdministratieSoort administratie) =>
        _gegevens.TryGetValue(administratie, out var lijst) ? lijst : _gegevens[administratie] = [];

    /// <summary>Hoe vaak de hele boekhouding is geladen (besluit 234: niet dubbel laden).</summary>
    public int KeerGeladen { get; set; }

    public Task<BtwBoeker.Application.Boekhouding.BoekhoudingStand> LaadAsync(AdministratieSoort administratie, CancellationToken annulering = default)
    {
        KeerGeladen++;
        return Task.FromResult(Stand(administratie));
    }

    public Task<byte[]?> LaadBijlageInhoudAsync(Guid bijlageId, CancellationToken annulering = default) =>
        Task.FromResult(_gegevens.Values.SelectMany(l => l).OfType<Domain.Financien.Bijlagen.BijlageInhoud>()
            .FirstOrDefault(i => i.BijlageId == bijlageId)?.Gegevens);

    public Task<T> PasToeAsync<T>(
        AdministratieSoort administratie,
        Func<BtwBoeker.Application.Boekhouding.BoekhoudingStand, BtwBoeker.Application.Boekhouding.BoekhoudingWijziging<T>> wijziging,
        CancellationToken annulering = default)
    {
        var resultaat = wijziging(Stand(administratie));
        var lijst = Van(administratie);
        foreach (var item in resultaat.Verwijderen)
        {
            lijst.Remove(item);
            if (item is Domain.Financien.Bijlagen.Bijlage bijlage)
            {
                lijst.RemoveAll(i => i is Domain.Financien.Bijlagen.BijlageInhoud inhoud && inhoud.BijlageId == bijlage.Id);
            }
        }

        foreach (var item in resultaat.Toevoegen)
        {
            var id = item.GetType().GetProperty("Id");
            if (id is not null && id.PropertyType == typeof(int) && (int)id.GetValue(item)! == 0)
            {
                id.SetValue(item, _id++);
            }

            lijst.Add(item);
        }

        return Task.FromResult(resultaat.Uitkomst());
    }

    private BtwBoeker.Application.Boekhouding.BoekhoudingStand Stand(AdministratieSoort administratie)
    {
        var lijst = Van(administratie);
        return new(administratie,
            [.. lijst.OfType<Domain.Financien.Boekhouding.Rubriek>().OrderBy(r => r.Volgorde)],
            [.. lijst.OfType<Domain.Financien.Boekhouding.Grootboekrekening>().OrderBy(r => r.Nummer)],
            [.. lijst.OfType<Domain.Financien.Boekhouding.Dagboek>().OrderBy(d => d.Soort)],
            [.. lijst.OfType<Domain.Financien.Boekhouding.Boekjaar>().OrderBy(b => b.Jaar)],
            [.. lijst.OfType<Domain.Financien.Boekhouding.Journaalpost>().OrderBy(p => p.Datum).ThenBy(p => p.Volgnummer ?? int.MaxValue)])
        {
            Bankafschriften = [.. lijst.OfType<Domain.Financien.Bank.Bankafschrift>().OrderBy(a => a.Datum)],
            Bankmutaties = [.. lijst.OfType<Domain.Financien.Bank.Bankmutatie>().OrderBy(m => m.Boekdatum)],
            Boekingsregels = [.. lijst.OfType<Domain.Financien.Bank.Boekingsregel>().OrderBy(r => r.Volgorde).ThenBy(r => r.Id)],
            // Relaties zijn gedeeld door beide administraties (besluit 105).
            Relaties = [.. _gegevens.Values.SelectMany(l => l).OfType<Domain.Financien.Relaties.Relatie>().OrderBy(r => r.Naam, StringComparer.CurrentCulture)],
            HistorischeRegels = [.. lijst.OfType<Domain.Financien.Boekhouding.HistorischeRegel>()],
            HistorischeTotalen = [.. lijst.OfType<Domain.Financien.Boekhouding.HistorischTotaal>()],
            Bijlagen = [.. lijst.OfType<Domain.Financien.Bijlagen.Bijlage>().OrderBy(b => b.ToegevoegdUtc)],
            Jaarrekeningindelingen = [.. lijst.OfType<Domain.Financien.Boekhouding.Jaarrekeningindeling>()],
        };
    }
}

/// <summary>Leest een vooraf klaargezet werkblad.</summary>
internal static class FakeBoekhouding
{
    public static BoekhoudingHulpmiddelen Hulpmiddelen() =>
        new(new FakeWerkbladLezer(), new FakeSpreadsheetSchrijver(), new FakeAfdrukDienst());
}

internal sealed class FakeWerkbladLezer : BtwBoeker.Application.Boekhouding.IWerkbladLezer
{
    public BtwBoeker.Application.Boekhouding.Werkblad? Blad { get; set; }

    public BtwBoeker.Application.Boekhouding.Werkblad Lees(string pad, string werkblad) =>
        Blad ?? throw new InvalidOperationException("Het Excel-bestand kan niet worden gelezen.");

    /// <summary>Alle werkbladen; standaard alleen het Voorblad.</summary>
    public IReadOnlyList<BtwBoeker.Application.Boekhouding.GenoemdWerkblad>? Bladen { get; set; }

    public IReadOnlyList<BtwBoeker.Application.Boekhouding.GenoemdWerkblad> LeesAlle(string pad) =>
        Bladen ?? [new(BtwBoeker.Application.Boekhouding.VoorbladInlezer.Werkbladnaam, Lees(pad, BtwBoeker.Application.Boekhouding.VoorbladInlezer.Werkbladnaam))];
}

internal sealed class FakeSpreadsheetSchrijver : BtwBoeker.Application.Boekhouding.ISpreadsheetSchrijver
{
    public List<(BtwBoeker.Application.Boekhouding.RapportTabel Tabel, string Pad)> Geschreven { get; } = [];

    public void Schrijf(BtwBoeker.Application.Boekhouding.RapportTabel tabel, string pad) => Geschreven.Add((tabel, pad));
}

internal sealed class FakeAfdrukDienst : IAfdrukDienst
{
    public List<BtwBoeker.Application.Boekhouding.RapportTabel> Afgedrukt { get; } = [];

    public bool Druk(BtwBoeker.Application.Boekhouding.RapportTabel tabel)
    {
        Afgedrukt.Add(tabel);
        return true;
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
