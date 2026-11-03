using Verenigd.Application.Webapp;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.Application.Tests.Webapp;

/// <summary>De ledenkopie voor de server (besluiten 35, 38 en 229).</summary>
public class WebappkopieTests
{
    private static readonly DateTime Nu = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Vandaag = new(2026, 10, 31);
    private static readonly Groep Welpen = new("Welpen", GroepSoort.Speltak);
    private static readonly Groep Scouts = new("Scouts", GroepSoort.Speltak);
    private static readonly Groep Bestuur = new("Bestuur", GroepSoort.Bestuur);
    private static readonly Groep[] Groepen = [Welpen, Scouts, Bestuur];

    private static Persoon Persoon(string nummer, string voornaam, params FunctieGegevens[] functies)
    {
        var persoon = new Persoon(Lidnummer.Parse(nummer),
            new LidGegevens(voornaam, null, "Test", new DateOnly(2016, 1, 1), "Man", voornaam + "@example.org", "+31612345678", "003Ab0000012345",
                "Actief", "Dorpsstraat", "1", null, "1234 AB", "Ergens", "Nederland"), Nu);
        persoon.WerkFunctiesBij(functies, Groepen.ToDictionary(g => g.Naam), Vandaag, Nu);
        persoon.WerkVoogdenBij([new VoogdGegevens(1, null, true, "voogd@example.org", null, "+31611111111")], Nu);
        return persoon;
    }

    private static FunctieGegevens Functie(string groep, string rol = "Jeugdlid", Lidtype type = Lidtype.Jeugdlid, DateOnly? eind = null) =>
        new(groep, rol, type, new DateOnly(2024, 9, 1), eind, "Actief");

    [Fact]
    public void Alleen_leden_met_een_actieve_functie_in_een_speltak()
    {
        var welp = Persoon("1", "Welp", Functie("Welpen"));
        var bestuurder = Persoon("2", "Bestuurder", Functie("Bestuur", "Voorzitter", Lidtype.Kaderlid));
        var oudlid = Persoon("3", "Oud", Functie("Scouts", eind: new DateOnly(2026, 6, 30)));
        var uitgeschreven = Persoon("4", "Weg", Functie("Welpen"));
        uitgeschreven.Uitschrijven(Vandaag, Nu);

        var kopie = Webappkopie.Maak([welp, bestuurder, oudlid, uitgeschreven], Groepen, Vandaag);

        Assert.Equal(["Welp"], kopie.Leden.Select(l => l.Voornaam));
        Assert.Equal(["Scouts", "Welpen"], kopie.Speltakken);
    }

    [Fact]
    public void Neemt_vaste_id_contact_voogden_en_functies_mee()
    {
        var leider = Persoon("5", "Akela", Functie("Welpen", "Leiding", Lidtype.Kaderlid), Functie("Scouts"), Functie("Bestuur", "Secretaris", Lidtype.Kaderlid));

        var lid = Assert.Single(Webappkopie.Maak([leider], Groepen, Vandaag).Leden);

        Assert.Equal(leider.Id, lid.Id);
        Assert.Equal("5", lid.Lidnummer);
        Assert.Equal("Akela@example.org", lid.Email);
        Assert.Equal("+31612345678", lid.Telefoon);
        Assert.Equal(new DateOnly(2016, 1, 1), lid.Geboortedatum);
        Assert.Equal(leider.LaatstGewijzigdUtc, lid.LaatstGewijzigdUtc);
        Assert.Equal([new Kopiefunctie("Scouts", "Jeugdlid", false), new Kopiefunctie("Welpen", "Leiding", true)], lid.Functies);
        var voogd = Assert.Single(lid.Voogden);
        Assert.Null(voogd.Naam); // naam onbekend (besluit 31)
        Assert.Equal("+31611111111", voogd.Mobiel);
    }

    [Fact]
    public void De_kopie_bevat_geen_adres_of_financiele_gegevens()
    {
        // Besluiten 35 en 229: de typen van de kopie hebben geen eigenschap voor adres, IBAN, betaler, machtiging of contributie.
        var verboden = new[] { "iban", "betaler", "machtig", "mandaat", "contributie", "bedrag", "straat", "huisnummer", "postcode", "plaats", "adres", "land", "gender", "notitie" };
        var typen = new[] { typeof(Ledenkopie), typeof(Kopielid), typeof(Kopievoogd), typeof(Kopiefunctie) };

        var fout = typen.SelectMany(t => t.GetProperties().Select(p => $"{t.Name}.{p.Name}"))
            .Where(n => verboden.Any(v => n.Contains(v, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(fout);
    }
}
