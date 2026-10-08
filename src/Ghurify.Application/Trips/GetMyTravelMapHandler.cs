using Ghurify.Application.Abstractions;
using Ghurify.Application.Social;

namespace Ghurify.Application.Trips;

/// <summary>
/// The signed-in person's own travel map: every place they have been (from Ghurify trips and the
/// ones they added), their notes, photos from their stories, and their confirmed trips to come.
/// </summary>
public sealed class GetMyTravelMapHandler(ITravelMapRepository maps, MediaLinks links, TripViewer viewer)
{
    public async Task<Result<TravelMap>> HandleAsync(long userId, CancellationToken cancellationToken)
    {
        var data = await maps.GetAsync(userId, includePrivate: true, viewer.TodayInDhaka, cancellationToken);

        return data is null
            ? AppError.NotFound("user_not_found", "There is no such person.")
            : TravelMapBuilder.Build(data, links.Link);
    }
}
