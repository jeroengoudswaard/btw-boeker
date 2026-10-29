using Verenigd.App.ViewModels.Leden;
using Verenigd.Application.Import;
using Verenigd.Domain.Leden;
using static Verenigd.Application.Tests.Import.Sol3Voorbeeld;

namespace Verenigd.App.Tests.Leden;

public sealed class ImportWizardViewModelTests : IDisposable
{
    private readonly DirectoryInfo _map = Directory.CreateTempSubdirectory("verenigd-wizard-");
    private readonly FakeDialoogService _dialogen = new();
    private readonly InMemoryLedenOpslag _opslag = new();
    private readonly FakeSessie _sessie;

    public ImportWizardViewModelTests()
    {
        _sessie = new FakeSessie(Path.Combine(_map.FullName, "test.vdb"), new InMemoryAdministratieRepository(), _opslag);
    }

    public void Dispose() => _map.Delete(recursive: true);

    private ImportWizardViewModel Maak() =>
        new(new Sol3ImportDienst(_sessie, new VasteTijd(new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero))), _dialogen);

    private string Schrijf(string naam, byte[] inhoud)
    {
        var pad = Path.Combine(_map.FullName, naam);
        File.WriteAllBytes(pad, inhoud);
        return pad;
    }

    private string[] VierBestanden(string groep = "Welpen") =>
    [
        Schrijf("leden.csv", Csv(KopLeden, Lid("100000001"), Lid("100000002", voornaam: "Bo"))),
        Schrijf("huishouden.csv", Csv(KopHuishouden, Huishouden("100000001"), Huishouden("100000002"))),
        Schrijf("voogden.csv", Csv(KopVoogden, Voogden("100000001"))),
        Schrijf("functies.csv", Csv(KopFuncties, Functie("100000001", groep: groep), Functie("100000002", groep: groep))),
    ];

    [Fact]
    public void Start_bij_stap_bestanden_met_vier_lege_regels_en_volgende_uitgeschakeld()
    {
        var wizard = Maak();

        Assert.Equal(ImportStap.Bestanden, wizard.Stap);
        Assert.Equal(4, wizard.Bestanden.Count);
        Assert.False(wizard.VolgendeCommand.CanExecute(null));
    }

    [Fact]
    public void Bestanden_worden_herkend_in_willekeurige_volgorde()
    {
        var wizard = Maak();

        wizard.VoegBestandenToe(VierBestanden().Reverse());

        Assert.All(wizard.Bestanden, b => Assert.True(b.IsGeldig));
        Assert.Equal("leden.csv", wizard.Bestanden[0].Bestandsnaam);
        Assert.Equal(2, wizard.Bestanden[0].AantalRegels);
        Assert.True(wizard.VolgendeCommand.CanExecute(null));
    }

    [Fact]
    public void Bladeren_gebruikt_de_bestandskiezer()
    {
        _dialogen.Importbestanden = VierBestanden();
        var wizard = Maak();

        wizard.BladerenCommand.Execute(null);

        Assert.True(wizard.VolgendeCommand.CanExecute(null));
    }

    [Fact]
    public void Onbekend_bestand_geeft_melding_en_telt_niet_mee()
    {
        var wizard = Maak();

        wizard.VoegBestandenToe([Schrijf("x.csv", Csv("Naam;Adres", ["a", "b"]))]);

        Assert.Contains("x.csv", wizard.Foutmelding, StringComparison.Ordinal);
        Assert.False(wizard.VolgendeCommand.CanExecute(null));
    }

    [Fact]
    public void Ontbrekende_export_houdt_volgende_uitgeschakeld()
    {
        var wizard = Maak();

        wizard.VoegBestandenToe(VierBestanden().Take(3));

        Assert.False(wizard.Bestanden[3].IsGeldig);
        Assert.False(wizard.VolgendeCommand.CanExecute(null));
    }

    [Fact]
    public async Task Nieuwe_groepen_geven_stap_groepen_met_voorstel()
    {
        var wizard = Maak();
        wizard.VoegBestandenToe(VierBestanden(groep: "Vrijwilligers (Migratie)"));

        await wizard.VolgendeCommand.ExecuteAsync(null);

        Assert.Equal(ImportStap.Groepen, wizard.Stap);
        var groep = Assert.Single(wizard.Groepen);
        Assert.Equal(GroepSoort.Technisch, groep.Soort);
    }

    [Fact]
    public async Task Controleren_toont_nieuw_en_gekozen_groepsoort_wordt_gebruikt()
    {
        var wizard = Maak();
        wizard.VoegBestandenToe(VierBestanden());
        await wizard.VolgendeCommand.ExecuteAsync(null);
        wizard.Groepen[0].Soort = GroepSoort.Overig;

        await wizard.VolgendeCommand.ExecuteAsync(null);
        Assert.Equal(ImportStap.Controleren, wizard.Stap);
        Assert.Equal(2, wizard.Nieuw.Count);
        Assert.Equal("Importeren", wizard.Volgendetekst);

        await wizard.VolgendeCommand.ExecuteAsync(null);
        Assert.Equal(ImportStap.Klaar, wizard.Stap);
        Assert.Equal(GroepSoort.Overig, Assert.Single(_opslag.Stand.Groepen).Soort);
        Assert.Single(_sessie.Backups);
        Assert.Equal(2, wizard.Resultaat?.Nieuw);
    }

    [Fact]
    public async Task Vorige_gaat_terug_zonder_te_importeren()
    {
        var wizard = Maak();
        wizard.VoegBestandenToe(VierBestanden());
        await wizard.VolgendeCommand.ExecuteAsync(null);
        await wizard.VolgendeCommand.ExecuteAsync(null);

        wizard.VorigeCommand.Execute(null);
        Assert.Equal(ImportStap.Groepen, wizard.Stap);
        wizard.VorigeCommand.Execute(null);
        Assert.Equal(ImportStap.Bestanden, wizard.Stap);
        Assert.False(wizard.VorigeCommand.CanExecute(null));
        Assert.Empty(_opslag.Stand.Personen);
    }

    [Fact]
    public async Task Uitschrijven_alleen_voor_aangevinkte_leden()
    {
        var wizard = Maak();
        wizard.VoegBestandenToe(VierBestanden());
        await wizard.VolgendeCommand.ExecuteAsync(null);
        await wizard.VolgendeCommand.ExecuteAsync(null);
        await wizard.VolgendeCommand.ExecuteAsync(null);

        var tweede = Maak();
        tweede.VoegBestandenToe(
        [
            Schrijf("leden2.csv", Csv(KopLeden, Lid("100000001"))),
            Schrijf("huishouden2.csv", Csv(KopHuishouden, Huishouden("100000001"))),
            Schrijf("voogden2.csv", Csv(KopVoogden, Voogden("100000001"))),
            Schrijf("functies2.csv", Csv(KopFuncties, Functie("100000001"))),
        ]);
        await tweede.VolgendeCommand.ExecuteAsync(null);
        Assert.Equal(ImportStap.Controleren, tweede.Stap);
        var regel = Assert.Single(tweede.Uitgeschreven);
        Assert.True(regel.Uitschrijven);
        regel.Uitschrijven = false;

        await tweede.VolgendeCommand.ExecuteAsync(null);

        Assert.All(_opslag.Stand.Personen, p => Assert.False(p.IsUitgeschreven));
        Assert.Equal(0, tweede.Resultaat?.Uitgeschreven);
    }

    [Fact]
    public async Task Afwijkend_iban_wordt_alleen_overgenomen_als_het_is_aangevinkt()
    {
        string[] Bestanden(string achtervoegsel, string iban1, string iban2) =>
        [
            Schrijf($"leden{achtervoegsel}.csv", Csv(KopLeden, Lid("100000001"), Lid("100000002", voornaam: "Bo"))),
            Schrijf($"huishouden{achtervoegsel}.csv", Csv(KopHuishouden, Huishouden("100000001", iban: iban1), Huishouden("100000002", iban: iban2))),
            Schrijf($"voogden{achtervoegsel}.csv", Csv(KopVoogden, Voogden("100000001"))),
            Schrijf($"functies{achtervoegsel}.csv", Csv(KopFuncties, Functie("100000001"), Functie("100000002"))),
        ];
        var eerste = Maak();
        eerste.VoegBestandenToe(Bestanden("1", "NL91ABNA0417164300", "NL91ABNA0417164300"));
        await eerste.VolgendeCommand.ExecuteAsync(null);
        await eerste.VolgendeCommand.ExecuteAsync(null);
        await eerste.VolgendeCommand.ExecuteAsync(null);

        var tweede = Maak();
        tweede.VoegBestandenToe(Bestanden("2", "BE68539007547034", "BE68539007547034"));
        await tweede.VolgendeCommand.ExecuteAsync(null);

        Assert.Equal(2, tweede.IbanVerschillen.Count);
        Assert.All(tweede.IbanVerschillen, r => Assert.False(r.Overnemen));
        Assert.Equal("IBAN-verschillen (2)", tweede.IbanVerschillenKop);
        tweede.IbanVerschillen.Single(r => r.Lidnummer == "100000002").Overnemen = true;

        await tweede.VolgendeCommand.ExecuteAsync(null);

        var stand = _opslag.Stand;
        Assert.Equal(2, stand.Betalers.Count);
        Assert.NotEqual(stand.Koppelingen.Single(k => k.Lidnummer.Waarde == "100000001").BetalerId,
            stand.Koppelingen.Single(k => k.Lidnummer.Waarde == "100000002").BetalerId);
    }

    [Fact]
    public async Task Ongewijzigde_import_kan_niet_worden_uitgevoerd()
    {
        var wizard = Maak();
        wizard.VoegBestandenToe(VierBestanden());
        await wizard.VolgendeCommand.ExecuteAsync(null);
        await wizard.VolgendeCommand.ExecuteAsync(null);
        await wizard.VolgendeCommand.ExecuteAsync(null);

        var tweede = Maak();
        tweede.VoegBestandenToe(VierBestanden());
        await tweede.VolgendeCommand.ExecuteAsync(null);

        Assert.True(tweede.IsOngewijzigd);
        Assert.False(tweede.VolgendeCommand.CanExecute(null));
    }

    [Fact]
    public async Task Waarschuwingen_worden_getoond_en_kunnen_als_csv_worden_opgeslagen()
    {
        var wizard = Maak();
        wizard.VoegBestandenToe(
        [
            Schrijf("leden.csv", Csv(KopLeden, Lid("100000001", status: "Raar"))),
            Schrijf("huishouden.csv", Csv(KopHuishouden, Huishouden("100000001"))),
            Schrijf("voogden.csv", Csv(KopVoogden)),
            Schrijf("functies.csv", Csv(KopFuncties, Functie("100000001"))),
        ]);
        await wizard.VolgendeCommand.ExecuteAsync(null);
        await wizard.VolgendeCommand.ExecuteAsync(null);
        _dialogen.CsvDoel = Path.Combine(_map.FullName, "waarschuwingen.csv");

        Assert.NotEmpty(wizard.Waarschuwingen);
        await wizard.WaarschuwingenOpslaanCommand.ExecuteAsync(null);

        var inhoud = await File.ReadAllTextAsync(_dialogen.CsvDoel);
        Assert.StartsWith("\"Export\";\"Regel\";\"Lidnummer\";\"Melding\"", inhoud.TrimStart('﻿'), StringComparison.Ordinal);
        Assert.Contains("Raar", inhoud, StringComparison.Ordinal);
    }
}
