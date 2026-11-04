using Verenigd.Application.Import;
using Verenigd.Domain.Leden;
using static Verenigd.Application.Tests.Import.Sol3Voorbeeld;

namespace Verenigd.Application.Tests.Import;

public class Sol3InlezerTests
{
    private const string Een = "100000001";
    private const string Twee = "100000002";

    private static Sol3Import Lees(
        string[][]? leden = null, string[][]? huishouden = null, string[][]? voogden = null, string[][]? functies = null)
    {
        leden ??= [Lid(Een)];
        var tabellen = new Dictionary<Sol3Export, CsvTabel>
        {
            [Sol3Export.LedenBasis] = Tabel(KopLeden, leden),
            [Sol3Export.Huishouden] = Tabel(KopHuishouden, huishouden ?? [.. leden.Select(l => Huishouden(l[0]))]),
            [Sol3Export.Voogden] = Tabel(KopVoogden, voogden ?? []),
            [Sol3Export.Functies] = Tabel(KopFuncties, functies ?? [.. leden.Select(l => Functie(l[0]))]),
        };
        return Sol3Inlezer.Lees(tabellen, new Sol3ImportOpties());
    }

    private static CsvTabel Tabel(string kop, string[][] regels)
    {
        using var stroom = new MemoryStream(Csv(kop, regels));
        return Sol3CsvLezer.Lees(stroom);
    }

    private static ImportLid Enige(Sol3Import import) => Assert.Single(import.Leden);

    [Fact]
    public void Combineert_vier_exports_per_lidnummer()
    {
        var import = Lees(
            huishouden: [Huishouden(Een, iban: "NL91ABNA0417164300", rekeninghouder: "P. de Vries")],
            voogden: [Voogden(Een)],
            functies: [Functie(Een, rol: "Lid", groep: "Welpen")]);

        var lid = Enige(import);
        Assert.Equal(Een, lid.Lidnummer.Waarde);
        Assert.Equal("Anna", lid.Gegevens.Voornaam);
        Assert.Equal(new DateOnly(2015, 3, 1), lid.Gegevens.Geboortedatum);
        Assert.Equal("+31612345678", lid.Gegevens.Telefoon);
        Assert.Equal("Dorpsstraat", lid.Gegevens.Straat);
        Assert.Equal("1234 AB", lid.Gegevens.Postcode);
        Assert.Equal("NL91ABNA0417164300", lid.Iban?.Waarde);
        Assert.Equal("P. de Vries", lid.Rekeninghouder);
        Assert.Equal("P. de Vries", Assert.Single(lid.Voogden).Naam);
        Assert.Equal(new FunctieGegevens("Welpen", "Lid", Lidtype.Jeugdlid, new DateOnly(2025, 9, 19), null, "Actief"), Assert.Single(lid.Functies));
        Assert.Empty(import.Meldingen);
    }

    [Fact]
    public void Identieke_dubbele_regels_worden_zonder_melding_ontdubbeld()
    {
        var import = Lees(leden: [Lid(Een), Lid(Een, eenheid: "Vrijwilligers (Migratie)")]);

        Enige(import);
        Assert.Empty(import.Meldingen);
    }

    [Fact]
    public void Afwijkende_regels_voor_hetzelfde_lid_geven_waarschuwing()
    {
        var import = Lees(leden: [Lid(Een), Lid(Een, email: "ander@example.org")]);

        Assert.Equal("anna@example.org", Enige(import).Gegevens.Email);
        Assert.Contains(import.Meldingen, m => m.Lidnummer == Een && m.Tekst.Contains("eerste regel", StringComparison.Ordinal));
    }

    [Fact]
    public void Onbekende_status_geeft_waarschuwing()
    {
        var import = Lees(leden: [Lid(Een, status: "Opgezegd")]);

        Assert.Contains(import.Meldingen, m => m.Export == Sol3Export.LedenBasis && m.Tekst.Contains("Opgezegd", StringComparison.Ordinal));
    }

    [Fact]
    public void Onleesbare_geboortedatum_geeft_waarschuwing_met_regelnummer()
    {
        var import = Lees(leden: [Lid(Een, geboortedatum: "2015-03-01")]);

        Assert.Null(Enige(import).Gegevens.Geboortedatum);
        var melding = Assert.Single(import.Meldingen);
        Assert.Equal(2, melding.Regel);
        Assert.Contains("Geboortedatum", melding.Tekst, StringComparison.Ordinal);
    }

    [Fact]
    public void Onherkenbaar_telefoonnummer_blijft_ongewijzigd_met_waarschuwing()
    {
        var import = Lees(leden: [Lid(Een, telefoon: "12345")]);

        Assert.Equal("12345", Enige(import).Gegevens.Telefoon);
        Assert.Contains(import.Meldingen, m => m.Tekst.Contains("Telefoon", StringComparison.Ordinal));
    }

    [Fact]
    public void Lege_velden_worden_null()
    {
        var import = Lees(leden: [Lid(Een, tussenvoegsel: "", email: "", telefoon: "")]);

        var gegevens = Enige(import).Gegevens;
        Assert.Null(gegevens.Tussenvoegsel);
        Assert.Null(gegevens.Email);
        Assert.Null(gegevens.Telefoon);
    }

    /// <summary>Besluit 247: de startdatum uit export 1 gaat mee, als terugval voor "Lid sinds".</summary>
    [Fact]
    public void Startdatum_uit_export_1_wordt_ingelezen()
    {
        var import = Lees(leden: [Lid(Een)]);

        Assert.Equal(new DateOnly(2025, 9, 19), Enige(import).Gegevens.Startdatum);
    }

    [Fact]
    public void Ongeldig_lidnummer_wordt_overgeslagen_met_waarschuwing()
    {
        var import = Lees(leden: [Lid(Een), Lid("12A456789")], huishouden: [Huishouden(Een)], functies: [Functie(Een)]);

        Enige(import);
        Assert.Contains(import.Meldingen, m => m.Regel == 3 && m.Tekst.Contains("lidnummer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Contactpersoon_id_niet_1_op_1_geeft_waarschuwing()
    {
        var import = Lees(leden: [Lid(Een, contactpersoonId: "003Ab00000AbCdE"), Lid(Twee, voornaam: "Bo", contactpersoonId: "003Ab00000AbCdE")]);

        Assert.Contains(import.Meldingen, m => m.Tekst.Contains("Contactpersoon-ID", StringComparison.Ordinal));
    }

    [Fact]
    public void Contactpersoon_id_wordt_hoofdlettergevoelig_vergeleken()
    {
        var import = Lees(leden: [Lid(Een, contactpersoonId: "003Ab00000AbCdE"), Lid(Twee, voornaam: "Bo", contactpersoonId: "003AB00000ABCDE")]);

        Assert.DoesNotContain(import.Meldingen, m => m.Tekst.Contains("ook gebruikt", StringComparison.Ordinal));
    }

    [Fact]
    public void Afwijkend_contactpersoon_id_in_export_2_geeft_waarschuwing()
    {
        var import = Lees(huishouden: [Huishouden(Een, contactpersoonId: "003Xx00000XxXxX")]);

        Assert.Contains(import.Meldingen, m => m.Export == Sol3Export.Huishouden && m.Tekst.Contains("Contactpersoon-ID", StringComparison.Ordinal));
    }

    [Fact]
    public void Lidnummer_dat_niet_in_export_1_staat_geeft_waarschuwing()
    {
        var import = Lees(huishouden: [Huishouden(Een), Huishouden(Twee)], functies: [Functie(Een), Functie(Twee)]);

        Enige(import);
        Assert.Contains(import.Meldingen, m => m.Export == Sol3Export.Huishouden && m.Lidnummer == Twee);
        Assert.Contains(import.Meldingen, m => m.Export == Sol3Export.Functies && m.Lidnummer == Twee);
    }

    [Theory]
    [InlineData("ERGENS", "Ergens")]
    [InlineData("'S-GRAVENHAGE", "'s-Gravenhage")]
    [InlineData("den haag", "Den Haag")]
    [InlineData("Den Haag", "Den Haag")]
    [InlineData("'s-Gravenhage", "'s-Gravenhage")]
    public void Plaatsnaam_in_hoofdletters_of_kleine_letters_wordt_genormaliseerd(string invoer, string verwacht)
    {
        var regel = Huishouden(Een);
        regel[6] = invoer;

        Assert.Equal(verwacht, Enige(Lees(huishouden: [regel])).Gegevens.Plaats);
    }

    [Fact]
    public void Onherkenbare_nederlandse_postcode_geeft_waarschuwing_belgische_niet()
    {
        Assert.Contains(Lees(huishouden: [Huishouden(Een, postcode: "123")]).Meldingen, m => m.Tekst.Contains("Postcode", StringComparison.Ordinal));
        Assert.Empty(Lees(huishouden: [Huishouden(Een, postcode: "1000", land: "België")]).Meldingen);
    }

    [Fact]
    public void Iban_met_ongeldig_controlegetal_geeft_waarschuwing()
    {
        var import = Lees(huishouden: [Huishouden(Een, iban: "NL92ABNA0417164300")]);

        Assert.NotNull(Enige(import).Iban);
        Assert.Contains(import.Meldingen, m => m.Tekst.Contains("IBAN", StringComparison.Ordinal));
    }

    [Fact]
    public void Lege_iban_wordt_null()
    {
        Assert.Null(Enige(Lees()).Iban);
    }

    [Theory]
    [InlineData("Voogd 1")]
    [InlineData("Voogd 2")]
    [InlineData("voogd")]
    [InlineData("Voogd Jansen")]
    public void Plaatsvervanger_betekent_naam_onbekend(string naam)
    {
        var voogd = Assert.Single(Enige(Lees(voogden: [Voogden(Een, naam1: naam)])).Voogden);

        Assert.True(voogd.NaamOnbekend);
        Assert.Null(voogd.Naam);
        Assert.Equal("p@example.org", voogd.Email);
    }

    [Fact]
    public void Tweede_voogd_en_telefoonnummers_worden_gelezen()
    {
        var voogden = Enige(Lees(voogden: [Voogden(Een, naam2: "Q. Jansen", email2: "q@example.org")])).Voogden;

        Assert.Equal(2, voogden.Count);
        Assert.Equal("+31701234567", voogden[0].Telefoon);
        Assert.Equal("+31612345678", voogden[0].Mobiel);
        Assert.Equal(new VoogdGegevens(2, "Q. Jansen", false, "q@example.org", null, null), voogden[1]);
    }

    [Fact]
    public void Lege_voogd_wordt_overgeslagen()
    {
        var regel = Voogden(Een, naam1: "", email1: "", naam2: "Q. Jansen");
        regel[3] = "";
        regel[7] = "";

        Assert.Equal(2, Assert.Single(Enige(Lees(voogden: [regel])).Voogden).Volgnummer);
    }

    [Fact]
    public void Dezelfde_functie_zonder_einddatum_wordt_samengevoegd_met_vroegste_startdatum()
    {
        var import = Lees(functies:
        [
            Functie(Een, rol: "Leider", groep: "Junioren", start: "28-01-2022", type: "Kaderlid"),
            Functie(Een, rol: "Leider", groep: "Junioren", start: "14-09-2018", type: "Kaderlid"),
            Functie(Een, rol: "Gegevensbeheerder", groep: "Junioren", start: "28-01-2022", type: "Kaderlid"),
            Functie(Een, rol: "Gegevensbeheerder", groep: "Junioren", start: "28-01-2022", type: "Kaderlid"),
        ]);

        var functies = Enige(import).Functies;
        Assert.Equal(2, functies.Count);
        Assert.Equal(new DateOnly(2018, 9, 14), functies.Single(f => f.Rol == "Leider").Startdatum);
    }

    [Fact]
    public void Functies_met_einddatum_worden_niet_samengevoegd()
    {
        var import = Lees(functies:
        [
            Functie(Een, start: "19-09-2023", eind: "18-09-2024"),
            Functie(Een, start: "19-09-2025"),
        ]);

        Assert.Equal(2, Enige(import).Functies.Count);
    }

    [Fact]
    public void Jeugdlid_en_kaderlid_tegelijk_is_toegestaan()
    {
        var import = Lees(functies:
        [
            Functie(Een, rol: "Teamleider", groep: "Loodsen", type: "Kaderlid"),
            Functie(Een, rol: "Lid", groep: "Loodsen", type: "Jeugdlid"),
        ]);

        Assert.Equal([Lidtype.Kaderlid, Lidtype.Jeugdlid], Enige(import).Functies.Select(f => f.Lidtype));
        Assert.Empty(import.Meldingen);
    }

    [Fact]
    public void Onbekend_lidtype_en_onleesbare_einddatum_geven_waarschuwing()
    {
        var import = Lees(functies: [Functie(Een, type: "Erelid", eind: "morgen")]);

        Assert.Equal(Lidtype.Onbekend, Assert.Single(Enige(import).Functies).Lidtype);
        Assert.Equal(2, import.Meldingen.Count(m => m.Export == Sol3Export.Functies));
    }

    [Fact]
    public void Meldingen_bevatten_nooit_iban_of_e_mailadres()
    {
        var import = Lees(
            leden: [Lid(Een, status: "Raar", telefoon: "12345", email: "geheim@example.org"), Lid(Een, email: "ander@example.org")],
            huishouden: [Huishouden(Een, iban: "NL92ABNA0417164300")]);

        Assert.NotEmpty(import.Meldingen);
        Assert.All(import.Meldingen, m =>
        {
            Assert.DoesNotContain("ABNA", m.Tekst, StringComparison.Ordinal);
            Assert.DoesNotContain("@", m.Tekst, StringComparison.Ordinal);
        });
    }
}
