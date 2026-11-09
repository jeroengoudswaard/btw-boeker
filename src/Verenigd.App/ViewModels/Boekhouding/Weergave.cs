using System.Globalization;
using Verenigd.Domain.Financien;

namespace Verenigd.App.ViewModels.Boekhouding;

internal static class Weergave
{
    private static readonly CultureInfo Nederlands = CultureInfo.GetCultureInfo("nl-NL");

    public static string Datum(DateOnly? datum) => datum?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    public static string Invoer(Bedrag bedrag) => (bedrag.Centen / 100m).ToString("0.00", Nederlands);

    public static DateOnly? AlsDatum(DateTime? datum) => datum is { } d ? DateOnly.FromDateTime(d) : null;

    public static DateTime? AlsDateTime(DateOnly? datum) => datum?.ToDateTime(TimeOnly.MinValue);
}
