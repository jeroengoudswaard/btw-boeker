using System.Text.Json;
using System.Text.Json.Serialization;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Boekhouding;

public sealed record SjabloonRubriek(string Naam);

public sealed record SjabloonRekening(int Nummer, string Naam, string? Rubriek, RekeningSoort Soort, RekeningRol Rol = RekeningRol.Geen);

/// <summary>Eén contributierekening per speltak: <c>Vanaf</c>, <c>Vanaf + Stap</c>, …; <c>{speltak}</c> in de naam wordt ingevuld.</summary>
public sealed record SjabloonSpeltakken(string Rubriek, int Vanaf, int Stap, string Naam, IReadOnlyList<string> Standaard);

/// <summary>
/// Een rekeningschema zonder bedragen of persoonsgegevens, om een administratie mee in te richten of te delen met andere
/// groepen (besluiten 114–117). In fase 5 komt de begrotingsindeling erbij; daarom heeft het formaat een versie.
/// </summary>
public sealed record Rekeningsjabloon(
    string Naam, AdministratieSoort? Administratie, IReadOnlyList<SjabloonRubriek> Rubrieken, IReadOnlyList<SjabloonRekening> Rekeningen,
    SjabloonSpeltakken? Speltakken = null);

public static class Rekeningsjablonen
{
    public const string Formaat = "verenigd-rekeningschema";
    public const int HuidigeVersie = 1;
    public const string Extensie = ".vsjabloon";

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
            throw new InvalidOperationException("Dit bestand is geen rekeningschema-sjabloon van Verenigd.");
        }

        if (bestand.Versie > HuidigeVersie)
        {
            throw new InvalidOperationException($"Dit sjabloon is gemaakt met een nieuwere versie van Verenigd (formaat {bestand.Versie}); werk de app eerst bij.");
        }

        var sjabloon = new Rekeningsjabloon(
            string.IsNullOrWhiteSpace(bestand.Naam) ? "Sjabloon" : bestand.Naam.Trim(),
            bestand.Administratie,
            [.. (bestand.Rubrieken ?? []).Select(r => new SjabloonRubriek(r.Naam?.Trim() ?? string.Empty))],
            [.. (bestand.Rekeningen ?? []).Select(r => new SjabloonRekening(r.Nummer, r.Naam?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(r.Rubriek) ? null : r.Rubriek.Trim(), r.Soort, r.Rol))],
            bestand.Speltakken);
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
            Rekeningen = [.. sjabloon.Rekeningen.Select(r => new BestandRekening { Nummer = r.Nummer, Naam = r.Naam, Rubriek = r.Rubriek, Soort = r.Soort, Rol = r.Rol })],
            Speltakken = sjabloon.Speltakken,
        };
        return JsonSerializer.Serialize(bestand, Opties) + Environment.NewLine;
    }

    /// <summary>Het rekeningschema van een ingerichte administratie als sjabloon (alleen actieve rekeningen, zonder bedragen).</summary>
    public static Rekeningsjabloon UitStand(BoekhoudingStand stand, string naam)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var rekeningen = stand.Rekeningen.Where(r => r.IsActief).ToList();
        var rubrieken = stand.Rubrieken.OrderBy(r => r.Volgorde).Where(r => rekeningen.Exists(g => g.Rubriek?.Id == r.Id)).ToList();
        return new Rekeningsjabloon(naam, stand.Administratie,
            [.. rubrieken.Select(r => new SjabloonRubriek(r.Naam))],
            [.. rekeningen
                .OrderBy(r => r.Rubriek is null ? int.MaxValue : rubrieken.FindIndex(g => g.Id == r.Rubriek.Id)).ThenBy(r => r.Nummer)
                .Select(r => new SjabloonRekening(r.Nummer, r.Naam, r.Rubriek?.Naam, r.Soort, r.Rol))]);
    }

    /// <summary>
    /// De rekeningen om in te richten, in de volgorde van de rubrieken in het sjabloon, met een contributierekening per
    /// gekozen speltak (dubbele en lege namen worden overgeslagen). Alle bedragen zijn nul.
    /// </summary>
    /// <exception cref="InvalidOperationException">Een speltakrekening valt samen met een bestaand nummer of buiten het bereik.</exception>
    public static IReadOnlyList<VoorbladRekening> Rekeningen(Rekeningsjabloon sjabloon, IEnumerable<string> speltakken)
    {
        ArgumentNullException.ThrowIfNull(sjabloon);
        ArgumentNullException.ThrowIfNull(speltakken);
        var alle = sjabloon.Rekeningen.ToList();
        if (sjabloon.Speltakken is { } regel)
        {
            var namen = speltakken.Select(s => s?.Trim() ?? string.Empty).Where(s => s.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
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
            .Select(r => new VoorbladRekening(r.Nummer, r.Naam, r.Rubriek, r.Soort, r.Rol, false, Bedrag.Nul, Bedrag.Nul))];
    }

    /// <summary>Het voorstel voor <see cref="BoekhoudingDienst.ImporteerAsync"/>: dezelfde route als de Excel-overname.</summary>
    public static VoorbladVoorstel Voorstel(IReadOnlyList<VoorbladRekening> rekeningen, int beginjaar) =>
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
    }
}
