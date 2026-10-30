using System.Net;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using MimeKit;
using Verenigd.Application.Email;
using Verenigd.Domain.Communicatie;

namespace Verenigd.Infrastructure.Email;

/// <summary>Bouwt het MIME-bericht: platte tekst (UTF-8) met eventuele bijlagen, zoals een PDF-factuur.</summary>
public static class MimeBouwer
{
    public static MimeMessage Maak(Emailinstellingen instellingen, Emailbericht bericht)
    {
        ArgumentNullException.ThrowIfNull(instellingen);
        ArgumentNullException.ThrowIfNull(bericht);
        var afzender = instellingen.Afzenderadres ?? throw new InvalidOperationException("Er is geen afzenderadres ingesteld.");
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(instellingen.Afzendernaam ?? afzender, afzender));
        mime.To.Add(new MailboxAddress(bericht.AanNaam, bericht.AanAdres));
        mime.Subject = bericht.Onderwerp;
        var opbouw = new BodyBuilder { TextBody = bericht.Tekst };
        foreach (var bijlage in bericht.Bijlagen)
        {
            opbouw.Attachments.Add(bijlage.Bestandsnaam, bijlage.Inhoud, ContentType.Parse(bijlage.Mediatype));
        }

        mime.Body = opbouw.ToMessageBody();
        return mime;
    }

    /// <summary>Base64url zonder opvulling, zoals de Gmail-API het veld <c>raw</c> verwacht.</summary>
    public static string Raw(MimeMessage mime)
    {
        ArgumentNullException.ThrowIfNull(mime);
        using var stroom = new MemoryStream();
        mime.WriteTo(stroom);
        return Convert.ToBase64String(stroom.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

/// <summary>Verstuurt via de Gmail-API met alleen de toestemming "versturen" (besluit 192).</summary>
public sealed class GmailVerzender : IEmailVerzender
{
    public async Task VerstuurAsync(Emailinstellingen instellingen, Emailbericht bericht, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(instellingen);
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = instellingen.ClientId, ClientSecret = instellingen.ClientGeheim },
            Scopes = [GmailService.Scope.GmailSend],
            DataStore = new NullDataStore(),
        });
        var credential = new UserCredential(flow, "verenigd", new TokenResponse { RefreshToken = instellingen.Vernieuwingssleutel });
        using var gmail = new GmailService(new BaseClientService.Initializer { HttpClientInitializer = credential, ApplicationName = "Verenigd" });
        try
        {
            await gmail.Users.Messages.Send(new Message { Raw = MimeBouwer.Raw(MimeBouwer.Maak(instellingen, bericht)) }, "me").ExecuteAsync(annulering).ConfigureAwait(false);
        }
        catch (TokenResponseException ex)
        {
            throw new EmailVerzendException("De koppeling met Google is verlopen of ingetrokken; koppel opnieuw bij Instellingen › E-mail.", ex);
        }
        catch (GoogleApiException ex)
        {
            throw new EmailVerzendException(ex.HttpStatusCode switch
            {
                HttpStatusCode.TooManyRequests => "Gmail laat nu geen berichten meer toe (limiet bereikt); probeer het later opnieuw.",
                HttpStatusCode.BadRequest => "Gmail weigerde het bericht, bijvoorbeeld door een ongeldig adres.",
                HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => "Geen toestemming om te versturen; koppel opnieuw bij Instellingen › E-mail.",
                _ => "Gmail gaf een fout bij het versturen.",
            }, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new EmailVerzendException("Geen verbinding met Google; controleer de internetverbinding.", ex);
        }
    }
}

/// <summary>
/// De eenmalige toestemming in de browser (besluit 192). <c>prompt=consent</c> zorgt dat Google ook bij opnieuw koppelen een
/// vernieuwingssleutel geeft; niets wordt op schijf bewaard (de sleutel gaat versleuteld de database in).
/// </summary>
public sealed class GoogleKoppeling : IGoogleKoppeling
{
    public async Task<string> KoppelAsync(string clientId, string clientGeheim, CancellationToken annulering = default)
    {
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientGeheim },
            Scopes = [GmailService.Scope.GmailSend],
            DataStore = new NullDataStore(),
            Prompt = "consent",
        });
        var credential = await new AuthorizationCodeInstalledApp(flow, new LocalServerCodeReceiver()).AuthorizeAsync("verenigd", annulering).ConfigureAwait(false);
        return credential.Token.RefreshToken
            ?? throw new InvalidOperationException("Google gaf geen vernieuwingssleutel. Trek de toegang in bij myaccount.google.com en koppel opnieuw.");
    }
}
