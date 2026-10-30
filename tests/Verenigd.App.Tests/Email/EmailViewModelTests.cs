using Verenigd.App.Tests.Leden;
using Verenigd.App.ViewModels.Email;
using Verenigd.Application.Email;
using Verenigd.Domain.Communicatie;
using Verenigd.Domain.Leden;

namespace Verenigd.App.Tests.Email;

/// <summary>Pagina E-mail (fase 6, besluiten 191–208): opstellen, verzonden en sjablonen. Adressen zijn verzonnen.</summary>
public sealed class EmailViewModelTests
{
    private readonly FakeDatabaseBeheer _beheer = new();
    private readonly FakeDialoogService _dialogen = new();
    private readonly NepVerzender _verzender = new();

    private async Task<EmailViewModel> MaakAsync(bool gekoppeld = true)
    {
        await LedenTestdata.VulAsync(_beheer.Leden,
            LedenTestdata.Lid("100000001", "Anna", "de Vries", "Welpen", Lidtype.Jeugdlid),
            LedenTestdata.Lid("100000002", "Kees", "Jansen", "Scouts", Lidtype.Kaderlid, email: "kees@example.org", rol: "Leiding"),
            LedenTestdata.Lid("100000003", "Bo", "Bakker", "Scouts", Lidtype.Kaderlid, email: "geen-adres", rol: "Leiding"));
        if (gekoppeld)
        {
            _beheer.Email.Koppel();
        }

        _beheer.Voeg("test.vdb", "geheim");
        var sessie = await _beheer.OpenAsync("test.vdb", "geheim");
        var pagina = new EmailViewModel(sessie, _dialogen, new VasteTijd(new DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero)),
            FakeBoekhouding.Hulpmiddelen(_verzender), () => "Scouting Ergens");
        await pagina.VernieuwCommand.ExecuteAsync(null);
        return pagina;
    }

    [Fact]
    public async Task Laden_toont_sjablonen_groepen_zonder_technische_groep_en_het_logboek()
    {
        var pagina = await MaakAsync();

        Assert.Equal(Standaardsjablonen.Alle().Count, pagina.Sjablonen.Count);
        Assert.Equal(["Scouts", "Welpen"], pagina.Groepen.Select(g => g.Naam));
        Assert.True(pagina.IsGekoppeld);
        Assert.Empty(pagina.Logboek);
    }

    [Fact]
    public async Task Groep_kiezen_bepaalt_de_ontvangers_met_de_standaardkeuze_en_meldt_leden_zonder_adres()
    {
        var pagina = await MaakAsync();

        pagina.Groepen.Single(g => g.Naam == "Welpen").IsGekozen = true;
        Assert.Equal(["voogd@example.org"], pagina.Ontvangers.Select(o => o.Adres)); // jeugdlid: de voogd

        pagina.Groepen.Single(g => g.Naam == "Scouts").IsGekozen = true;
        Assert.Equal(2, pagina.Ontvangers.Count);
        Assert.Contains("Bo", pagina.ZonderAdres, StringComparison.Ordinal);

        pagina.GekozenOntvangerkeuze = EmailViewModel.Ontvangerkeuzes.Single(k => k.Keuze == Ontvangerkeuze.AlleenLeden);
        Assert.Equal(["anna@example.org", "kees@example.org"], pagina.Ontvangers.Select(o => o.Adres).Order());
    }

    [Fact]
    public async Task Sjabloon_kiezen_vult_onderwerp_en_tekst_en_het_voorbeeld_is_per_ontvanger_ingevuld()
    {
        var pagina = await MaakAsync();
        pagina.Groepen.Single(g => g.Naam == "Scouts").IsGekozen = true;

        pagina.GekozenSjabloon = pagina.Sjablonen.Single(s => s.Soort == Emailsoort.Vrij);
        pagina.Onderwerp = "Opkomst {speltak}";
        pagina.Tekst = "Beste {aanhef}, groet van {vereniging}. {onbekend}";
        pagina.GekozenOntvanger = pagina.Ontvangers.Single();

        Assert.Equal("Opkomst Scouts", pagina.VoorbeeldOnderwerp);
        Assert.Equal("Beste Kees, groet van Scouting Ergens. {onbekend}", pagina.VoorbeeldTekst);
        Assert.Contains("{onbekend}", pagina.Waarschuwing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Versturen_vraagt_bevestiging_verstuurt_per_ontvanger_en_toont_mislukte_berichten()
    {
        var pagina = await MaakAsync();
        pagina.Groepen.Single(g => g.Naam == "Welpen").IsGekozen = true;
        pagina.Groepen.Single(g => g.Naam == "Scouts").IsGekozen = true;
        pagina.Onderwerp = "Kamp";
        pagina.Tekst = "Beste {aanhef}";
        _verzender.FoutVoor = "kees@example.org";

        await pagina.VerstuurCommand.ExecuteAsync(null);

        Assert.Contains("2 e-mails", Assert.Single(_dialogen.Bevestigingsvragen), StringComparison.Ordinal);
        Assert.Equal("voogd@example.org", Assert.Single(_verzender.Verstuurd).AanAdres);
        Assert.Equal("kees@example.org", Assert.Single(pagina.Mislukt).Ontvanger.Adres);
        Assert.Equal(2, pagina.Logboek.Count);
        Assert.NotNull(pagina.Foutmelding);
    }

    [Fact]
    public async Task Zonder_bevestiging_of_zonder_koppeling_wordt_niets_verstuurd()
    {
        var pagina = await MaakAsync(gekoppeld: false);
        pagina.Groepen.Single(g => g.Naam == "Welpen").IsGekozen = true;
        pagina.Onderwerp = "Kamp";
        pagina.Tekst = "Tekst";

        Assert.False(pagina.IsGekoppeld);
        Assert.False(pagina.VerstuurCommand.CanExecute(null));

        _beheer.Email.Koppel();
        await pagina.VernieuwCommand.ExecuteAsync(null);
        _dialogen.BevestigAntwoord = false;
        await pagina.VerstuurCommand.ExecuteAsync(null);
        Assert.Empty(_verzender.Verstuurd);
    }

    [Fact]
    public async Task Proef_gaat_naar_het_eigen_adres()
    {
        var pagina = await MaakAsync();
        pagina.Groepen.Single(g => g.Naam == "Welpen").IsGekozen = true;
        pagina.Onderwerp = "Kamp";
        pagina.Tekst = "Beste {aanhef}";

        await pagina.ProefCommand.ExecuteAsync(null);

        var proef = Assert.Single(_verzender.Verstuurd);
        Assert.Equal("penningmeester@example.org", proef.AanAdres);
        Assert.StartsWith("[Proef]", proef.Onderwerp, StringComparison.Ordinal);
        Assert.Empty(pagina.Logboek);
    }

    [Fact]
    public async Task Voorbereid_bericht_gebruikt_de_ontvangers_en_het_sjabloon_van_dat_soort()
    {
        var pagina = await MaakAsync();
        var factuur = new Emailontvanger("Bakkerij", "info@example.org", new Dictionary<string, string> { ["factuurnummer"] = "V2026-001" })
        {
            Bijlagen = [new Emailbijlage("Factuur V2026-001.pdf", [1, 2, 3], "application/pdf")],
        };

        pagina.BereidVoor(new EmailVoorstel(Emailsoort.Factuur, [factuur], "Factuur V2026-001"));

        Assert.Equal(EmailSubtab.Opstellen, pagina.Subtab);
        Assert.True(pagina.IsVoorbereid);
        Assert.Equal(Emailsoort.Factuur, pagina.GekozenSjabloon?.Soort);
        Assert.Equal("info@example.org", Assert.Single(pagina.Ontvangers).Adres);
        Assert.Contains("V2026-001", pagina.VoorbeeldOnderwerp, StringComparison.Ordinal);
        Assert.Equal("Factuur V2026-001.pdf", pagina.VoorbeeldBijlagen);

        pagina.Groepen.Single(g => g.Naam == "Welpen").IsGekozen = true; // groepen tellen niet bij een voorbereid bericht
        Assert.Single(pagina.Ontvangers);

        pagina.WisVoorstelCommand.Execute(null);
        Assert.False(pagina.IsVoorbereid);
        Assert.Equal("voogd@example.org", Assert.Single(pagina.Ontvangers).Adres);
    }

    [Fact]
    public async Task Sjabloon_toevoegen_wijzigen_en_verwijderen_maar_een_standaardsjabloon_blijft()
    {
        var pagina = await MaakAsync();
        pagina.Subtab = EmailSubtab.Sjablonen;

        pagina.NieuwSjabloonCommand.Execute(null);
        pagina.Bewerking!.Naam = "Kamp";
        pagina.Bewerking.Onderwerp = "Kamp 2027";
        pagina.Bewerking.Tekst = "Beste {aanhef}";
        Assert.True(pagina.HeeftOnopgeslagenWijzigingen);
        Assert.True(await pagina.BewaarAsync());
        Assert.Contains(pagina.Sjablonen, s => s.Naam == "Kamp");

        pagina.Beheersjabloon = pagina.Sjablonen.Single(s => s.Naam == "Kamp");
        await pagina.VerwijderSjabloonCommand.ExecuteAsync(null);
        Assert.DoesNotContain(pagina.Sjablonen, s => s.Naam == "Kamp");

        pagina.Beheersjabloon = pagina.Sjablonen.First(s => s.IsStandaard);
        Assert.False(pagina.VerwijderSjabloonCommand.CanExecute(null));
        Assert.NotEmpty(pagina.Bewerking!.Invulvelden);
    }

    [Fact]
    public async Task Leeg_onderwerp_kan_niet_worden_opgeslagen()
    {
        var pagina = await MaakAsync();
        pagina.NieuwSjabloonCommand.Execute(null);
        pagina.Bewerking!.Naam = "Kamp";

        Assert.NotNull(pagina.Bewerking.Fout);
        Assert.False(pagina.KanOpslaan);
    }
}
