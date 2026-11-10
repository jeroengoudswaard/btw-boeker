using System.Text.Json;
using System.Text.Json.Serialization;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Boekhouding;

public sealed record SjabloonRubriek(string Naam);

public sealed record SjabloonRekening(int Nummer, string Naam, string? Rubriek, RekeningSoort Soort, RekeningRol Rol = RekeningRol.Geen, ResultaatKant? Kant = null);

/// <summary>Eén contributierekening per speltak: <c>Vanaf</c>, <c>Vanaf + Stap</c>, …; <c>{speltak}</c> in de naam wordt ingevuld.</summary>
public sealed record SjabloonSpeltakken(string Rubriek, int Vanaf, int Stap, string Naam, IReadOnlyList<string> Standaard);

/// <summary>
/// Een rekeningschema zonder bedragen of persoonsgegevens, om een administratie mee in te richten of te delen met andere
/// groepen (besluiten 114–117). Het formaat heeft een versie.
/// </summary>
public sealed record Rekeningsjabloon(
    string Naam, AdministratieSoort? Administratie, IReadOnlyList<SjabloonRubriek> Rubrieken, IReadOnlyList<SjabloonRekening> Rekeningen,
    SjabloonSpeltakken? Speltakken = null)
{
    /// <summary>Optioneel: de eigen jaarrekeningindeling (besluiten 116 en 263); leeg = de automatische indeling.</summary>
    public IReadOnlyList<SjabloonIndelingsregel> Jaarrekening { get; init; } = [];
}

public static class Rekeningsjablonen
{
    public const string Formaat = "btw-boeker-rekeningschema";
    public const int HuidigeVersie = 1;
    public const string Extensie = ".btwsjabloon";

    private static readonly JsonSerializerOptions Opties = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Het ingebouwde standaardschema (besluit 118).</summary>
    public static Rekeningsjabloon Standaard(AdministratieSoort administratie)
    {
        var naam = $"sjabloon-{(administratie == AdministratieSoort.Stichting ? "stichting" : "vereniging")}.json";
        using var stroom = typeof(Rekeningsjablonen).Assembly.GetManifestResourceStream(naam)
            ?? throw new InvalidOperationException($"Het ingebouwde sjabloon {naam} ontbreekt.");
        using var lezer = new StreamReader(stroom);
        return Lees(lezer.ReadToEnd());
    }

    /// <exception cref="InvalidOperationException">Geen (geldig) sjabloon; de melding zegt wat er mis is.</exception>
    public static Rekeningsjabloon Lees(string tekst)
    {
        Bestand? bestand;
        try
        {
            bestand = JsonSerializer.Deserialize<Bestand>(tekst, Opties);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Dit is geen geldig sjabloonbestand (de inhoud is geen leesbare JSON).");
        }

        if (bestand is null || !string.Equals(bestand.Formaat, Formaat, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Dit bestand is geen rekeningschema-sjabloon van BTW-Boeker.");
        }

        if (bestand.Versie > HuidigeVersie)
        {
            throw new InvalidOperationException($"Dit sjabloon is gemaakt met een nieuwere versie van BTW-Boeker (formaat {bestand.Versie}); werk de app eerst bij.");
        }

        var sjabloon = new Rekeningsjabloon(
            string.IsNullOrWhiteSpace(bestand.Naam) ? "Sjabloon" : bestand.Naam.Trim(),
            bestand.Administratie,
            [.. (bestand.Rubrieken ?? []).Select(r => new SjabloonRubriek(r.Naam?.Trim() ?? string.Empty))],
            [.. (bestand.Rekeningen ?? []).Select(r => new SjabloonRekening(r.Nummer, r.Naam?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(r.Rubriek) ? null : r.Rubriek.Trim(), r.Soort, r.Rol, r.Kant))],
            bestand.Speltakken)
        {
            Jaarrekening = [.. (bestand.Jaarrekening ?? []).Select(r => new SjabloonIndelingsregel(r.Rapport, r.Afdeling, r.Soort, r.Tekst?.Trim() ?? string.Empty,
                [.. (r.Rubrieken ?? []).Select(n => n?.Trim() ?? string.Empty)]))],
        };
        Controleer(sjabloon);
        return sjabloon;
    }

    public static string Schrijf(Rekeningsjabloon sjabloon)
    {
        ArgumentNullException.ThrowIfNull(sjabloon);
        var bestand = new Bestand
        {
            Formaat = Formaat,
            Versie = HuidigeVersie,
            Naam = sjabloon.Naam,
            Administratie = sjabloon.Administratie,
            Rubrieken = [.. sjabloon.Rubrieken.Select(r => new BestandRubriek { Naam = r.Naam })],
            Rekeningen = [.. sjabloon.Rekeningen.Select(r => new BestandRekening { Nummer = r.Nummer, Naam = r.Naam, Rubriek = r.Rubriek, Soort = r.Soort, Rol = r.Rol, Kant = r.Kant })],
            Speltakken = sjabloon.Speltakken,
            Jaarrekening = sjabloon.Jaarrekening.Count == 0 ? null : [.. sjabloon.Jaarrekening.Select(r => new BestandIndelingsregel
            {
                Rapport = r.Rapport, Afdeling = r.Afdeling, Soort = r.Soort, Tekst = r.Tekst, Rubrieken = r.Rubrieken.Count == 0 ? null : [.. r.Rubrieken],
            })],
        };
        return JsonSerializer.Serialize(bestand, Opties) + Environment.NewLine;
    }

    /// <summary>
    /// Het rekeningschema van een ingerichte administratie als sjabloon (alleen actieve rekeningen, zonder bedragen), met de
    /// eigen jaarrekeningindeling voor zover die naar meegenomen rubrieken verwijst.
    /// </summary>
    public static Rekeningsjabloon UitStand(BoekhoudingStand stand, string naam)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var rekeningen = stand.Rekeningen.Where(r => r.IsActief).ToList();
        var rubrieken = stand.Rubrieken.OrderBy(r => r.Volgorde).Where(r => rekeningen.Exists(g => g.Rubriek?.Id == r.Id)).ToList();
        return new Rekeningsjabloon(naam, stand.Administratie,
            [.. rubrieken.Select(r => new SjabloonRubriek(r.Naam))],
            [.. rekeningen
                .OrderBy(r => r.Rubriek is null ? int.MaxValue : rubrieken.FindIndex(g => g.Id == r.Rubriek.Id)).ThenBy(r => r.Nummer)
                .Select(r => new SjabloonRekening(r.Nummer, r.Naam, r.Rubriek?.Naam, r.Soort, r.Rol, r.Soort == RekeningSoort.Resultaat ? r.Kant : null))])
        {
            Jaarrekening = [.. JaarrekeningDienst.NaarSjabloon(stand)
                .Select(r => r with { Rubrieken = [.. r.Rubrieken.Where(n => rubrieken.Exists(g => g.Naam == n))] })
                .Where(r => r.Soort != IndelingsregelSoort.Regel || r.Rubrieken.Count > 0)],
        };
    }

    /// <summary>
    /// De rekeningen om in te richten, in de volgorde van de rubrieken in het sjabloon, met een contributierekening per
    /// gekozen speltak (dubbele en lege namen worden overgeslagen). Alle bedragen zijn nul.
    /// </summary>
    /// <exception cref="InvalidOperationException">Een speltakrekening valt samen met een bestaand nummer of buiten het bereik.</exception>
    public static IReadOnlyList<Inrichtrekening> Rekeningen(Rekeningsjabloon sjabloon, IEnumerable<string> speltakken)
    {
        ArgumentNullException.ThrowIfNull(sjabloon);
        ArgumentNullException.ThrowIfNull(speltakken);
        var alle = sjabloon.Rekeningen.ToList();
        if (sjabloon.Speltakken is { } regel)
        {
            var namen = speltakken.Select(s => s?.Trim() ?? string.Empty).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            for (var i = 0; i < namen.Count; i++)
            {
                var nummer = regel.Vanaf + (i * regel.Stap);
                if (nummer > 99999 || alle.Exists(r => r.Nummer == nummer))
                {
                    throw new InvalidOperationException($"Voor speltak {namen[i]} is geen vrij rekeningnummer ({nummer}); kies minder speltakken.");
                }

                alle.Add(new SjabloonRekening(nummer, Kort(regel.Naam.Replace("{speltak}", namen[i], StringComparison.Ordinal)), regel.Rubriek, RekeningSoort.Resultaat));
            }
        }

        var volgorde = sjabloon.Rubrieken.Select(r => r.Naam).ToList();
        return [.. alle
            .OrderBy(r => r.Rubriek is null ? int.MaxValue : volgorde.IndexOf(r.Rubriek)).ThenBy(r => r.Nummer)
            .Select(r => new Inrichtrekening(r.Nummer, r.Naam, r.Rubriek, r.Soort, r.Rol, false, Bedrag.Nul, Bedrag.Nul) { Kant = r.Kant })];
    }

    /// <summary>Het voorstel voor <see cref="BoekhoudingDienst.ImporteerAsync"/>.</summary>
    public static Inrichtvoorstel Voorstel(IReadOnlyList<Inrichtrekening> rekeningen, int beginjaar) =>
        new(beginjaar - 1, rekeningen, []) { Herkomst = "standaardinrichting", MetVerkoopboek = true };

    private static void Controleer(Rekeningsjabloon sjabloon)
    {
        var rubrieken = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rubriek in sjabloon.Rubrieken)
        {
            if (rubriek.Naam.Length is 0 or > Rubriek.MaximaleNaamLengte || !rubrieken.Add(rubriek.Naam))
            {
                throw new InvalidOperationException($"De rubriek \"{rubriek.Naam}\" is leeg, te lang of dubbel.");
            }
        }

        var nummers = new HashSet<int>();
        foreach (var rekening in sjabloon.Rekeningen)
        {
            if (rekening.Nummer is < 1 or > 99999)
            {
                throw new InvalidOperationException($"Het rekeningnummer {rekening.Nummer} is ongeldig; gebruik 1 tot en met 99999.");
            }

            if (!nummers.Add(rekening.Nummer))
            {
                throw new InvalidOperationException($"Rekeningnummer {rekening.Nummer} komt twee keer voor.");
            }

            if (rekening.Naam.Length is 0 or > Grootboekrekening.MaximaleNaamLengte)
            {
                throw new InvalidOperationException($"Rekening {rekening.Nummer} heeft geen (geldige) naam.");
            }

            if (rekening.Rubriek is { } r && !rubrieken.Contains(r))
            {
                throw new InvalidOperationException($"Rekening {rekening.Nummer} verwijst naar de onbekende rubriek \"{r}\".");
            }

            if (!Enum.IsDefined(rekening.Soort) || !Enum.IsDefined(rekening.Rol) || (rekening.Rol != RekeningRol.Geen && rekening.Soort != RekeningSoort.Balans))
            {
                throw new InvalidOperationException($"Rekening {rekening.Nummer} heeft een ongeldige soort of rol.");
            }

            if (rekening.Kant is { } kant && (rekening.Soort != RekeningSoort.Resultaat || kant is not (ResultaatKant.Baten or ResultaatKant.Lasten)))
            {
                throw new InvalidOperationException($"Rekening {rekening.Nummer}: baten of lasten kan alleen bij een resultaatrekening.");
            }
        }

        if (sjabloon.Rekeningen.Count(r => r.Rol == RekeningRol.Reserve) != 1)
        {
            throw new InvalidOperationException("Een sjabloon heeft precies één reserverekening nodig (rol Reserve), voor de jaarafsluiting.");
        }

        foreach (var rol in new[] { RekeningRol.Kas, RekeningRol.Kruisposten })
        {
            if (sjabloon.Rekeningen.Count(r => r.Rol == rol) > 1)
            {
                throw new InvalidOperationException($"Er is maar één rekening met de rol {rol} mogelijk.");
            }
        }

        foreach (var regel in sjabloon.Jaarrekening)
        {
            if (!Enum.IsDefined(regel.Rapport) || !Jaarrekeningindeling.Afdelingen(regel.Rapport).Contains(regel.Afdeling) || !Enum.IsDefined(regel.Soort)
                || regel.Tekst.Length is 0 or > Indelingsregel.MaximaleTekstLengte || regel.Rubrieken.Any(n => !rubrieken.Contains(n)))
            {
                throw new InvalidOperationException($"De jaarrekeningregel \"{regel.Tekst}\" is ongeldig of verwijst naar een onbekende rubriek.");
            }
        }

        if (sjabloon.Speltakken is { } s
            && (s.Vanaf is < 1 or > 99999 || s.Stap < 1 || !s.Naam.Contains("{speltak}", StringComparison.Ordinal) || !rubrieken.Contains(s.Rubriek)))
        {
            throw new InvalidOperationException("De regel voor de speltakrekeningen is ongeldig.");
        }
    }

    private static string Kort(string naam) => naam.Length > Grootboekrekening.MaximaleNaamLengte ? naam[..Grootboekrekening.MaximaleNaamLengte] : naam;

    private sealed class Bestand
    {
        public string? Formaat { get; set; }
        public int Versie { get; set; }
        public string? Naam { get; set; }
        public AdministratieSoort? Administratie { get; set; }
        public List<BestandRubriek>? Rubrieken { get; set; }
        public List<BestandRekening>? Rekeningen { get; set; }
        public SjabloonSpeltakken? Speltakken { get; set; }
        public List<BestandIndelingsregel>? Jaarrekening { get; set; }
    }

    private sealed class BestandIndelingsregel
    {
        public JaarrekeningRapport Rapport { get; set; }
        public JaarrekeningAfdeling Afdeling { get; set; }
        public IndelingsregelSoort Soort { get; set; }
        public string? Tekst { get; set; }
        public List<string>? Rubrieken { get; set; }
    }

    private sealed class BestandRubriek
    {
        public string? Naam { get; set; }
    }

    private sealed class BestandRekening
    {
        public int Nummer { get; set; }
        public string? Naam { get; set; }
        public string? Rubriek { get; set; }
        public RekeningSoort Soort { get; set; }
        public RekeningRol Rol { get; set; }

        /// <summary>Optioneel (besluit 152); ontbreekt het, dan geldt de standaard (vanaf 8000 baten).</summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public ResultaatKant? Kant { get; set; }
    }
}
