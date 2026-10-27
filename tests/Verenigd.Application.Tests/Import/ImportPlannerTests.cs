using Verenigd.Application.Import;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.Application.Tests.Import;

public class ImportPlannerTests
{
    private static readonly DateOnly Importdatum = new(2026, 10, 27);
    private static readonly DateTime Nu = new(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc);

    private static ImportLid Lid(
        string lidnummer, string voornaam = "Anna", string? email = "anna@example.org", string? iban = null,
        string? rekeninghouder = null, string groep = "Welpen", DateOnly? geboortedatum = null) =>
        new(
            Lidnummer.Parse(lidnummer),
            new LidGegevens(voornaam, null, "Vries", geboortedatum ?? new DateOnly(2015, 3, 1), "Vrouw", email, null, null, "Actief",
                "Dorpsstraat", "1", null, "1234 AB", "Ergens", "Nederland"),
            [new VoogdGegevens(1, "P. de Vries", false, "p@example.org", null, null)],
            [new FunctieGegevens(groep, "Lid", Lidtype.Jeugdlid, new DateOnly(2025, 9, 19), null, "Actief")],
            iban is null ? null : Iban.Maak(iban),
            rekeninghouder);

    private static Sol3Import Import(params ImportLid[] leden) => new(leden, []);

    /// <summary>Plant en past toe zoals de echte import, en verwerkt de nieuwe objecten in de stand.</summary>
    private static ImportStand VoerUit(ImportStand stand, Sol3Import import, ImportKeuzes? keuzes = null)
    {
        var wijzigingen = ImportToepasser.PasToe(stand, import, keuzes ?? ImportKeuzes.Standaard(ImportPlanner.Plan(stand, import, Importdatum)), Importdatum, Nu);
        return new ImportStand(
            [.. stand.Personen, .. wijzigingen.NieuwePersonen],
            [.. stand.Groepen, .. wijzigingen.NieuweGroepen],
            [.. stand.Betalers, .. wijzigingen.NieuweBetalers],
            [.. stand.Koppelingen, .. wijzigingen.NieuweKoppelingen]);
    }

    [Fact]
    public void Lege_database_alles_is_nieuw_met_voorgestelde_groepsoort()
    {
        var import = Import(Lid("1", groep: "Welpen"), Lid("2", voornaam: "Bo", groep: "Vrijwilligers (Migratie)"));

        var voorstel = ImportPlanner.Plan(ImportStand.Leeg, import, Importdatum);

        Assert.Equal(2, voorstel.Nieuw.Count);
        Assert.Empty(voorstel.Gewijzigd);
        Assert.Empty(voorstel.Uitgeschreven);
        Assert.Contains(new NieuweGroep("Welpen", GroepSoort.Speltak), voorstel.NieuweGroepen);
        Assert.Contains(new NieuweGroep("Vrijwilligers (Migratie)", GroepSoort.Technisch), voorstel.NieuweGroepen);
    }

    [Fact]
    public void Dezelfde_import_twee_keer_geeft_geen_wijzigingen()
    {
        var import = Import(Lid("1", iban: "NL91ABNA0417164300", rekeninghouder: "P. de Vries"), Lid("2", voornaam: "Bo"));
        var stand = VoerUit(ImportStand.Leeg, import);

        var voorstel = ImportPlanner.Plan(stand, import, Importdatum);

        Assert.True(voorstel.IsLeeg);
        Assert.Equal(2, stand.Personen.Count);
        Assert.Single(stand.Betalers);
    }

    [Fact]
    public void Gewijzigd_veld_wordt_met_oud_en_nieuw_getoond_en_bijgewerkt()
    {
        var stand = VoerUit(ImportStand.Leeg, Import(Lid("1")));
        var import = Import(Lid("1", email: "nieuw@example.org"));

        var regel = Assert.Single(ImportPlanner.Plan(stand, import, Importdatum).Gewijzigd);
        Assert.Equal("Anna Vries", regel.Naam);
        Assert.Equal(new Veldwijziging("E-mail", "anna@example.org", "nieuw@example.org"), Assert.Single(regel.Wijzigingen));

        VoerUit(stand, import);
        Assert.Equal("nieuw@example.org", stand.Personen[0].Email);
    }

    [Fact]
    public void Verdwenen_lid_wordt_alleen_na_bevestiging_uitgeschreven()
    {
        var stand = VoerUit(ImportStand.Leeg, Import(Lid("1"), Lid("2", voornaam: "Bo")));
        var import = Import(Lid("1"));

        var voorstel = ImportPlanner.Plan(stand, import, Importdatum);
        Assert.Equal("2", Assert.Single(voorstel.Uitgeschreven).Lidnummer.Waarde);

        VoerUit(stand, import, ImportKeuzes.Standaard(voorstel) with { Uitschrijven = new HashSet<Lidnummer>() });
        Assert.False(stand.Personen[1].IsUitgeschreven);

        VoerUit(stand, import, ImportKeuzes.Standaard(voorstel));
        Assert.Equal(Importdatum, stand.Personen[1].UitgeschrevenOp);
        Assert.Equal(Importdatum, stand.Personen[1].Functies[0].Einddatum);
        Assert.Equal(2, stand.Personen.Count); // nooit verwijderen

        Assert.Empty(ImportPlanner.Plan(stand, import, Importdatum).Uitgeschreven);
    }

    [Fact]
    public void Uitgeschreven_lid_dat_terugkomt_wordt_heringeschreven()
    {
        var stand = VoerUit(ImportStand.Leeg, Import(Lid("1"), Lid("2", voornaam: "Bo")));
        stand = VoerUit(stand, Import(Lid("1")));
        var import = Import(Lid("1"), Lid("2", voornaam: "Bo"));

        var regel = Assert.Single(ImportPlanner.Plan(stand, import, Importdatum.AddDays(1)).Gewijzigd);
        Assert.Contains(regel.Wijzigingen, w => w.Veld == "Uitgeschreven op" && w.Nieuw is null);

        VoerUit(stand, import);
        Assert.False(stand.Personen[1].IsUitgeschreven);
        Assert.Null(stand.Personen[1].Functies[0].Einddatum);
    }

    [Fact]
    public void Gekozen_groepsoort_wordt_gebruikt()
    {
        var import = Import(Lid("1", groep: "Loodsen"));
        var voorstel = ImportPlanner.Plan(ImportStand.Leeg, import, Importdatum);
        var keuzes = ImportKeuzes.Standaard(voorstel) with
        {
            GroepSoorten = new Dictionary<string, GroepSoort>(StringComparer.Ordinal) { ["Loodsen"] = GroepSoort.Overig },
        };

        var stand = VoerUit(ImportStand.Leeg, import, keuzes);

        Assert.Equal(GroepSoort.Overig, Assert.Single(stand.Groepen).Soort);
        Assert.Empty(ImportPlanner.Plan(stand, import, Importdatum).NieuweGroepen);
    }

    [Fact]
    public void Zelfde_iban_is_een_betaler()
    {
        var stand = VoerUit(ImportStand.Leeg, Import(
            Lid("1", iban: "NL91ABNA0417164300"), Lid("2", voornaam: "Bo", iban: "nl91 abna 0417 1643 00")));

        var betaler = Assert.Single(stand.Betalers);
        Assert.All(stand.Koppelingen, k => Assert.Equal(betaler.Id, k.BetalerId));
        Assert.Equal(2, stand.Koppelingen.Count);
    }

    [Fact]
    public void Gewijzigde_iban_koppelt_aan_andere_betaler_lege_iban_laat_koppeling_staan()
    {
        var stand = VoerUit(ImportStand.Leeg, Import(Lid("1", iban: "NL91ABNA0417164300"), Lid("2", voornaam: "Bo", iban: "NL91ABNA0417164300")));

        var import = Import(Lid("1", iban: "BE68539007547034"), Lid("2", voornaam: "Bo"));
        var voorstel = ImportPlanner.Plan(stand, import, Importdatum);
        var regel = Assert.Single(voorstel.Gewijzigd);
        Assert.Equal(new Veldwijziging("IBAN", "NL91 ABNA 0417 1643 00", "BE68 5390 0754 7034"), Assert.Single(regel.Wijzigingen));

        stand = VoerUit(stand, import);
        Assert.Equal(2, stand.Betalers.Count);
        var koppeling1 = stand.Koppelingen.Single(k => k.Lidnummer.Waarde == "1");
        var koppeling2 = stand.Koppelingen.Single(k => k.Lidnummer.Waarde == "2");
        Assert.NotEqual(koppeling1.BetalerId, koppeling2.BetalerId);
        Assert.True(ImportPlanner.Plan(stand, import, Importdatum).IsLeeg);
    }

    [Fact]
    public void Gewijzigde_rekeninghouder_wordt_getoond_en_bijgewerkt()
    {
        var stand = VoerUit(ImportStand.Leeg, Import(Lid("1", iban: "NL91ABNA0417164300", rekeninghouder: "P. de Vries")));
        var import = Import(Lid("1", iban: "NL91ABNA0417164300", rekeninghouder: "P. en Q. de Vries"));

        Assert.Contains(Assert.Single(ImportPlanner.Plan(stand, import, Importdatum).Gewijzigd).Wijzigingen, w => w.Veld == "Rekeninghouder");

        VoerUit(stand, import);
        Assert.Equal("P. en Q. de Vries", stand.Betalers[0].Rekeninghouder);
    }

    [Fact]
    public void Mogelijke_dubbele_persoon_geeft_waarschuwing_maar_wordt_niet_samengevoegd()
    {
        var stand = VoerUit(ImportStand.Leeg, Import(Lid("1")));
        var import = Import(Lid("1"), Lid("2"));

        var voorstel = ImportPlanner.Plan(stand, import, Importdatum);

        Assert.Single(voorstel.Nieuw);
        Assert.Contains(voorstel.Meldingen, m => m.Lidnummer == "2" && m.Tekst.Contains("dubbel", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, VoerUit(stand, import).Personen.Count);
    }

    [Fact]
    public void Meldingen_uit_de_import_worden_doorgegeven()
    {
        var import = new Sol3Import([Lid("1")], [new Importmelding(Sol3Export.LedenBasis, 2, "1", "Test")]);

        Assert.Contains(ImportPlanner.Plan(ImportStand.Leeg, import, Importdatum).Meldingen, m => m.Tekst == "Test");
    }

    [Fact]
    public void Samenvatting_bevat_alleen_aantallen()
    {
        var voorstel = ImportPlanner.Plan(ImportStand.Leeg, Import(Lid("100000001")), Importdatum);

        Assert.DoesNotContain("100000001", voorstel.Samenvatting, StringComparison.Ordinal);
        Assert.DoesNotContain("Anna", voorstel.Samenvatting, StringComparison.Ordinal);
        Assert.Contains("1 nieuw", voorstel.Samenvatting, StringComparison.Ordinal);
    }
}
