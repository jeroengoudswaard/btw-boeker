using Verenigd.Domain.Leden;

namespace Verenigd.Application.Leden;

public interface ILedenRepository
{
    /// <summary>Alle personen, inclusief uitgeschreven leden, met functies (en groepen) en voogden.</summary>
    Task<IReadOnlyList<Persoon>> HaalAlleAsync(CancellationToken annulering = default);

    Task<IReadOnlyList<Groep>> HaalGroepenAsync(CancellationToken annulering = default);

    /// <exception cref="KeyNotFoundException">De persoon bestaat niet (meer).</exception>
    Task BewaarNotitiesAsync(Guid persoonId, string? notities, DateTime nuUtc, CancellationToken annulering = default);
}
