using Verenigd.Domain.Leden;

namespace Verenigd.Application.Leden;

public interface ILedenRepository
{
    /// <summary>Alle personen, inclusief uitgeschreven leden, met functies (en groepen) en voogden.</summary>
    Task<IReadOnlyList<Persoon>> HaalAlleAsync(CancellationToken annulering = default);

    Task<IReadOnlyList<Groep>> HaalGroepenAsync(CancellationToken annulering = default);

    /// <summary>Besluit 215: het soort per groep (op naam) wijzigen, ook buiten de import.</summary>
    Task BewaarGroepsoortenAsync(IReadOnlyDictionary<string, GroepSoort> soortPerGroep, CancellationToken annulering = default);

    /// <exception cref="KeyNotFoundException">De persoon bestaat niet (meer).</exception>
    Task BewaarNotitiesAsync(Guid persoonId, string? notities, DateTime nuUtc, CancellationToken annulering = default);

    /// <summary>Besluit 34: een goedgekeurd voorstel van de leiding overnemen (zie <see cref="Persoon.NeemVoorstelOver"/>).</summary>
    /// <exception cref="KeyNotFoundException">De persoon bestaat niet (meer).</exception>
    Task NeemVoorstelOverAsync(Guid persoonId, string veld, string? waarde, DateTime nuUtc, CancellationToken annulering = default);
}
