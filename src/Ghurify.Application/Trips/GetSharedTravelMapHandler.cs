using Ghurify.Application.Abstractions;
using Ghurify.Application.Social;

namespace Ghurify.Application.Trips;

/// <summary>
/// Someone's travel map as anyone else sees it, on their public profile. Empty unless they chose to
/// share it, and then only the Ghurify destinations they have been to: never a place they added
/// themselves (it could be their home), their notes, or where they are going next. The database
/// applies the same rules, so nothing private ever leaves it for this request.
/// </summary>
public sealed class GetSharedTravelMapHandler(ITravelMapRepository maps, MediaLinks links, TripViewer viewer)
{
    public async Task<Result<TravelMap>> HandleAsync(long userId, CancellationToken cancellationToken)
    {
        var data = await maps.GetAsync(userId, includePrivate: false, viewer.TodayInDhaka, cancellationToken);

        if (data is null)
        {
            return AppError.NotFound("user_not_found", "There is no such person.");
        }

        // Belt and braces: whatever came back, a map that is not shared shows nothing.
        return data.Shared
            ? TravelMapBuilder.Build(data with { Upcoming = [] }, links.Link)
            : TravelMapBuilder.Build(data with { Visits = [], Photos = [], Upcoming = [] }, links.Link);
    }
}
