using System.Text;
using Verenigd.Application.Email;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Communicatie;
using Verenigd.Infrastructure.Database;
using Verenigd.Infrastructure.Email;

namespace Verenigd.Infrastructure.Tests.Email;

/// <summary>E-mail in de echte, versleutelde database en de MIME-opbouw voor Gmail (fase 6). Adressen zijn verzonnen.</summary>
public sealed class EmailOpslagTests : IDisposable
{
    private const string Wachtwoord = "scouting is fantastisch";
    private readonly TijdelijkeMap _map = new();
    private readonly DatabaseBeheer _beheer = new();

    public void Dispose() => _map.Dispose();

    [Fact]
    public async Task Sjablonen_logboek_en_instellingen_blijven_bewaard()
    {
        var pad = _map.Bestand("test.vdb");
        await _beheer.MaakAanAsync(pad, Wachtwoord);
        await using (var sessie = await _beheer.OpenAsync(pad, Wachtwoord))
        {
            var dienst = new EmailDienst(sessie, new GmailVerzender(), TimeProvider.System);
            var stand = await dienst.LaadAsync(); // vult de standaardsjablonen aan
            Assert.Equal(Standaardsjablonen.Alle().Count, stand.Sjablonen.Count);

            var eigen = new Emailsjabloon(Emailsoort.Vrij, "Kamp", "Kamp 2027", "Beste {aanhef}");
            await dienst.BewaarSjabloonAsync(eigen);
            eigen.Wijzig("Kamp", "Kamp 2027: aanmelden", "Beste {aanhef}");
            await dienst.BewaarSjabloonAsync(eigen);

            var instellingen = stand.Instellingen(AdministratieSoort.Vereniging);
            instellingen.ZetAfzender("Penningmeester", "penningmeester@example.org");
            instellingen.ZetClient("id.apps.googleusercontent.com", "geheim");
            instellingen.Koppel("vernieuwingssleutel", DateTime.UtcNow);
            await dienst.BewaarInstellingenAsync(instellingen);
            await sessie.Email.LogAsync(VerzondenBericht.Gelukt("Anna", "anna@example.org", "Kamp", "Kamp", DateTime.UtcNow));

            var stichting = stand.Instellingen(AdministratieSoort.Stichting);
            stichting.ZetAfzender("Penningmeester Stichting", "stichting@example.org");
            await dienst.BewaarInstellingenAsync(stichting);
            await sessie.Email.LogAsync(VerzondenBericht.Gelukt("Kerk", "kerk@example.org", "Factuur", "Factuur", DateTime.UtcNow, AdministratieSoort.Stichting));
        }

        await using (var sessie = await _beheer.OpenAsync(pad, Wachtwoord))
        {
            var stand = await sessie.Email.LaadAsync();
            Assert.Equal("Kamp 2027: aanmelden", stand.Sjablonen.Single(s => s.Naam == "Kamp").Onderwerp);
            Assert.True(stand.Instellingen(AdministratieSoort.Vereniging).IsGekoppeld);
            Assert.Equal("penningmeester@example.org", stand.Instellingen(AdministratieSoort.Vereniging).Afzenderadres);
            Assert.Equal("stichting@example.org", stand.Instellingen(AdministratieSoort.Stichting).Afzenderadres);
            Assert.False(stand.Instellingen(AdministratieSoort.Stichting).IsGekoppeld);
            Assert.Equal(AdministratieSoort.Vereniging, stand.Logboek.Single(b => b.Adres == "anna@example.org").Administratie);
            Assert.Equal(AdministratieSoort.Stichting, stand.Logboek.Single(b => b.Adres == "kerk@example.org").Administratie);

            await sessie.Email.VerwijderSjabloonAsync(stand.Sjablonen.First(s => s.IsStandaard).Id); // standaard blijft staan
            Assert.Equal(stand.Sjablonen.Count, (await sessie.Email.LaadAsync()).Sjablonen.Count);
        }
    }

    [Fact]
    public void Mime_bericht_heeft_afzender_ontvanger_tekst_en_pdf_bijlage_en_base64url()
    {
        var instellingen = new Emailinstellingen();
        instellingen.ZetAfzender("Penningmeester Scouting", "penningmeester@example.org");
        var bericht = new Emailbericht("Anna de Vries", "anna@example.org", "Factuur V2026-001", "Beste Anna, één bijlage.",
            [new Emailbijlage("Factuur V2026-001.pdf", Encoding.ASCII.GetBytes("%PDF-1.7"), "application/pdf")]);

        var mime = MimeBouwer.Maak(instellingen, bericht);
        var raw = MimeBouwer.Raw(mime);

        Assert.Equal("penningmeester@example.org", mime.From.Mailboxes.Single()!.Address);
        Assert.Equal("anna@example.org", mime.To.Mailboxes.Single()!.Address);
        Assert.Equal("Factuur V2026-001", mime.Subject);
        Assert.Equal("Beste Anna, één bijlage.", mime.TextBody?.Trim());
        Assert.Equal("Factuur V2026-001.pdf", mime.Attachments.Single().ContentDisposition?.FileName);
        Assert.DoesNotContain('+', raw);
        Assert.DoesNotContain('/', raw);
        Assert.DoesNotContain('=', raw);
    }
}
