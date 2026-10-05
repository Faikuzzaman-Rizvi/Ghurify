namespace Ghurify.Infrastructure.Data;

/// <summary>
/// Every stored procedure name used by the repositories, in one place.
/// Repositories reference these constants so a renamed procedure is a compile error
/// instead of a runtime one. Names follow the intent prefixes from the database rules:
/// Query (many rows), Get (one row), Add, Set, Del.
/// </summary>
public static class Procedures
{
    public static class Main
    {
        public const string Schema = "Main";

        /// <summary>Stores a new OTP, enforcing the per-address send limit in the same transaction.</summary>
        public const string AddOtpCode = "[Main].[AddOtpCode]";

        /// <summary>Counts a wrong guess and locks the code once the limit is reached.</summary>
        public const string SetOtpCodeAttempted = "[Main].[SetOtpCodeAttempted]";

        /// <summary>Returns the account for an email address, creating it on first sign-in.</summary>
        public const string GetOrAddUserByEmail = "[Main].[GetOrAddUserByEmail]";

        /// <summary>Revokes a refresh token and issues its successor, atomically.</summary>
        public const string SetRefreshTokenRotated = "[Main].[SetRefreshTokenRotated]";

        /// <summary>Revokes every live token in a family.</summary>
        public const string SetRefreshTokenFamilyRevoked = "[Main].[SetRefreshTokenFamilyRevoked]";

        /// <summary>Every destination, with its live-trip count and lowest price.</summary>
        public const string QueryDestinations = "[Main].[QueryDestinations]";

        /// <summary>Trip search: filtered, sorted and paged, with the total on every row.</summary>
        public const string QueryTrips = "[Main].[QueryTrips]";

        /// <summary>One live trip with its cost breakdown and itinerary.</summary>
        public const string GetTrip = "[Main].[GetTrip]";
    }

    public static class Pay
    {
        public const string Schema = "Pay";
    }

    public static class Social
    {
        public const string Schema = "Social";
    }

    public static class Safety
    {
        public const string Schema = "Safety";
    }
}
