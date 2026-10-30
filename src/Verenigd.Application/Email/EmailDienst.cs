using Verenigd.Application.Database;
using Verenigd.Domain.Communicatie;

namespace Verenigd.Application.Email;

public sealed record EmailStand(IReadOnlyList<Emailsjabloon> Sjablonen, IReadOnlyList<VerzondenBericht> Logboek, Emailinstellingen Instellingen);

/// <summary>Sjablonen, logboek en instellingen van e-mail, in de versleutelde database.</summary>
public interface IEmailOpslag
{
    Task<EmailStand> LaadAsync(CancellationToken annulering = default);

    /// <summary>Voegt toe (Id 0) of werkt bij.</summary>
    Task BewaarSjabloonAsync(Emailsjabloon sjabloon, CancellationToken annulering = default);

    Task VerwijderSjabloonAsync(int id, CancellationToken annulering = default);

    Task BewaarInstellingenAsync(Emailinstellingen instellingen, CancellationToken annulering = default);

    Task LogAsync(VerzondenBericht bericht, CancellationToken annulering = default);
}

public sealed record Mislukking(Emailontvanger Ontvanger, string Fout);

/// <param name="NietVerstuurd">Niet verstuurd omdat de daglimiet bereikt is (besluit 193).</param>
public sealed record Verzendresultaat(int Verzonden, IReadOnlyList<Mislukking> Mislukt, int NietVerstuurd)
{
    public bool GestoptOpLimiet => NietVerstuurd > 0;
}

/// <summary>
/// Versturen (besluiten 191–195): per ontvanger een eigen, ingevulde e-mail; een mislukt bericht houdt de rest niet tegen;
/// het logboek bewaart aan wie, wanneer, onderwerp en sjabloon; stoppen bij de daglimiet van Gmail.
/// </summary>
public sealed class EmailDienst(IDatabaseSessie sessie, IEmailVerzender verzender, TimeProvider tijd)
{
    /// <summary>Besluit 193: een gmail.com-account mag ongeveer 500 berichten per dag versturen.</summary>
    public const int Daglimiet = 500;

    private DateTime NuUtc => tijd.GetUtcNow().UtcDateTime;

    /// <summary>Laadt alles en vult ontbrekende standaardsjablonen aan.</summary>
    public async Task<EmailStand> LaadAsync(CancellationToken annulering = default)
    {
        var stand = await sessie.Email.LaadAsync(annulering).ConfigureAwait(false);
        var ontbrekend = Standaardsjablonen.Alle().Where(s => !stand.Sjablonen.Any(b => b.IsStandaard && b.Soort == s.Soort)).ToList();
        if (ontbrekend.Count == 0)
        {
            return stand;
        }

        foreach (var sjabloon in ontbrekend)
        {
            await sessie.Email.BewaarSjabloonAsync(sjabloon, annulering).ConfigureAwait(false);
        }

        return await sessie.Email.LaadAsync(annulering).ConfigureAwait(false);
    }

    public int VerzondenVandaag(EmailStand stand)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var vandaag = NuUtc.Date;
        return stand.Logboek.Count(b => b.Status == Verzendstatus.Verzonden && b.TijdstipUtc.Date == vandaag);
    }

    public async Task<Verzendresultaat> VerstuurAsync(
        IReadOnlyList<Emailontvanger> ontvangers, string onderwerp, string tekst, string sjabloonnaam, IProgress<int>? voortgang = null,
        CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(ontvangers);
        var stand = await LaadAsync(annulering).ConfigureAwait(false);
        var instellingen = Gekoppeld(stand);
        var ruimte = Math.Max(0, Daglimiet - VerzondenVandaag(stand));
        var verzonden = 0;
        var mislukt = new List<Mislukking>();
        for (var i = 0; i < ontvangers.Count && i < ruimte; i++)
        {
            annulering.ThrowIfCancellationRequested();
            var ontvanger = ontvangers[i];
            var bericht = Maak(ontvanger, onderwerp, tekst);
            try
            {
                await verzender.VerstuurAsync(instellingen, bericht, annulering).ConfigureAwait(false);
                await sessie.Email.LogAsync(VerzondenBericht.Gelukt(ontvanger.Naam, ontvanger.Adres, bericht.Onderwerp, sjabloonnaam, NuUtc), annulering).ConfigureAwait(false);
                verzonden++;
            }
            catch (EmailVerzendException ex)
            {
                await sessie.Email.LogAsync(VerzondenBericht.Mislukt(ontvanger.Naam, ontvanger.Adres, bericht.Onderwerp, sjabloonnaam, ex.Message, NuUtc), annulering).ConfigureAwait(false);
                mislukt.Add(new Mislukking(ontvanger, ex.Message));
            }

            voortgang?.Report(i + 1);
        }

        return new Verzendresultaat(verzonden, mislukt, Math.Max(0, ontvangers.Count - ruimte));
    }

    /// <summary>Het bericht zoals <paramref name="voorbeeld"/> het krijgt, naar het eigen adres; niet in het logboek.</summary>
    public async Task ProefAsync(Emailontvanger voorbeeld, string onderwerp, string tekst, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(voorbeeld);
        var instellingen = Gekoppeld(await LaadAsync(annulering).ConfigureAwait(false));
        var bericht = Maak(voorbeeld, onderwerp, tekst);
        await verzender.VerstuurAsync(instellingen,
            bericht with { AanNaam = instellingen.Afzendernaam ?? instellingen.Afzenderadres!, AanAdres = instellingen.Afzenderadres!, Onderwerp = "[Proef] " + bericht.Onderwerp },
            annulering).ConfigureAwait(false);
    }

    public static Emailbericht Maak(Emailontvanger ontvanger, string onderwerp, string tekst)
    {
        ArgumentNullException.ThrowIfNull(ontvanger);
        return new Emailbericht(ontvanger.Naam, ontvanger.Adres, Sjablooninvuller.Vul(onderwerp, ontvanger.Velden), Sjablooninvuller.Vul(tekst, ontvanger.Velden), ontvanger.Bijlagen);
    }

    public Task BewaarSjabloonAsync(Emailsjabloon sjabloon, CancellationToken annulering = default) => sessie.Email.BewaarSjabloonAsync(sjabloon, annulering);

    public async Task VerwijderSjabloonAsync(Emailsjabloon sjabloon, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(sjabloon);
        if (sjabloon.IsStandaard)
        {
            throw new InvalidOperationException("Een standaardsjabloon kan niet worden verwijderd; u kunt het wel aanpassen.");
        }

        await sessie.Email.VerwijderSjabloonAsync(sjabloon.Id, annulering).ConfigureAwait(false);
    }

    public Task BewaarInstellingenAsync(Emailinstellingen instellingen, CancellationToken annulering = default) =>
        sessie.Email.BewaarInstellingenAsync(instellingen, annulering);

    /// <summary>De eenmalige toestemming in de browser (besluit 192); bewaart de vernieuwingssleutel versleuteld.</summary>
    public async Task KoppelAsync(IGoogleKoppeling koppeling, CancellationToken annulering = default)
    {
        ArgumentNullException.ThrowIfNull(koppeling);
        var stand = await LaadAsync(annulering).ConfigureAwait(false);
        var instellingen = stand.Instellingen;
        if (!instellingen.HeeftClient)
        {
            throw new InvalidOperationException("Lees eerst het clientbestand van Google Cloud in.");
        }

        var sleutel = await koppeling.KoppelAsync(instellingen.ClientId!, instellingen.ClientGeheim!, annulering).ConfigureAwait(false);
        instellingen.Koppel(sleutel, NuUtc);
        await sessie.Email.BewaarInstellingenAsync(instellingen, annulering).ConfigureAwait(false);
    }

    private static Emailinstellingen Gekoppeld(EmailStand stand) =>
        stand.Instellingen is { IsGekoppeld: true, Afzenderadres: not null } instellingen
            ? instellingen
            : throw new InvalidOperationException("E-mail is nog niet gekoppeld: ga naar Instellingen › E-mail.");
}

/// <summary>De ingebouwde sjablonen (besluit 191), aan te passen door de gebruiker.</summary>
public static class Standaardsjablonen
{
    private const string Groet = "\n\nMet vriendelijke groet,\n{vereniging}";

    public static IReadOnlyList<Emailsjabloon> Alle() =>
    [
        Emailsjabloon.Standaard(Emailsoort.Vrij, "Mededeling speltak", "Bericht van {vereniging}",
            "Beste {aanhef},\n\n" + Groet.TrimStart('\n')),
        Emailsjabloon.Standaard(Emailsoort.Welkom, "Welkomstmail", "Welkom bij {vereniging}",
            "Beste {aanhef},\n\nWelkom bij {vereniging}! {voornaam} is ingeschreven bij de {speltak}. We kijken uit naar de eerste opkomst."
            + "\n\nDe contributie wordt maandelijks geïncasseerd; u krijgt vooraf een aankondiging. Heeft u vragen, beantwoord dan deze e-mail." + Groet),
        Emailsjabloon.Standaard(Emailsoort.Vooraankondiging, "Vooraankondiging incasso", "Aankondiging incasso contributie {vereniging}",
            "Beste {aanhef},\n\nOp {uitvoerdatum} schrijven wij {bedrag} af voor de contributie van {voornaam}, met de omschrijving \"{omschrijving}\"."
            + "\n\nKenmerk machtiging: {mandaatkenmerk}\nIncassant-ID: {incassantid}\n\nZorgt u voor voldoende saldo? Heeft u vragen, beantwoord dan deze e-mail." + Groet),
        Emailsjabloon.Standaard(Emailsoort.Stornering, "Bericht bij stornering", "Contributie niet ontvangen",
            "Beste {aanhef},\n\nDe incasso van {bedrag} voor de contributie van {voornaam} (\"{omschrijving}\") is teruggeboekt. Reden van de bank: {reden}."
            + "\n\nWe incasseren het bedrag opnieuw bij de volgende run. Wilt u liever zelf overmaken of klopt er iets niet, beantwoord dan deze e-mail." + Groet),
        Emailsjabloon.Standaard(Emailsoort.Factuur, "Factuur", "Factuur {factuurnummer} van {vereniging}",
            "Beste {aanhef},\n\nIn de bijlage vindt u factuur {factuurnummer} van {factuurdatum} voor een bedrag van {bedrag}."
            + " Wij verzoeken u dit bedrag uiterlijk {vervaldatum} over te maken; de betaalgegevens en een QR-code staan op de factuur." + Groet),
        Emailsjabloon.Standaard(Emailsoort.Herinnering, "Herinnering factuur", "Herinnering: factuur {factuurnummer}",
            "Beste {aanhef},\n\nVolgens onze administratie is factuur {factuurnummer} van {factuurdatum} ({bedrag}, te betalen uiterlijk {vervaldatum}) nog niet betaald."
            + " Wilt u het bedrag zo spoedig mogelijk overmaken? Heeft u al betaald, dan kunt u deze herinnering als niet verzonden beschouwen." + Groet),
    ];
}
