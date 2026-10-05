using Ghurify.Application.Identity;
using Ghurify.Domain.Trips;

namespace Ghurify.Application.Trips;

/// <summary>
/// Works out what the person asking may see. Trip pages are public, so the viewer may be
/// signed out; when signed in, their profile decides whether women-only trips are shown.
/// </summary>
public sealed class TripViewer(IUserRepository users, IClock clock)
{
    /// <summary>
    /// Bangladesh has kept UTC+6 all year since 2009. "Today" for a trip is the date in Dhaka,
    /// not in UTC: between midnight and 06:00 in Dhaka the two disagree.
    /// </summary>
    private static readonly TimeSpan DhakaOffset = TimeSpan.FromHours(6);

    public DateOnly TodayInDhaka => DateOnly.FromDateTime(clock.UtcNow.ToOffset(DhakaOffset).DateTime);

    public async Task<bool> IncludesWomenOnlyAsync(long? viewerId, CancellationToken cancellationToken)
    {
        if (viewerId is null)
        {
            return TripVisibility.IncludesWomenOnlyTrips(viewerGender: null);
        }

        var viewer = await users.FindByIdAsync(viewerId.Value, cancellationToken);
        return TripVisibility.IncludesWomenOnlyTrips(viewer?.Gender);
    }
}
