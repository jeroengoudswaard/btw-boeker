using Verenigd.Application.Email;
using Verenigd.Application.Tests.Incasso;
using Verenigd.Domain.Communicatie;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.Application.Tests.Email;

/// <summary>E-mail (besluiten 191–195 en 203–207): invullen, ontvangers, berichten en standaardsjablonen.</summary>
public sealed class EmailTests
{
    private static readonly DateTime Nu = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Vandaag = new(2026, 11, 3);
    private readonly Dictionary<string, Groep> _groepen = new(StringComparer.Ordinal)
    {
        ["Welpen"] = new Groep("Welpen", GroepSoort.Speltak),
        ["Leiding"] = new Groep("Leiding", GroepSoort.Overig),
    };

    private Persoon Lid(string nummer, string voornaam, string? email, Lidtype type, params (string Naam, string? Email)[] voogden)
    {
        var p = new Persoon(Lidnummer.Parse(nummer),
            new LidGegevens(voornaam, null, "Vries", new DateOnly(2015, 1, 1), null, email, null, null, "Actief", null, null, null, null, null, null), Nu);
        p.WerkFunctiesBij([new FunctieGegevens(type == Lidtype.Jeugdlid ? "Welpen" : "Leiding", "Lid", type, new DateOnly(2025, 9, 1), null, "Actief")],
            _groepen, Vandaag, Nu);
        p.WerkVoogdenBij([.. voogden.Select((v, i) => new VoogdGegevens(i + 1, v.Naam, v.Naam.StartsWith("Voogd ", StringComparison.Ordinal), v.Email, null, null))], Nu);
        return p;
    }

    [Fact]
    public void Invullen_vervangt_bekende_velden_en_laat_onbekende_staan()
    {
        var tekst = Sjablooninvuller.Vul("Beste {aanhef}, {onbekend} groet {Vereniging}",
            new Dictionary<string, string> { ["aanhef"] = "Anna", ["vereniging"] = "Scouting" });

        Assert.Equal("Beste Anna, {onbekend} groet Scouting", tekst);
        Assert.Equal(["onbekend"], Sjablooninvuller.OnbekendeVelden("{aanhef} {onbekend}", ["aanhef"]));
    }

    [Fact]
    public void Standaard_stuurt_jeugdleden_naar_voogden_en_kaderleden_naar_zichzelf_ontdubbeld()
    {
        var anna = Lid("1", "Anna", "anna@example.org", Lidtype.Jeugdlid, ("P. de Vries", "ouders@example.org"), ("M. de Vries", "OUDERS@example.org "));
        var bo = Lid("2", "Bo", null, Lidtype.Jeugdlid, ("P. de Vries", "ouders@example.org"));
        var kees = Lid("3", "Kees", "kees@example.org", Lidtype.Kaderlid, ("X", "x@example.org"));

        var ontvangers = Ontvangers.VoorLeden([anna, bo, kees], Ontvangerkeuze.Standaard, Vandaag, "Scouting Voorbeeld");

        Assert.Equal(["ouders@example.org", "kees@example.org"], ontvangers.Select(o => o.Adres));
        var ouders = ontvangers[0];
        Assert.Equal("Anna en Bo", ouders.Velden["voornaam"]);
        Assert.Equal("P. de Vries", ouders.Velden["aanhef"]);
        Assert.Equal("Welpen", ouders.Velden["speltak"]);
        Assert.Equal("Kees", ontvangers[1].Velden["aanhef"]);
        Assert.Equal("Scouting Voorbeeld", ontvangers[1].Velden["vereniging"]);
    }

    [Fact]
    public void Keuze_voogden_en_leden_en_uitgeschreven_of_zonder_adres_vallen_af()
    {
        var anna = Lid("1", "Anna", "anna@example.org", Lidtype.Jeugdlid, ("Voogd 1", "ouders@example.org"));
        var zonder = Lid("2", "Bo", "geen adres", Lidtype.Jeugdlid);

        var beide = Ontvangers.VoorLeden([anna, zonder], Ontvangerkeuze.VoogdenEnLeden, Vandaag, "Scouting");
        var leden = Ontvangers.VoorLeden([anna], Ontvangerkeuze.AlleenLeden, Vandaag, "Scouting");

        Assert.Equal(["ouders@example.org", "anna@example.org"], beide.Select(o => o.Adres));
        Assert.Equal("ouder/verzorger van Anna", beide[0].Velden["aanhef"]); // naam onbekend (besluit 31)
        Assert.Equal(["anna@example.org"], leden.Select(o => o.Adres));
        Assert.Single(Ontvangers.ZonderAdres([anna, zonder], Ontvangerkeuze.Standaard, Vandaag));
    }

    [Fact]
    public void Vooraankondiging_per_betaler_met_bedrag_datum_en_mandaat()
    {
        var data = new IncassoTestdata();
        var anna = data.Lid("100", "Anna", new DateOnly(2026, 1, 1));
        anna.WerkVoogdenBij([new VoogdGegevens(1, "P. Test", false, "ouders@example.org", null, null)], Nu);
        var run = data.EerdereRun(new Domain.Financien.Maand(2026, 11), (anna, new Domain.Financien.Maand(2026, 11)));

        var berichten = Berichten.Vooraankondiging(run, data.Stand, Vandaag, "Scouting");

        var bericht = Assert.Single(berichten);
        Assert.Equal("ouders@example.org", bericht.Adres);
        Assert.Equal(run.Posten[0].Bedrag.ToString(), bericht.Velden["bedrag"]);
        Assert.Equal(run.Uitvoerdatum.ToString("dd-MM-yyyy", System.Globalization.CultureInfo.InvariantCulture), bericht.Velden["uitvoerdatum"]);
        Assert.Equal(run.Posten[0].Mandaatkenmerk, bericht.Velden["mandaatkenmerk"]);
    }

    [Fact]
    public void Standaardsjablonen_dekken_elke_soort_en_gebruiken_alleen_bekende_velden()
    {
        Assert.Equal(Enum.GetValues<Emailsoort>().Order(), Standaardsjablonen.Alle().Select(s => s.Soort).Distinct().Order());
        foreach (var sjabloon in Standaardsjablonen.Alle())
        {
            Assert.True(sjabloon.IsStandaard);
            Assert.Empty(Sjablooninvuller.OnbekendeVelden(sjabloon.Onderwerp + sjabloon.Tekst, Invulvelden.Voor(sjabloon.Soort).Select(v => v.Naam).ToList()));
        }
    }

    [Theory]
    [InlineData("""{"installed":{"client_id":"abc.apps.googleusercontent.com","client_secret":"geheim"}}""", true)]
    [InlineData("""{"web":{"client_id":"abc","client_secret":"x"}}""", false)]
    [InlineData("geen json", false)]
    public void Clientbestand_van_google_wordt_gelezen(string json, bool geldig)
    {
        var gelukt = GoogleClientbestand.TryLees(json, out var clientId, out var geheim);

        Assert.Equal(geldig, gelukt);
        if (geldig)
        {
            Assert.Equal("abc.apps.googleusercontent.com", clientId);
            Assert.Equal("geheim", geheim);
        }
    }
}
