namespace BtwBoeker.Domain.Financien.Bank;

/// <summary>
/// De banktransactiecode uit CAMT.053: de ISO-code (domein, familie, subfamilie) en/of de eigen code van de bank,
/// bijvoorbeeld Rabobank 1025 voor rente (die heeft geen ISO-code).
/// </summary>
public readonly record struct BankTransactiecode(string? Domein, string? Familie, string? Subfamilie, string? Eigen, string? Uitgever)
{
    private static readonly Dictionary<string, string> Familienamen = new(StringComparer.Ordinal)
    {
        ["ICDT"] = "Verstuurde overboeking",
        ["RCDT"] = "Ontvangen overboeking",
        ["RDDT"] = "Betaalde incasso",
        ["IDDT"] = "Ontvangen incasso (eigen)",
        ["CCRD"] = "Pinbetaling",
        ["MDOP"] = "Diversen (bijvoorbeeld kosten)",
        ["MCOP"] = "Diversen (bij)",
        ["ICHQ"] = "Cheque",
    };

    /// <summary>Bijvoorbeeld <c>PMNT-ICDT-ESCT</c>; <c>null</c> zonder ISO-code.</summary>
    public string? Iso => Domein is null ? null : string.Join('-', new[] { Domein, Familie, Subfamilie }.Where(d => !string.IsNullOrEmpty(d)));

    /// <summary>Bijvoorbeeld <c>RABOBANK 1025</c>; <c>null</c> zonder eigen code.</summary>
    public string? EigenSleutel => Eigen is null ? null : $"{Uitgever ?? "BANK"} {Eigen}".ToUpperInvariant();

    /// <summary>Leesbare naam voor keuzelijsten.</summary>
    public string Omschrijving => Familie is not null && Familienamen.TryGetValue(Familie, out var naam)
        ? naam
        : Eigen is not null ? $"{Hoofdletter(Uitgever ?? "Bank")} {Eigen}" : Iso ?? "Onbekend";

    /// <summary>Opslagvorm: de vijf delen gescheiden door <c>|</c>.</summary>
    public string Waarde => string.Join('|', Domein, Familie, Subfamilie, Eigen, Uitgever);

    /// <summary>
    /// Een criterium uit een boekingsregel: een ISO-code of het begin ervan (<c>PMNT-ICDT</c>), of een eigen code
    /// (<c>RABOBANK 1025</c>). Hoofdletterongevoelig.
    /// </summary>
    public bool Past(string? criterium)
    {
        var c = criterium?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(c))
        {
            return false;
        }

        return (Iso is { } iso && (iso == c || iso.StartsWith(c + "-", StringComparison.Ordinal))) || EigenSleutel == c;
    }

    public static BankTransactiecode Lees(string? waarde)
    {
        var delen = (waarde ?? string.Empty).Split('|');
        string? Deel(int i) => i < delen.Length && delen[i].Length > 0 ? delen[i] : null;
        return new BankTransactiecode(Deel(0), Deel(1), Deel(2), Deel(3), Deel(4));
    }

    public override string ToString() => Iso ?? EigenSleutel ?? string.Empty;

    private static string Hoofdletter(string tekst) => tekst.Length == 0 ? tekst : char.ToUpperInvariant(tekst[0]) + tekst[1..].ToLowerInvariant();
}
