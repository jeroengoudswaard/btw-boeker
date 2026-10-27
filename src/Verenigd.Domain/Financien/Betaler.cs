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

    public void WijzigRekeninghouder(string? rekeninghouder)
    {
        var schoon = string.IsNullOrWhiteSpace(rekeninghouder) ? null : rekeninghouder.Trim();
        Rekeninghouder = schoon is { Length: > MaximaleNaamLengte } ? schoon[..MaximaleNaamLengte] : schoon;
    }
}
