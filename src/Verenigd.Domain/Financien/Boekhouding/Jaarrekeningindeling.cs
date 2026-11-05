using Verenigd.Domain.Administraties;

namespace Verenigd.Domain.Financien.Boekhouding;

/// <summary>De twee jaarstukken met een eigen indeling (besluit 265).</summary>
public enum JaarrekeningRapport
{
    Balans = 1,

    /// <summary>De staat van baten en lasten (W&amp;V).</summary>
    BatenEnLasten = 2,
}

/// <summary>
/// De vaste delen van een jaarstuk. Bij de balans bepaalt het saldo van een rekening het deel (debet = activa), bij baten
/// en lasten de eigenschap <see cref="ResultaatKant"/> (besluit 152).
/// </summary>
public enum JaarrekeningAfdeling
{
    Activa = 1,
    Passiva = 2,
    Baten = 3,
    Lasten = 4,
}

public enum IndelingsregelSoort
{
    /// <summary>Een tussenkop zonder bedragen.</summary>
    Kop = 1,

    /// <summary>Een regel met de rekeningen van één of meer rubrieken.</summary>
    Regel = 2,

    /// <summary>Het totaal van de regels sinds de vorige kop (of het begin van het deel).</summary>
    Subtotaal = 3,
}

/// <summary>Eén regel van de indeling zoals de gebruiker hem opgeeft; de volgorde is die van de lijst.</summary>
public sealed record IndelingsregelInvoer(JaarrekeningAfdeling Afdeling, IndelingsregelSoort Soort, string Tekst, IReadOnlyList<Rubriek> Rubrieken);

/// <summary>
/// Een eigen indeling van de balans of de staat van baten en lasten (besluiten 69, 151 en 263–268): kopjes, regels die
/// rubrieken bundelen en subtotalen. Alleen weergave; het rekeningschema en de boekingen veranderen niet. Een rubriek staat
/// hooguit één keer in elk deel.
/// </summary>
public sealed class Jaarrekeningindeling
{
    private readonly List<Indelingsregel> _regels = [];

    private Jaarrekeningindeling()
    {
    }

    public Jaarrekeningindeling(AdministratieSoort administratie, JaarrekeningRapport rapport)
    {
        if (!Enum.IsDefined(rapport))
        {
            throw new ArgumentOutOfRangeException(nameof(rapport));
        }

        Administratie = administratie;
        Rapport = rapport;
    }

    public int Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public JaarrekeningRapport Rapport { get; private set; }

    /// <summary>In de volgorde van de indeling.</summary>
    public IReadOnlyList<Indelingsregel> Regels => [.. _regels.OrderBy(r => r.Volgorde)];

    public static IReadOnlyList<JaarrekeningAfdeling> Afdelingen(JaarrekeningRapport rapport) => rapport == JaarrekeningRapport.Balans
        ? [JaarrekeningAfdeling.Activa, JaarrekeningAfdeling.Passiva]
        : [JaarrekeningAfdeling.Baten, JaarrekeningAfdeling.Lasten];

    /// <summary>Vervangt alle regels; bij een fout blijft de indeling ongewijzigd.</summary>
    /// <exception cref="ArgumentException">De melding zegt welke regel niet klopt.</exception>
    public void Vervang(IEnumerable<IndelingsregelInvoer> regels)
    {
        ArgumentNullException.ThrowIfNull(regels);
        var lijst = regels.ToList();
        var afdelingen = Afdelingen(Rapport);
        var gebruikt = new HashSet<(JaarrekeningAfdeling, int)>();
        var nieuw = new List<Indelingsregel>();
        for (var i = 0; i < lijst.Count; i++)
        {
            var invoer = lijst[i] ?? throw new ArgumentException($"Regel {i + 1} ontbreekt.", nameof(regels));
            if (!afdelingen.Contains(invoer.Afdeling))
            {
                throw new ArgumentException($"Regel {i + 1}: {invoer.Afdeling} hoort niet bij dit rapport.", nameof(regels));
            }

            if (!Enum.IsDefined(invoer.Soort))
            {
                throw new ArgumentException($"Regel {i + 1} heeft een onbekende soort.", nameof(regels));
            }

            var tekst = invoer.Tekst?.Trim() ?? string.Empty;
            if (tekst.Length is 0 or > Indelingsregel.MaximaleTekstLengte)
            {
                throw new ArgumentException($"Regel {i + 1} heeft geen tekst, of een tekst van meer dan {Indelingsregel.MaximaleTekstLengte} tekens.", nameof(regels));
            }

            var rubrieken = invoer.Rubrieken ?? [];
            if (invoer.Soort != IndelingsregelSoort.Regel && rubrieken.Count > 0)
            {
                throw new ArgumentException($"\"{tekst}\": een kop of subtotaal heeft geen rubrieken.", nameof(regels));
            }

            if (invoer.Soort == IndelingsregelSoort.Regel && rubrieken.Count == 0)
            {
                throw new ArgumentException($"\"{tekst}\": kies minstens één rubriek.", nameof(regels));
            }

            foreach (var rubriek in rubrieken)
            {
                if (rubriek.Administratie != Administratie)
                {
                    throw new ArgumentException($"De rubriek {rubriek.Naam} hoort bij een andere administratie.", nameof(regels));
                }

                if (!gebruikt.Add((invoer.Afdeling, Sleutel(rubriek))))
                {
                    throw new ArgumentException($"De rubriek {rubriek.Naam} staat al bij een andere regel van {Naam(invoer.Afdeling)}.", nameof(regels));
                }
            }

            nieuw.Add(new Indelingsregel(i + 1, invoer.Afdeling, invoer.Soort, tekst, rubrieken.Distinct()));
        }

        _regels.Clear();
        _regels.AddRange(nieuw);
    }

    public static string Naam(JaarrekeningAfdeling afdeling) => afdeling switch
    {
        JaarrekeningAfdeling.Activa => "Activa",
        JaarrekeningAfdeling.Passiva => "Passiva",
        JaarrekeningAfdeling.Baten => "Baten",
        _ => "Lasten",
    };

    // Een nieuwe rubriek heeft nog geen Id; dan telt het object zelf.
    private static int Sleutel(Rubriek rubriek) => rubriek.Id != 0 ? rubriek.Id : -System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(rubriek);
}

/// <summary>Eén regel van een <see cref="Jaarrekeningindeling"/>.</summary>
public sealed class Indelingsregel
{
    public const int MaximaleTekstLengte = 100;

    private readonly List<Rubriek> _rubrieken = [];

    private Indelingsregel()
    {
        Tekst = null!;
    }

    internal Indelingsregel(int volgorde, JaarrekeningAfdeling afdeling, IndelingsregelSoort soort, string tekst, IEnumerable<Rubriek> rubrieken)
    {
        Volgorde = volgorde;
        Afdeling = afdeling;
        Soort = soort;
        Tekst = tekst;
        _rubrieken.AddRange(rubrieken);
    }

    public int Id { get; private set; }
    public int Volgorde { get; private set; }
    public JaarrekeningAfdeling Afdeling { get; private set; }
    public IndelingsregelSoort Soort { get; private set; }
    public string Tekst { get; private set; }
    public IReadOnlyList<Rubriek> Rubrieken => _rubrieken;

    public bool Bevat(Rubriek? rubriek) =>
        rubriek is not null && _rubrieken.Exists(r => ReferenceEquals(r, rubriek) || (r.Id != 0 && r.Id == rubriek.Id));
}
