using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Trips;

/// <summary>
/// Takes one of someone's photos off their visit. Anyone else's photo, or one on another visit,
/// looks like a missing one.
/// </summary>
public sealed class RemoveVisitPhotoHandler(ITravelMapRepository maps)
{
    public async Task<Result<Done>> HandleAsync(long userId, long visitId, long mediaId, CancellationToken cancellationToken) =>
        await maps.RemovePhotoAsync(userId, visitId, mediaId, cancellationToken)
            ? Done.Value
            : AppError.NotFound("photo_not_found", "There is no such photo on this place.");
}
