using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Verenigd.App.ViewModels;

/// <summary>
/// Besluit 234: meet hoe lang een handeling duurt en schrijft dat in het logbestand ("Duur Openen database: 812 ms").
/// Alleen de naam van de handeling, nooit persoonsgegevens. Gebruik: <c>using var meting = Meetpunt.Start(logger, "…");</c>
/// </summary>
internal readonly struct Meetpunt(ILogger logger, string handeling) : IDisposable
{
    private readonly long _start = Stopwatch.GetTimestamp();

    public static Meetpunt Start(ILogger logger, string handeling) => new(logger, handeling);

    public void Dispose() => ShellLog.Duur(logger, handeling, (long)Stopwatch.GetElapsedTime(_start).TotalMilliseconds);
}
