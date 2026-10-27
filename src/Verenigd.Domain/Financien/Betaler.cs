namespace Verenigd.Domain.Financien;

/// <summary>
/// Degene die betaalt: IBAN, rekeninghouder en (vanaf fase 3) de SEPA-machtiging. Leden met hetzelfde IBAN
/// horen bij één betaler (besluit 29). Financieel gegeven: gaat nooit naar de server (besluit 35).
/// </summary>
public sealed class Betaler
{
    public const int MaximaleNaamLengte = 200;

    public Betaler(Iban iban, string? rekeninghouder)
    {
        Id = Guid.NewGuid();
        Iban = iban;
        WijzigRekeninghouder(rekeninghouder);
    }

    public Guid Id { get; private set; }
    public Iban Iban { get; private set; }
    public string? Rekeninghouder { get; private set; }

    /// <summary>Kenmerk van de SEPA-machtiging (mandaat-ID), handmatig ingevoerd (besluit 48).</summary>
    public string? Mandaatkenmerk { get; private set; }

    public DateOnly? MachtigingOndertekendOp { get; private set; }
    public DateOnly? MachtigingIngetrokkenOp { get; private set; }

    public bool HeeftActieveMachtiging(DateOnly op) =>
        Mandaatkenmerk is not null && MachtigingOndertekendOp <= op && (MachtigingIngetrokkenOp is null || MachtigingIngetrokkenOp > op);

    public void WijzigIban(Iban iban) => Iban = iban;

    public void RegistreerMachtiging(string mandaatkenmerk, DateOnly ondertekendOp)
    {
        var kenmerk = mandaatkenmerk?.Trim();
        if (!SepaTekst.IsGeldigKenmerk(kenmerk))
        {
            throw new ArgumentException(
                "Een mandaatkenmerk heeft 1–35 tekens (letters, cijfers en / - ? : ( ) . , ' +), zonder spaties.", nameof(mandaatkenmerk));
        }

        Mandaatkenmerk = kenmerk;
        MachtigingOndertekendOp = ondertekendOp;
        MachtigingIngetrokkenOp = null;
    }

    public void VerwijderMachtiging()
    {
        Mandaatkenmerk = null;
        MachtigingOndertekendOp = null;
        MachtigingIngetrokkenOp = null;
    }

    public void TrekMachtigingIn(DateOnly per)
    {
        if (Mandaatkenmerk is null)
        {
            throw new InvalidOperationException("Er is geen machtiging om in te trekken.");
        }

        MachtigingIngetrokkenOp = per;
    }

    public void WijzigRekeninghouder(string? rekeninghouder)
    {
        var schoon = string.IsNullOrWhiteSpace(rekeninghouder) ? null : rekeninghouder.Trim();
        Rekeninghouder = schoon is { Length: > MaximaleNaamLengte } ? schoon[..MaximaleNaamLengte] : schoon;
    }
}
