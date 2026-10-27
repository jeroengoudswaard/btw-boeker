using System.Globalization;

namespace Verenigd.Application.Import;

/// <summary>Datums uit SOL3: altijd dag-maand-jaar, met <c>-</c> of <c>/</c> (besluit 28).</summary>
public static class Sol3Datum
{
    private static readonly string[] Formaten = ["d-M-yyyy", "d/M/yyyy"];

    public static bool TryParse(string? invoer, out DateOnly datum) =>
        DateOnly.TryParseExact(invoer?.Trim(), Formaten, CultureInfo.InvariantCulture, DateTimeStyles.None, out datum);
}
