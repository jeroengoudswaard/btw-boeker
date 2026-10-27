namespace Verenigd.Domain.Leden;

/// <summary>
/// Een Salesforce-ID uit SOL3 (zoals het Contactpersoon-ID). Hoofdlettergevoelig: altijd ordinaal
/// vergelijken (besluit 30).
/// </summary>
public readonly record struct SolId
{
    public SolId(string waarde)
    {
        var tekst = waarde?.Trim();
        if (string.IsNullOrEmpty(tekst))
        {
            throw new ArgumentException("Een SOL3-ID mag niet leeg zijn.", nameof(waarde));
        }

        Waarde = tekst;
    }

    public string Waarde { get; }

    public bool Equals(SolId ander) => string.Equals(Waarde, ander.Waarde, StringComparison.Ordinal);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Waarde ?? string.Empty);

    public override string ToString() => Waarde ?? string.Empty;
}
