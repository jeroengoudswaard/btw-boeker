using Verenigd.Domain.Administraties;

namespace Verenigd.Domain.Financien.Debiteuren;

public enum DebiteurmutatieSoort
{
    /// <summary>De run is geboekt: de vordering ontstaat.</summary>
    Vordering = 1,

    /// <summary>De bank heeft de incassobatch uitgekeerd.</summary>
    Ontvangst = 2,

    /// <summary>De incasso is teruggeboekt (besluit 92).</summary>
    Stornering = 3,

    /// <summary>Een handmatige betaling, bijvoorbeeld een overboeking door de ouders (besluit 102).</summary>
    Betaling = 4,

    /// <summary>Afgeboekt als oninbaar (besluit 101).</summary>
    Oninbaar = 5,

    /// <summary>Verlaagd door een creditfactuur (stap 4c-2).</summary>
    Creditering = 6,
}

/// <summary>
/// Eén mutatie op een vordering: een contributiepost (aangeduid met het EndToEndId) of een verkoopfactuur (het factuurnummer). Het saldo van de vordering is
/// de som van de mutaties waarvan de journaalpost bestaat en niet is tegengeboekt.
/// </summary>
public sealed class Debiteurmutatie
{
    private Debiteurmutatie()
    {
        Vordering = null!;
    }

    public Debiteurmutatie(
        AdministratieSoort administratie, string vordering, Guid betalerId, Guid? incassorunId, DebiteurmutatieSoort soort,
        DateOnly datum, long saldo, Guid postId, string? reden)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vordering);
        ArgumentOutOfRangeException.ThrowIfZero(saldo);
        Id = Guid.NewGuid();
        Administratie = administratie;
        Vordering = vordering.Trim();
        BetalerId = betalerId;
        IncassorunId = incassorunId;
        Soort = soort;
        Datum = datum;
        Saldo = saldo;
        PostId = postId;
        Reden = string.IsNullOrWhiteSpace(reden) ? null : reden.Trim();
    }

    public Guid Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }

    /// <summary>Het EndToEndId van de oorspronkelijke incassopost.</summary>
    public string Vordering { get; private set; }

    public Guid BetalerId { get; private set; }

    /// <summary>De run waaruit de mutatie volgt (bij vordering en ontvangst), of <c>null</c>.</summary>
    public Guid? IncassorunId { get; private set; }

    public DebiteurmutatieSoort Soort { get; private set; }
    public DateOnly Datum { get; private set; }

    /// <summary>In centen: positief verhoogt de vordering, negatief verlaagt haar.</summary>
    public long Saldo { get; private set; }

    /// <summary>De journaalpost waaruit de mutatie volgt.</summary>
    public Guid PostId { get; private set; }

    /// <summary>Bij een stornering: de SEPA-redencode.</summary>
    public string? Reden { get; private set; }
}

/// <summary>Een gestorneerde post die in de volgende incassorun opnieuw wordt meegenomen (besluit 92).</summary>
public sealed class Herincasso
{
    private Herincasso()
    {
        Vordering = null!;
    }

    public Herincasso(string vordering, DateTime aangevraagdUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vordering);
        Id = Guid.NewGuid();
        Vordering = vordering.Trim();
        AangevraagdUtc = aangevraagdUtc;
    }

    public Guid Id { get; private set; }
    public string Vordering { get; private set; }
    public DateTime AangevraagdUtc { get; private set; }

    /// <summary>De run waarin de herincasso is meegenomen; <c>null</c> zolang hij nog wacht.</summary>
    public Guid? RunId { get; private set; }

    public bool IsOpen => RunId is null;

    public void Verwerk(Guid runId)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Deze herincasso is al in een run meegenomen.");
        }

        RunId = runId;
    }

    /// <summary>De run is geannuleerd: de herincasso wacht weer op de volgende run.</summary>
    public void GeefVrij() => RunId = null;
}

/// <summary>Leesbare namen voor SEPA-redencodes van storneringen (ISO 20022 ExternalReturnReason1Code).</summary>
public static class SepaRetourreden
{
    private static readonly Dictionary<string, string> Namen = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AM04"] = "Onvoldoende saldo",
        ["MD06"] = "Terugboeking door de betaler",
        ["MS02"] = "Geweigerd door de betaler",
        ["MS03"] = "Geweigerd door de bank",
        ["AC01"] = "Rekeningnummer onjuist",
        ["AC04"] = "Rekening opgeheven",
        ["AC06"] = "Rekening geblokkeerd",
        ["AG01"] = "Incasso niet toegestaan op deze rekening",
        ["MD01"] = "Geen machtiging",
        ["MD07"] = "Rekeninghouder overleden",
        ["SL01"] = "Geblokkeerd door de betaler (incassoblokkade)",
        ["FF01"] = "Ongeldig bestand",
        ["BE05"] = "Incassant onbekend",
    };

    public static string Omschrijving(string? code) =>
        code is not null && Namen.TryGetValue(code.Trim(), out var naam) ? naam : "Onbekende reden";

    public static string Weergave(string? code) => code is null ? "Onbekende reden" : $"{Omschrijving(code)} ({code.Trim().ToUpperInvariant()})";
}
