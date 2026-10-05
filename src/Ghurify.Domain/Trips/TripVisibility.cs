using Ghurify.Domain.Identity;

namespace Ghurify.Domain.Trips;

/// <summary>Who is shown which trips.</summary>
public static class TripVisibility
{
    /// <summary>
    /// Whether women-only trips are shown to a viewer.
    ///
    /// They are hidden from people who could never join them, rather than shown and then
    /// refused. Someone whose gender is not known yet (signed out, or not stated on the
    /// profile) still sees them: the join step is where eligibility is enforced, and hiding a
    /// headline safety feature from every visitor would be worse than showing it.
    /// </summary>
    public static bool IncludesWomenOnlyTrips(Gender? viewerGender) =>
        viewerGender is null or Gender.Unspecified or Gender.Female;
}