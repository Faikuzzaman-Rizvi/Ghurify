using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Trips;

/// <summary>
/// Takes a visit off someone's own map, a Ghurify trip's included (it is never added back).
/// Anyone else's visit looks like a missing one.
/// </summary>
public sealed class RemoveVisitHandler(ITravelMapRepository maps)
{
    public async Task<Result<Done>> HandleAsync(long userId, long visitId, CancellationToken cancellationToken) =>
        await maps.RemoveVisitAsync(visitId, userId, cancellationToken)
            ? Done.Value
            : AppError.NotFound("visit_not_found", "There is no such place on your map.");
}
