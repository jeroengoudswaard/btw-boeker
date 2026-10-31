using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Verenigd.Application.Debiteuren;
using Verenigd.Application.Incasso;
using Verenigd.Domain.Communicatie;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Facturen;
using Verenigd.Domain.Financien.Relaties;
using Verenigd.Domain.Leden;

namespace Verenigd.Application.Email;

/// <summary>Eén ontvanger met de waarden van de invulvelden (besluit 194: elke ontvanger een eigen, ingevulde e-mail).</summary>
public sealed record Emailontvanger(string Naam, string Adres, IReadOnlyDictionary<string, string> Velden)
{
    public IReadOnlyList<Emailbijlage> Bijlagen { get; init; } = [];
}

public sealed record Emailbijlage(string Bestandsnaam, byte[] Inhoud, string Mediatype);

public sealed record Emailbericht(string AanNaam, string AanAdres, string Onderwerp, string Tekst, IReadOnlyList<Emailbijlage> Bijlagen);

/// <summary>Verstuurt één bericht via het gekoppelde account (Gmail-API, besluit 192). In tests een nep-verzender.</summary>
public interface IEmailVerzender
{
    Task VerstuurAsync(Emailinstellingen instellingen, Emailbericht bericht, CancellationToken annulering = default);
}

/// <summary>De eenmalige toestemming in de browser; geeft de vernieuwingssleutel terug.</summary>
public interface IGoogleKoppeling
{
    Task<string> KoppelAsync(string clientId, string clientGeheim, CancellationToken annulering = default);
}

/// <summary>Een verzendfout die de gebruiker kan begrijpen (zonder adressen of tekst).</summary>
public sealed class EmailVerzendException(string melding, Exception? oorzaak = null) : Exception(melding, oorzaak);

/// <summary>Besluit 203: welke adressen bij leden.</summary>
public enum Ontvangerkeuze
{
    /// <summary>Voogden bij leden met een actieve jeugdlidfunctie, anders het lid zelf.</summary>
    Standaard,
    AlleenVoogden,
    AlleenLeden,
    VoogdenEnLeden,
}

public sealed record Invulveld(string Naam, string Uitleg);

/// <summary>De invulvelden per soort sjabloon; de editor toont ze en een test bewaakt de standaardsjablonen.</summary>
public static class Invulvelden
{
    private static readonly Invulveld[] Algemeen =
    [
        new("aanhef", "Naam van de ontvanger, bijvoorbeeld de voogd of het lid"),
        new("voornaam", "Voornaam van het lid (of de leden, bij één adres voor meer leden)"),
        new("speltak", "Speltak van het lid"),
        new("vereniging", "Naam van de Vereniging"),
    ];

    public static IReadOnlyList<Invulveld> Voor(Emailsoort soort) => soort switch
    {
        Emailsoort.Vooraankondiging =>
        [
            .. Algemeen, new("bedrag", "Bedrag van de incasso"), new("uitvoerdatum", "Datum waarop de bank afschrijft"),
            new("mandaatkenmerk", "Kenmerk van de machtiging"), new("incassantid", "Incassant-ID van de Vereniging"), new("omschrijving", "Omschrijving op het bankafschrift"),
        ],
        Emailsoort.Stornering => [.. Algemeen, new("bedrag", "Teruggeboekt bedrag"), new("reden", "Reden van de bank"), new("omschrijving", "Omschrijving van de incasso")],
        Emailsoort.Factuur or Emailsoort.Herinnering =>
        [
            new("aanhef", "Contactpersoon of naam van de relatie"), new("vereniging", "Naam van de Vereniging"), new("factuurnummer", "Nummer van de factuur"),
            new("bedrag", "Bedrag van de factuur"), new("vervaldatum", "Uiterste betaaldatum"), new("factuurdatum", "Datum van de factuur"),
        ],
        _ => Algemeen,
    };
}

/// <summary>Vult <c>{veld}</c> in (hoofdletterongevoelig); onbekende velden blijven zichtbaar staan.</summary>
public static partial class Sjablooninvuller
{
    [GeneratedRegex(@"\{([A-Za-z]+)\}")]
    private static partial Regex Veld();

    public static string Vul(string tekst, IReadOnlyDictionary<string, string> velden)
    {
        ArgumentNullException.ThrowIfNull(tekst);
        ArgumentNullException.ThrowIfNull(velden);
        var zoek = velden.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        return Veld().Replace(tekst, m => zoek.TryGetValue(m.Groups[1].Value, out var waarde) ? waarde : m.Value);
    }

    public static IReadOnlyList<string> OnbekendeVelden(string tekst, IReadOnlyCollection<string> bekend)
    {
        ArgumentNullException.ThrowIfNull(tekst);
        return [.. Veld().Matches(tekst).Select(m => m.Groups[1].Value).Where(v => !bekend.Contains(v, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase)];
    }
}

/// <summary>Kiest de adressen bij leden (besluiten 31, 203 en 204) en ontdubbelt op e-mailadres.</summary>
public static class Ontvangers
{
    public static IReadOnlyList<Emailontvanger> VoorLeden(IEnumerable<Persoon> leden, Ontvangerkeuze keuze, DateOnly vandaag, string vereniging) =>
        Ontdubbel(Kandidaten(leden, keuze, vandaag).Where(k => Emailadres.IsGeldig(k.Adres)), vereniging);

    /// <summary>Leden waarvoor geen enkel geldig adres is gevonden (om te melden).</summary>
    public static IReadOnlyList<Persoon> ZonderAdres(IEnumerable<Persoon> leden, Ontvangerkeuze keuze, DateOnly vandaag) =>
        [.. leden.Where(l => !l.IsUitgeschreven && !Kandidaten([l], keuze, vandaag).Any(k => Emailadres.IsGeldig(k.Adres)))];

    /// <summary>Besluit 204: de leden die onder een betaler vallen, met de standaardkeuze.</summary>
    public static IReadOnlyList<Persoon> LedenVanBetaler(IncassoStand stand, Guid betalerId)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var lidnummers = stand.Koppelingen.Where(k => k.BetalerId == betalerId).Select(k => k.Lidnummer).ToHashSet();
        return [.. stand.Personen.Where(p => lidnummers.Contains(p.Lidnummer))];
    }

    private static IEnumerable<(string Naam, string Adres, Persoon Lid)> Kandidaten(IEnumerable<Persoon> leden, Ontvangerkeuze keuze, DateOnly vandaag)
    {
        foreach (var lid in leden.Where(l => !l.IsUitgeschreven))
        {
            var jeugdlid = lid.Functies.Any(f => f.Lidtype == Lidtype.Jeugdlid && f.IsActief(vandaag));
            var voogden = keuze is Ontvangerkeuze.AlleenVoogden or Ontvangerkeuze.VoogdenEnLeden || (keuze == Ontvangerkeuze.Standaard && jeugdlid);
            var zelf = keuze is Ontvangerkeuze.AlleenLeden or Ontvangerkeuze.VoogdenEnLeden || (keuze == Ontvangerkeuze.Standaard && !jeugdlid);
            if (voogden)
            {
                foreach (var voogd in lid.Voogden)
                {
                    yield return (voogd.NaamOnbekend || string.IsNullOrWhiteSpace(voogd.Naam) ? $"ouder/verzorger van {lid.Voornaam}" : voogd.Naam!, voogd.Email ?? string.Empty, lid);
                }
            }

            if (zelf)
            {
                yield return (lid.Voornaam, lid.Email ?? string.Empty, lid);
            }
        }
    }

    private static List<Emailontvanger> Ontdubbel(IEnumerable<(string Naam, string Adres, Persoon Lid)> kandidaten, string vereniging) =>
        [.. kandidaten
            .GroupBy(k => Emailadres.Sleutel(k.Adres))
            .Select(g =>
            {
                var leden = g.Select(k => k.Lid).DistinctBy(l => l.Id).ToList();
                var eerste = g.First();
                var velden = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["aanhef"] = eerste.Naam,
                    ["voornaam"] = Opsomming(leden.Select(l => l.Voornaam)),
                    ["speltak"] = Opsomming(leden.SelectMany(Speltakken).Distinct(StringComparer.Ordinal)),
                    ["vereniging"] = vereniging,
                };
                return new Emailontvanger(eerste.Naam, eerste.Adres.Trim(), velden);
            })];

    private static IEnumerable<string> Speltakken(Persoon lid) =>
        lid.Functies.Where(f => f.Einddatum is null && f.Groep.Soort == GroepSoort.Speltak).Select(f => f.Groep.Naam);

    /// <summary>"Anna", "Anna en Bo", "Anna, Bo en Kees".</summary>
    public static string Opsomming(IEnumerable<string> namen)
    {
        var lijst = namen.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        return lijst.Count <= 1 ? string.Concat(lijst) : $"{string.Join(", ", lijst.Take(lijst.Count - 1))} en {lijst[^1]}";
    }
}

/// <summary>Berichten vanuit de modules (besluiten 204–207): ontvangers met de velden van dat soort bericht.</summary>
public static class Berichten
{
    private static string Datum(DateOnly datum) => datum.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    /// <summary>Besluit 206: per post in de run de betaler, met bedrag, uitvoerdatum en mandaatkenmerk.</summary>
    public static IReadOnlyList<Emailontvanger> Vooraankondiging(Incassorun run, IncassoStand stand, DateOnly vandaag, string vereniging)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(stand);
        return [.. run.Posten.SelectMany(post =>
            Ontvangers.VoorLeden(Ontvangers.LedenVanBetaler(stand, post.BetalerId), Ontvangerkeuze.Standaard, vandaag, vereniging)
                .Select(o => MetVelden(o, new()
                {
                    ["bedrag"] = post.Bedrag.ToString(),
                    ["uitvoerdatum"] = Datum(run.Uitvoerdatum),
                    ["mandaatkenmerk"] = post.Mandaatkenmerk,
                    ["incassantid"] = stand.Instellingen?.IncassantId.ToString() ?? string.Empty,
                    ["omschrijving"] = post.Omschrijving,
                })))];
    }

    /// <summary>Besluit 204: een stornering gaat naar de leden en voogden onder de betaler van de post.</summary>
    public static IReadOnlyList<Emailontvanger> Stornering(Vorderingstand vordering, IncassoStand stand, DateOnly vandaag, string vereniging)
    {
        ArgumentNullException.ThrowIfNull(vordering);
        return [.. Ontvangers.VoorLeden(Ontvangers.LedenVanBetaler(stand, vordering.Post.BetalerId), Ontvangerkeuze.Standaard, vandaag, vereniging)
            .Select(o => MetVelden(o, new()
            {
                ["bedrag"] = new Bedrag(vordering.Saldo).ToString(),
                ["reden"] = vordering.Redentekst ?? "onbekend",
                ["omschrijving"] = vordering.Post.Omschrijving,
            }))];
    }

    /// <summary>Besluit 207: de factuur (of herinnering) naar het adres van de relatie, met de PDF als bijlage.</summary>
    public static Emailontvanger? Factuur(Verkoopfactuur factuur, Relatie relatie, string vereniging, byte[]? pdf)
    {
        ArgumentNullException.ThrowIfNull(factuur);
        ArgumentNullException.ThrowIfNull(relatie);
        if (!Emailadres.IsGeldig(relatie.Email))
        {
            return null;
        }

        var aanhef = string.IsNullOrWhiteSpace(relatie.Contactpersoon) ? relatie.Naam : relatie.Contactpersoon!;
        return new Emailontvanger(aanhef, relatie.Email!.Trim(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["aanhef"] = aanhef,
            ["vereniging"] = vereniging,
            ["factuurnummer"] = factuur.Nummer ?? string.Empty,
            ["bedrag"] = factuur.Totaal.ToString(),
            ["vervaldatum"] = Datum(factuur.Vervaldatum),
            ["factuurdatum"] = Datum(factuur.Factuurdatum),
        })
        {
            Bijlagen = pdf is null ? [] : [new Emailbijlage($"Factuur {factuur.Nummer}.pdf", pdf, "application/pdf")],
        };
    }

    private static Emailontvanger MetVelden(Emailontvanger ontvanger, Dictionary<string, string> extra)
    {
        var velden = new Dictionary<string, string>(ontvanger.Velden, StringComparer.OrdinalIgnoreCase);
        foreach (var (naam, waarde) in extra)
        {
            velden[naam] = waarde;
        }

        return ontvanger with { Velden = velden };
    }
}

/// <summary>Leest het clientbestand dat Google Cloud geeft bij een OAuth-client van het type Desktop-app.</summary>
public static class GoogleClientbestand
{
    public static bool TryLees(string json, out string clientId, out string clientGeheim)
    {
        clientId = clientGeheim = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("installed", out var installed)
                && installed.TryGetProperty("client_id", out var id) && installed.TryGetProperty("client_secret", out var geheim)
                && id.GetString() is { Length: > 0 } i && geheim.GetString() is { Length: > 0 } g)
            {
                clientId = i;
                clientGeheim = g;
                return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }
}
