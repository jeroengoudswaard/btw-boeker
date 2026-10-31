using Verenigd.Application.Administraties;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Application.Email;
using Verenigd.Application.Import;
using Verenigd.Application.Incasso;
using Verenigd.Application.Inrichting;
using Verenigd.Application.Leden;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Communicatie;

namespace Verenigd.Application.Tests.Email;

/// <summary>Versturen (besluiten 192–195): per ontvanger ingevuld, logboek zonder tekst, daglimiet en proef.</summary>
public sealed class EmailDienstTests
{
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private const AdministratieSoort Sti = AdministratieSoort.Stichting;
    private readonly InMemoryEmailOpslag _opslag = new();
    private readonly NepVerzender _verzender = new();
    private readonly VasteTijd _tijd = new(new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero));

    private EmailDienst Dienst => new(new Sessie(_opslag), _verzender, _tijd);

    private static Emailontvanger Ontvanger(string naam, string adres) =>
        new(naam, adres, new Dictionary<string, string> { ["aanhef"] = naam, ["vereniging"] = "Scouting" });

    private void Koppel(Emailinstellingen? instellingen = null, string adres = "penningmeester@example.org")
    {
        instellingen ??= _opslag.Instellingen;
        instellingen.ZetAfzender("Penningmeester", adres);
        instellingen.ZetClient("id", "geheim");
        instellingen.Koppel("sleutel", DateTime.UtcNow);
    }

    [Fact]
    public async Task Laden_vult_de_standaardsjablonen_een_keer_aan()
    {
        await Dienst.LaadAsync();
        var stand = await Dienst.LaadAsync();

        Assert.Equal(Standaardsjablonen.Alle().Count, stand.Sjablonen.Count);
    }

    [Fact]
    public async Task Zonder_koppeling_wordt_niets_verstuurd()
    {
        var fout = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Dienst.VerstuurAsync(Ver, [Ontvanger("Anna", "anna@example.org")], "Hoi", "Beste {aanhef}", "Vrij"));

        Assert.Contains("Instellingen › E-mail", fout.Message, StringComparison.Ordinal);
        Assert.Empty(_verzender.Verstuurd);
    }

    [Fact]
    public async Task Elke_ontvanger_krijgt_een_eigen_ingevulde_mail_en_een_regel_in_het_logboek()
    {
        Koppel();
        _verzender.FoutVoor = "bo@example.org";

        var resultaat = await Dienst.VerstuurAsync(
            Ver, [Ontvanger("Anna", "anna@example.org"), Ontvanger("Bo", "bo@example.org"), Ontvanger("Kees", "kees@example.org")],
            "Kamp {vereniging}", "Beste {aanhef},", "Mededeling speltak");

        Assert.Equal(2, resultaat.Verzonden);
        Assert.Equal("bo@example.org", Assert.Single(resultaat.Mislukt).Ontvanger.Adres);
        Assert.Equal(["Beste Anna,", "Beste Kees,"], _verzender.Verstuurd.Select(b => b.Tekst));
        Assert.All(_verzender.Verstuurd, b => Assert.Equal("Kamp Scouting", b.Onderwerp));
        Assert.Equal(3, _opslag.Logboek.Count);
        Assert.Equal(Verzendstatus.Mislukt, _opslag.Logboek.Single(l => l.Adres == "bo@example.org").Status);
        Assert.All(_opslag.Logboek, l => Assert.Equal("Mededeling speltak", l.Sjabloon));
    }

    [Fact]
    public async Task Daglimiet_stopt_het_versturen_en_meldt_wat_er_niet_verstuurd_is()
    {
        Koppel();
        for (var i = 0; i < EmailDienst.Daglimiet - 1; i++)
        {
            _opslag.Logboek.Add(VerzondenBericht.Gelukt("x", "x@example.org", "o", "s", _tijd.GetUtcNow().UtcDateTime));
        }

        var resultaat = await Dienst.VerstuurAsync(Ver, [Ontvanger("Anna", "anna@example.org"), Ontvanger("Bo", "bo@example.org")], "o", "t", "s");

        Assert.Equal(1, resultaat.Verzonden);
        Assert.True(resultaat.GestoptOpLimiet);
        Assert.Equal(1, resultaat.NietVerstuurd);
    }

    [Fact]
    public async Task Proef_gaat_naar_het_eigen_adres_en_niet_in_het_logboek()
    {
        Koppel();

        await Dienst.ProefAsync(Ver, Ontvanger("Anna", "anna@example.org"), "Kamp", "Beste {aanhef}");

        var bericht = Assert.Single(_verzender.Verstuurd);
        Assert.Equal("penningmeester@example.org", bericht.AanAdres);
        Assert.Equal("[Proef] Kamp", bericht.Onderwerp);
        Assert.Equal("Beste Anna", bericht.Tekst);
        Assert.Empty(_opslag.Logboek);
    }

    [Fact]
    public async Task Koppelen_bewaart_de_vernieuwingssleutel_en_een_standaardsjabloon_blijft_bestaan()
    {
        _opslag.Instellingen.ZetClient("id", "geheim");

        await Dienst.KoppelAsync(Ver, new NepKoppeling());
        var stand = await Dienst.LaadAsync();

        Assert.Equal("vernieuwd", stand.Instellingen(Ver).Vernieuwingssleutel);
        Assert.False(stand.Instellingen(Sti).IsGekoppeld);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Dienst.VerwijderSjabloonAsync(stand.Sjablonen[0]));
    }

    [Fact]
    public async Task Elke_administratie_verstuurt_met_haar_eigen_koppeling_en_daglimiet()
    {
        Koppel(_opslag.Stichting, "penningmeester.stichting@example.org");
        for (var i = 0; i < EmailDienst.Daglimiet; i++)
        {
            _opslag.Logboek.Add(VerzondenBericht.Gelukt("x", "x@example.org", "o", "s", _tijd.GetUtcNow().UtcDateTime)); // vol bij de Vereniging
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => Dienst.VerstuurAsync(Ver, [Ontvanger("Anna", "anna@example.org")], "o", "t", "s"));
        var resultaat = await Dienst.VerstuurAsync(Sti, [Ontvanger("Kerk", "kerk@example.org")], "Factuur", "t", "Factuur");

        Assert.Equal(1, resultaat.Verzonden);
        Assert.Equal("penningmeester.stichting@example.org", Assert.Single(_verzender.Afzenders));
        Assert.Equal(Sti, _opslag.Logboek[^1].Administratie);
        Assert.Equal(1, Dienst.VerzondenVandaag(await Dienst.LaadAsync(), Sti));
    }

    internal sealed class InMemoryEmailOpslag : IEmailOpslag
    {
        private int _volgend = 1;

        public List<Emailsjabloon> Sjablonen { get; } = [];
        public List<VerzondenBericht> Logboek { get; } = [];
        public Emailinstellingen Instellingen { get; set; } = new();
        public Emailinstellingen Stichting { get; set; } = new(AdministratieSoort.Stichting);

        public Task<EmailStand> LaadAsync(CancellationToken annulering = default) =>
            Task.FromResult(new EmailStand([.. Sjablonen], [.. Logboek], [Instellingen, Stichting]));

        public Task BewaarSjabloonAsync(Emailsjabloon sjabloon, CancellationToken annulering = default)
        {
            if (!Sjablonen.Contains(sjabloon))
            {
                typeof(Emailsjabloon).GetProperty(nameof(Emailsjabloon.Id))!.SetValue(sjabloon, _volgend++);
                Sjablonen.Add(sjabloon);
            }

            return Task.CompletedTask;
        }

        public Task VerwijderSjabloonAsync(int id, CancellationToken annulering = default)
        {
            Sjablonen.RemoveAll(s => s.Id == id);
            return Task.CompletedTask;
        }

        public Task BewaarInstellingenAsync(Emailinstellingen instellingen, CancellationToken annulering = default)
        {
            if (instellingen.Administratie == AdministratieSoort.Stichting)
            {
                Stichting = instellingen;
            }
            else
            {
                Instellingen = instellingen;
            }

            return Task.CompletedTask;
        }

        public Task LogAsync(VerzondenBericht bericht, CancellationToken annulering = default)
        {
            Logboek.Add(bericht);
            return Task.CompletedTask;
        }
    }

    internal sealed class NepVerzender : IEmailVerzender
    {
        public List<Emailbericht> Verstuurd { get; } = [];
        public List<string?> Afzenders { get; } = [];
        public string? FoutVoor { get; set; }

        public Task VerstuurAsync(Emailinstellingen instellingen, Emailbericht bericht, CancellationToken annulering = default)
        {
            Afzenders.Add(instellingen.Afzenderadres);
            if (bericht.AanAdres == FoutVoor)
            {
                throw new EmailVerzendException("Het adres werd geweigerd.");
            }

            Verstuurd.Add(bericht);
            return Task.CompletedTask;
        }
    }

    private sealed class NepKoppeling : IGoogleKoppeling
    {
        public Task<string> KoppelAsync(string clientId, string clientGeheim, CancellationToken annulering = default) => Task.FromResult("vernieuwd");
    }

    private sealed class VasteTijd(DateTimeOffset nu) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nu;
    }

    private sealed class Sessie(IEmailOpslag email) : IDatabaseSessie
    {
        public string Pad => "test.vdb";
        public IAdministratieRepository Administraties => throw new NotSupportedException();
        public ILedenRepository Leden => throw new NotSupportedException();
        public ILedenImportOpslag LedenImport => throw new NotSupportedException();
        public IContributieRepository Contributie => throw new NotSupportedException();
        public IBoekhoudingRepository Boekhouding => throw new NotSupportedException();
        public IStartwizardOpslag Startwizard => throw new NotSupportedException();
        public IEmailOpslag Email { get; } = email;

        public Task MaakBackupAsync(string doelPad, CancellationToken annulering = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
