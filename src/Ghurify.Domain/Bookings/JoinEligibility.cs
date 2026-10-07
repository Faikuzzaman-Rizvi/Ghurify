using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;

namespace Ghurify.Domain.Bookings;

/// <summary>Who may ask to join which trip, before anything about the trip's seats is checked.</summary>
public static class JoinEligibility
{
    /// <summary>Null when the traveller may request to join; otherwise the reason they may not.</summary>
    public static string? Check(UserAccess traveler, GroupType groupType)
    {
        ArgumentNullException.ThrowIfNull(traveler);

        if (!traveler.IsActive)
        {
            return "account_inactive";
        }

        if (!traveler.IsVerifiedTraveler)
        {
            return "traveler_not_verified";
        }

        // Women-only is a safety promise to every woman on the trip: it is enforced on the
        // verified gender, not on what anyone types.
        if (groupType == GroupType.WomenOnly && traveler.Gender != Gender.Female)
        {
            return "women_only_trip";
        }

        return null;
    }
}
