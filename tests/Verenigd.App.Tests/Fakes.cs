using Verenigd.App.Diensten;
using Verenigd.Application.Administraties;
using Verenigd.Application.Beveiliging;
using Verenigd.Application.Database;
using Verenigd.Application.Import;
using Verenigd.Application.Incasso;
using Verenigd.Application.Instellingen;
using Verenigd.Application.Leden;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
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

    public string? XmlDoel { get; set; }
    public string? ExcelBestand { get; set; }
    public string? XlsxDoel { get; set; }

    /// <summary>Wat de "gebruiker" in de importwizard van de boekhouding doet; standaard annuleren.</summary>
    public Func<ViewModels.Boekhouding.BoekhoudingImportViewModel, Task<bool>>? BoekhoudingImport { get; set; }

    public string? KiesExcelBestand() => ExcelBestand;

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

    public string? Clientbestand { get; set; }

    public string? KiesClientbestand() => Clientbestand;

    public string? KiesSjabloonDoel(string voorgesteldeNaam)
    {
        SjabloonVoorstel = voorgesteldeNaam;
        return SjabloonDoel;
    }

    public bool ToonStandaardinrichting(ViewModels.Boekhouding.StandaardinrichtingViewModel wizard) =>
        Standaardinrichting is not null && Standaardinrichting(wizard).GetAwaiter().GetResult();

    public bool ToonBoekhoudingImport(ViewModels.Boekhouding.BoekhoudingImportViewModel wizard) =>
        BoekhoudingImport is not null && BoekhoudingImport(wizard).GetAwaiter().GetResult();

    /// <summary>Wat de "gebruiker" in de incassowizard doet; standaard annuleren.</summary>
    public Func<ViewModels.Contributie.IncassoWizardViewModel, Task<bool>>? IncassoWizard { get; set; }

    public string? KiesXmlDoel(string voorgesteldeNaam) => XmlDoel;

    public bool ToonIncassoWizard(ViewModels.Contributie.IncassoWizardViewModel wizard) =>
        IncassoWizard is not null && IncassoWizard(wizard).GetAwaiter().GetResult();

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
    public InMemoryLedenOpslag Leden { get; } = new();
    public InMemoryContributieOpslag Contributie { get; } = new();
    public InMemoryStartwizardOpslag Startwizard { get; } = new();
    public InMemoryEmailOpslag Email { get; } = new();
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

        Contributie.Leden = Leden;
        LaatsteSessie = new FakeSessie(pad, Repository, Leden, Contributie) { Startwizard = Startwizard, Email = Email };
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

internal sealed class FakeSessie(
    string pad, IAdministratieRepository administraties, InMemoryLedenOpslag leden, InMemoryContributieOpslag? contributie = null) : IDatabaseSessie
{
    private readonly InMemoryContributieOpslag _contributie = contributie ?? new InMemoryContributieOpslag { Leden = leden };

    public IContributieRepository Contributie => _contributie;
    public Verenigd.Application.Boekhouding.IBoekhoudingRepository Boekhouding => field ??= new InMemoryBoekhoudingOpslag(_contributie);
    public string Pad { get; } = pad;
    public IAdministratieRepository Administraties { get; } = administraties;
    public ILedenRepository Leden { get; } = leden;
    public ILedenImportOpslag LedenImport { get; } = leden;
    public Verenigd.Application.Inrichting.IStartwizardOpslag Startwizard { get; init; } = new InMemoryStartwizardOpslag();
    public Verenigd.Application.Email.IEmailOpslag Email { get; init; } = new InMemoryEmailOpslag();
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

internal sealed class InMemoryStartwizardOpslag : Verenigd.Application.Inrichting.IStartwizardOpslag
{
    public Verenigd.Application.Inrichting.Startwizardvoortgang Voortgang { get; set; } = Verenigd.Application.Inrichting.Startwizardvoortgang.Nieuw;

    public Task<Verenigd.Application.Inrichting.Startwizardvoortgang> LaadAsync(CancellationToken annulering = default) => Task.FromResult(Voortgang);

    public Task BewaarAsync(Verenigd.Application.Inrichting.Startwizardvoortgang voortgang, CancellationToken annulering = default)
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

/// <summary>Contributie en incasso in het geheugen, met dezelfde regels als de echte opslag.</summary>
internal sealed class InMemoryContributieOpslag : IContributieRepository
{
    private int _volgendId = 1;

    public InMemoryLedenOpslag Leden { get; set; } = new();
    public List<Contributiecategorie> Categorieen { get; } = [];
    public List<Tarief> Tarieven { get; } = [];
    public List<LidCategorie> LidCategorieen { get; } = [];
    public List<Betaler> Betalers { get; } = [];
    public List<Domain.Financien.BetalerLid> Koppelingen { get; } = [];
    public List<Incassorun> Runs { get; } = [];
    public List<Domain.Financien.Debiteuren.Herincasso> Herincassos { get; } = [];
    public Incassoinstellingen? Instellingen { get; set; }

    public Task<IncassoStand> LaadAsync(CancellationToken annulering = default) =>
        Task.FromResult(new IncassoStand(
            Leden.Stand.Personen.ToList(), Categorieen.ToList(), Tarieven.ToList(), LidCategorieen.ToList(), Betalers.ToList(),
            Koppelingen.ToList(), Runs.ToList(), Instellingen) { Herincassos = Herincassos.ToList() });

    public Task VraagHerincassoAanAsync(Domain.Financien.Debiteuren.Herincasso herincasso, CancellationToken annulering = default)
    {
        Herincassos.Add(herincasso);
        return Task.CompletedTask;
    }

    public Task BewaarOpbrengstrekeningenAsync(IReadOnlyDictionary<int, int?> perCategorie, CancellationToken annulering = default)
    {
        foreach (var (id, rekening) in perCategorie)
        {
            Categorieen.Single(c => c.Id == id).KoppelOpbrengstrekening(rekening);
        }

        return Task.CompletedTask;
    }

    public Task VoegCategorieenToeAsync(IReadOnlyList<Contributiecategorie> categorieen, CancellationToken annulering = default)
    {
        foreach (var categorie in categorieen)
        {
            typeof(Contributiecategorie).GetProperty(nameof(Contributiecategorie.Id))!.SetValue(categorie, _volgendId++);
            Categorieen.Add(categorie);
        }

        return Task.CompletedTask;
    }

    public Task BewaarTarievenAsync(int boekjaar, IReadOnlyDictionary<int, Bedrag> bedragen, CancellationToken annulering = default)
    {
        foreach (var (id, bedrag) in bedragen)
        {
            if (Tarieven.Find(t => t.Boekjaar == boekjaar && t.Categorie.Id == id) is { } tarief)
            {
                tarief.WijzigBedrag(bedrag);
            }
            else
            {
                Tarieven.Add(new Tarief(Categorieen.Single(c => c.Id == id), boekjaar, bedrag));
            }
        }

        return Task.CompletedTask;
    }

    public Task BewaarLidCategorieenAsync(IReadOnlyList<LidCategorieInvoer> invoer, CancellationToken annulering = default)
    {
        foreach (var regel in invoer)
        {
            LidCategorieen.RemoveAll(c => c.Lidnummer == regel.Lidnummer && c.Ingangsdatum == regel.Ingangsdatum);
            LidCategorieen.Add(new LidCategorie(regel.Lidnummer, Categorieen.Single(c => c.Id == regel.CategorieId), regel.Ingangsdatum));
        }

        return Task.CompletedTask;
    }

    public Task<Guid> BewaarBetalerAsync(BetalerInvoer invoer, CancellationToken annulering = default)
    {
        var fouten = invoer.Fouten(Betalers);
        if (fouten.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", fouten));
        }

        var iban = Domain.Financien.Iban.Maak(invoer.Iban);
        var betaler = invoer.Id is { } id ? Betalers.Single(b => b.Id == id) : new Betaler(iban, invoer.Rekeninghouder);
        if (invoer.Id is null)
        {
            Betalers.Add(betaler);
        }

        betaler.WijzigIban(iban);
        betaler.WijzigRekeninghouder(invoer.Rekeninghouder);
        if (string.IsNullOrWhiteSpace(invoer.Mandaatkenmerk))
        {
            betaler.VerwijderMachtiging();
        }
        else
        {
            betaler.RegistreerMachtiging(invoer.Mandaatkenmerk, invoer.OndertekendOp!.Value);
            if (invoer.IngetrokkenOp is { } ingetrokken)
            {
                betaler.TrekMachtigingIn(ingetrokken);
            }
        }

        Koppelingen.RemoveAll(k => k.BetalerId == betaler.Id && !invoer.Leden.Contains(k.Lidnummer));
        foreach (var lidnummer in invoer.Leden)
        {
            if (Koppelingen.Find(k => k.Lidnummer == lidnummer) is { } koppeling)
            {
                koppeling.KoppelAan(betaler.Id);
            }
            else
            {
                Koppelingen.Add(new Domain.Financien.BetalerLid(lidnummer, betaler.Id));
            }
        }

        return Task.FromResult(betaler.Id);
    }

    public Task BewaarInstellingenAsync(IncassoinstellingenInvoer invoer, CancellationToken annulering = default)
    {
        if (!IncassantId.TryMaak(invoer.IncassantId, out var id))
        {
            throw new ArgumentException("Het incassant-ID is ongeldig.");
        }

        var iban = Domain.Financien.Iban.Maak(invoer.Iban);
        Instellingen ??= new Incassoinstellingen(id, invoer.Naam, iban, invoer.Incassodag, invoer.EersteIncassomaand);
        Instellingen.Wijzig(id, invoer.Naam, iban, invoer.Incassodag, invoer.EersteIncassomaand, invoer.Omschrijvingssjabloon);
        return Task.CompletedTask;
    }

    public async Task BewaarRunAsync(Func<IncassoStand, Incassorun> maakRun, CancellationToken annulering = default) =>
        Runs.Add(maakRun(await LaadAsync(annulering)));

    public Task LegBestandVastAsync(Guid runId, string hash, CancellationToken annulering = default)
    {
        Runs.Single(r => r.Id == runId).LegBestandVast(hash);
        return Task.CompletedTask;
    }

    public Task AnnuleerRunAsync(Guid runId, DateTime nuUtc, CancellationToken annulering = default)
    {
        var run = Runs.Single(r => r.Id == runId);
        if (run.Status == IncassorunStatus.Definitief)
        {
            run.Annuleer(nuUtc);
        }

        foreach (var herincasso in Herincassos.Where(h => h.RunId == runId))
        {
            herincasso.GeefVrij();
        }

        return Task.CompletedTask;
    }
}

/// <summary>Grootboek in het geheugen, per administratie; ID's worden uitgedeeld zoals de database dat doet.</summary>
internal sealed class InMemoryBoekhoudingOpslag(InMemoryContributieOpslag? contributie = null) : Verenigd.Application.Boekhouding.IBoekhoudingRepository
{
    private readonly Dictionary<AdministratieSoort, List<object>> _gegevens = [];
    private int _id = 1;

    public List<object> Van(AdministratieSoort administratie) =>
        _gegevens.TryGetValue(administratie, out var lijst) ? lijst : _gegevens[administratie] = [];

    public Task<Verenigd.Application.Boekhouding.BoekhoudingStand> LaadAsync(AdministratieSoort administratie, CancellationToken annulering = default) =>
        Task.FromResult(Stand(administratie));

    public Task<byte[]?> LaadBijlageInhoudAsync(Guid bijlageId, CancellationToken annulering = default) =>
        Task.FromResult(_gegevens.Values.SelectMany(l => l).OfType<Domain.Financien.Bijlagen.BijlageInhoud>()
            .FirstOrDefault(i => i.BijlageId == bijlageId)?.Gegevens);

    public Task<T> PasToeAsync<T>(
        AdministratieSoort administratie,
        Func<Verenigd.Application.Boekhouding.BoekhoudingStand, Verenigd.Application.Boekhouding.BoekhoudingWijziging<T>> wijziging,
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

    private Verenigd.Application.Boekhouding.BoekhoudingStand Stand(AdministratieSoort administratie)
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
            Debiteurmutaties = [.. lijst.OfType<Domain.Financien.Debiteuren.Debiteurmutatie>().OrderBy(m => m.Datum)],
            Incassoruns = administratie == AdministratieSoort.Vereniging && contributie is not null ? [.. contributie.Runs] : [],
            Herincassos = administratie == AdministratieSoort.Vereniging && contributie is not null ? [.. contributie.Herincassos] : [],
            // Relaties zijn gedeeld door beide administraties (besluit 105).
            Relaties = [.. _gegevens.Values.SelectMany(l => l).OfType<Domain.Financien.Relaties.Relatie>().OrderBy(r => r.Naam, StringComparer.CurrentCulture)],
            Verkoopfacturen = [.. lijst.OfType<Domain.Financien.Facturen.Verkoopfactuur>()],
            Factuurinstellingen = lijst.OfType<Domain.Financien.Facturen.Factuurinstellingen>().SingleOrDefault(),
            HistorischeRegels = [.. lijst.OfType<Domain.Financien.Boekhouding.HistorischeRegel>()],
            HistorischeTotalen = [.. lijst.OfType<Domain.Financien.Boekhouding.HistorischTotaal>()],
            Bijlagen = [.. lijst.OfType<Domain.Financien.Bijlagen.Bijlage>().OrderBy(b => b.ToegevoegdUtc)],
            Begrotingen = [.. lijst.OfType<Domain.Financien.Begrotingen.Begroting>().OrderBy(b => b.Jaar).ThenBy(b => b.Versie)],
            Meerjarenplannen = [.. lijst.OfType<Domain.Financien.Begrotingen.Meerjarenplan>().OrderBy(p => p.Startjaar).ThenBy(p => p.Versie)],
        };
    }
}

/// <summary>Leest een vooraf klaargezet werkblad.</summary>
internal static class FakeBoekhouding
{
    public static BoekhoudingHulpmiddelen Hulpmiddelen(NepVerzender? verzender = null) =>
        new(new FakeWerkbladLezer(), new FakeSpreadsheetSchrijver(), new FakeAfdrukDienst(), new FakeFactuurPdfMaker(),
            Verzender: verzender ?? new NepVerzender(), Koppeling: new NepKoppeling());
}

internal sealed class FakeFactuurPdfMaker : Verenigd.Application.Facturen.IFactuurPdfMaker
{
    public static readonly byte[] Inhoud = "%PDF-nep"u8.ToArray();

    public List<Domain.Financien.Facturen.Verkoopfactuur> Gemaakt { get; } = [];

    public byte[] Maak(Domain.Financien.Facturen.Verkoopfactuur factuur, Domain.Financien.Facturen.Factuurinstellingen instellingen)
    {
        Gemaakt.Add(factuur);
        return Inhoud;
    }
}

internal sealed class FakeWerkbladLezer : Verenigd.Application.Boekhouding.IWerkbladLezer
{
    public Verenigd.Application.Boekhouding.Werkblad? Blad { get; set; }

    public Verenigd.Application.Boekhouding.Werkblad Lees(string pad, string werkblad) =>
        Blad ?? throw new InvalidOperationException("Het Excel-bestand kan niet worden gelezen.");

    /// <summary>Alle werkbladen; standaard alleen het Voorblad.</summary>
    public IReadOnlyList<Verenigd.Application.Boekhouding.GenoemdWerkblad>? Bladen { get; set; }

    public IReadOnlyList<Verenigd.Application.Boekhouding.GenoemdWerkblad> LeesAlle(string pad) =>
        Bladen ?? [new(Verenigd.Application.Boekhouding.VoorbladInlezer.Werkbladnaam, Lees(pad, Verenigd.Application.Boekhouding.VoorbladInlezer.Werkbladnaam))];
}

internal sealed class FakeSpreadsheetSchrijver : Verenigd.Application.Boekhouding.ISpreadsheetSchrijver
{
    public List<(Verenigd.Application.Boekhouding.RapportTabel Tabel, string Pad)> Geschreven { get; } = [];

    public void Schrijf(Verenigd.Application.Boekhouding.RapportTabel tabel, string pad) => Geschreven.Add((tabel, pad));
}

internal sealed class FakeAfdrukDienst : IAfdrukDienst
{
    public List<Verenigd.Application.Boekhouding.RapportTabel> Afgedrukt { get; } = [];

    public bool Druk(Verenigd.Application.Boekhouding.RapportTabel tabel)
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

internal sealed class InMemoryEmailOpslag : Verenigd.Application.Email.IEmailOpslag
{
    private int _volgend = 1;

    public List<Verenigd.Domain.Communicatie.Emailsjabloon> Sjablonen { get; } = [];
    public List<Verenigd.Domain.Communicatie.VerzondenBericht> Logboek { get; } = [];
    public Verenigd.Domain.Communicatie.Emailinstellingen Instellingen { get; set; } = new();

    public Task<Verenigd.Application.Email.EmailStand> LaadAsync(CancellationToken annulering = default) =>
        Task.FromResult(new Verenigd.Application.Email.EmailStand([.. Sjablonen], [.. Logboek], Instellingen));

    public Task BewaarSjabloonAsync(Verenigd.Domain.Communicatie.Emailsjabloon sjabloon, CancellationToken annulering = default)
    {
        if (sjabloon.Id == 0)
        {
            typeof(Verenigd.Domain.Communicatie.Emailsjabloon).GetProperty(nameof(sjabloon.Id))!.SetValue(sjabloon, _volgend++);
            Sjablonen.Add(sjabloon);
        }

        return Task.CompletedTask;
    }

    public Task VerwijderSjabloonAsync(int id, CancellationToken annulering = default)
    {
        Sjablonen.RemoveAll(s => s.Id == id && !s.IsStandaard);
        return Task.CompletedTask;
    }

    public Task BewaarInstellingenAsync(Verenigd.Domain.Communicatie.Emailinstellingen instellingen, CancellationToken annulering = default)
    {
        Instellingen = instellingen;
        return Task.CompletedTask;
    }

    public Task LogAsync(Verenigd.Domain.Communicatie.VerzondenBericht bericht, CancellationToken annulering = default)
    {
        Logboek.Add(bericht);
        return Task.CompletedTask;
    }

    /// <summary>Gekoppeld met een verzonnen afzender, zodat versturen kan.</summary>
    public void Koppel()
    {
        Instellingen.ZetAfzender("Penningmeester", "penningmeester@example.org");
        Instellingen.ZetClient("id.apps.googleusercontent.com", "geheim");
        Instellingen.Koppel("vernieuwd", DateTime.UtcNow);
    }
}

internal sealed class NepVerzender : Verenigd.Application.Email.IEmailVerzender
{
    public List<Verenigd.Application.Email.Emailbericht> Verstuurd { get; } = [];
    public string? FoutVoor { get; set; }

    public Task VerstuurAsync(Verenigd.Domain.Communicatie.Emailinstellingen instellingen, Verenigd.Application.Email.Emailbericht bericht, CancellationToken annulering = default)
    {
        if (bericht.AanAdres == FoutVoor)
        {
            throw new Verenigd.Application.Email.EmailVerzendException("Adres bestaat niet.");
        }

        Verstuurd.Add(bericht);
        return Task.CompletedTask;
    }
}

internal sealed class NepKoppeling : Verenigd.Application.Email.IGoogleKoppeling
{
    public int Aantal { get; private set; }

    public Task<string> KoppelAsync(string clientId, string clientGeheim, CancellationToken annulering = default)
    {
        Aantal++;
        return Task.FromResult("vernieuwd");
    }
}
