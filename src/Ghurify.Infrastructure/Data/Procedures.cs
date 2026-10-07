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

        /// <summary>Registers an account waiting for email confirmation, atomically.</summary>
        public const string AddPendingUser = "[Main].[AddPendingUser]";

        /// <summary>Sets or removes a profile picture, returning the one it replaced.</summary>
        public const string SetUserAvatar = "[Main].[SetUserAvatar]";

        /// <summary>The admin people search.</summary>
        public const string QueryAdminUsers = "[Main].[QueryAdminUsers]";

        /// <summary>One person in full, for the admin desk.</summary>
        public const string GetAdminUser = "[Main].[GetAdminUser]";

        /// <summary>Suspends, reactivates or closes an account, ending its sessions when not active.</summary>
        public const string SetUserStatus = "[Main].[SetUserStatus]";

        /// <summary>Makes the password stop working and ends every session.</summary>
        public const string SetPasswordResetRequired = "[Main].[SetPasswordResetRequired]";

        /// <summary>The admin trip search, in every status.</summary>
        public const string QueryAdminTrips = "[Main].[QueryAdminTrips]";

        /// <summary>Adds or edits a destination by slug.</summary>
        public const string SetDestination = "[Main].[SetDestination]";

        /// <summary>Identity documents whose images are due to be deleted.</summary>
        public const string QueryVerificationDocumentsToPurge = "[Main].[QueryVerificationDocumentsToPurge]";

        /// <summary>An account and its password hash, by email address.</summary>
        public const string GetUserCredential = "[Main].[GetUserCredential]";

        /// <summary>Sets a password; confirms a pending account and can end every session.</summary>
        public const string SetUserPassword = "[Main].[SetUserPassword]";

        /// <summary>Counts a wrong password for an address and pauses sign-in at the limit.</summary>
        public const string SetSignInFailure = "[Main].[SetSignInFailure]";

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

        /// <summary>Creates a draft trip with its cost lines and itinerary.</summary>
        public const string AddTrip = "[Main].[AddTrip]";

        /// <summary>Saves a host's own trip, replacing its lines; locks terms once seats are taken.</summary>
        public const string SetTrip = "[Main].[SetTrip]";

        /// <summary>Publishes a host's own draft, re-checking the destination under lock.</summary>
        public const string SetTripPublished = "[Main].[SetTripPublished]";

        /// <summary>Every trip one host runs.</summary>
        public const string QueryHostTrips = "[Main].[QueryHostTrips]";

        /// <summary>A traveller asks to join; one live request per trip.</summary>
        public const string AddJoinRequest = "[Main].[AddJoinRequest]";

        /// <summary>Approves a request and holds a seat atomically.</summary>
        public const string SetJoinRequestApproved = "[Main].[SetJoinRequestApproved]";

        /// <summary>Declines a pending request.</summary>
        public const string SetJoinRequestDeclined = "[Main].[SetJoinRequestDeclined]";

        /// <summary>A traveller withdraws a request, releasing an unpaid seat.</summary>
        public const string SetJoinRequestCancelled = "[Main].[SetJoinRequestCancelled]";

        /// <summary>A trip's requests for its host.</summary>
        public const string QueryTripJoinRequests = "[Main].[QueryTripJoinRequests]";

        /// <summary>Stores a batch of notifications, skipping duplicates.</summary>
        public const string AddNotifications = "[Main].[AddNotifications]";

        /// <summary>Cancels trips (by a host, or a closure) and everything hanging off them.</summary>
        public const string SetTripsCancelled = "[Main].[SetTripsCancelled]";

        /// <summary>Marks finished trips completed, returning everyone who travelled.</summary>
        public const string SetTripsCompleted = "[Main].[SetTripsCompleted]";

        /// <summary>Status, gender, roles and strongest passed identity check, for authorization.</summary>
        public const string GetUserAccess = "[Main].[GetUserAccess]";

        /// <summary>The signed-in user's own profile with roles and verification level.</summary>
        public const string GetUserProfile = "[Main].[GetUserProfile]";

        /// <summary>Saves the account fields and the profile row in one transaction.</summary>
        public const string SetUserProfile = "[Main].[SetUserProfile]";

        /// <summary>Records an identity check, enforcing one national ID per account.</summary>
        public const string AddVerification = "[Main].[AddVerification]";

        /// <summary>Settles a pending check from a provider callback or the admin desk.</summary>
        public const string SetVerificationSettled = "[Main].[SetVerificationSettled]";

        /// <summary>The admin verification queue.</summary>
        public const string QueryVerificationQueue = "[Main].[QueryVerificationQueue]";
    }

    public static class Pay
    {
        public const string Schema = "Pay";

        /// <summary>Releases every seat hold past its payment deadline, once.</summary>
        public const string SetBookingHoldsExpired = "[Pay].[SetBookingHoldsExpired]";

        /// <summary>A traveller's own requests and bookings.</summary>
        public const string QueryMyBookings = "[Pay].[QueryMyBookings]";

        /// <summary>Starts (or replays, by idempotency key) a payment attempt for a held booking.</summary>
        public const string AddPayment = "[Pay].[AddPayment]";

        /// <summary>Records the gateway session and redirect for an attempt.</summary>
        public const string SetPaymentPending = "[Pay].[SetPaymentPending]";

        /// <summary>Marks an attempt that is still in flight as failed.</summary>
        public const string SetPaymentFailed = "[Pay].[SetPaymentFailed]";

        /// <summary>Settles a verified payment: ledger Hold and booking confirmation, atomically.</summary>
        public const string SetPaymentSucceeded = "[Pay].[SetPaymentSucceeded]";

        /// <summary>Stores a gateway callback once.</summary>
        public const string AddWebhookEvent = "[Pay].[AddWebhookEvent]";

        /// <summary>Takes money out of escrow for a refund, capped at the balance, atomically.</summary>
        public const string AddRefund = "[Pay].[AddRefund]";

        /// <summary>A traveller's own booking as the checkout page shows it.</summary>
        public const string GetBookingCheckout = "[Pay].[GetBookingCheckout]";

        /// <summary>One booking with its payments, refunds and escrow totals, for the admin desk.</summary>
        public const string GetAdminBooking = "[Pay].[GetAdminBooking]";

        /// <summary>Payment history for a traveller, a host or the admin desk, with totals.</summary>
        public const string QueryPayments = "[Pay].[QueryPayments]";

        /// <summary>One payment with its refunds (and, for the admin desk, its gateway callbacks).</summary>
        public const string GetPayment = "[Pay].[GetPayment]";

        /// <summary>Creates a batch of refunds with their ledger entries, capped per booking.</summary>
        public const string AddRefunds = "[Pay].[AddRefunds]";

        /// <summary>Records the gateway's answers for a batch of refunds.</summary>
        public const string SetRefundResults = "[Pay].[SetRefundResults]";

        /// <summary>Releases every payout stage that has fallen due, for all trips.</summary>
        public const string SetDuePayouts = "[Pay].[SetDuePayouts]";

        /// <summary>A host's payouts, or the admin queue by status.</summary>
        public const string QueryHostPayouts = "[Pay].[QueryHostPayouts]";

        /// <summary>What the refund rules need for a traveller cancelling their own booking.</summary>
        public const string GetBookingCancellation = "[Pay].[GetBookingCancellation]";

        /// <summary>Closes a traveller's own paid booking and hands the seat back.</summary>
        public const string SetBookingCancelled = "[Pay].[SetBookingCancelled]";
    }

    public static class Social
    {
        public const string Schema = "Social";

        /// <summary>Chat membership and whether numbers must be masked.</summary>
        public const string GetChatAccess = "[Social].[GetChatAccess]";

        /// <summary>A page of chat history plus the pinned announcements.</summary>
        public const string QueryChatMessages = "[Social].[QueryChatMessages]";

        /// <summary>Unread chat counts across a user's trips.</summary>
        public const string QueryChatUnread = "[Social].[QueryChatUnread]";

        /// <summary>The feed, or one author's posts, with their ready media.</summary>
        public const string QueryPosts = "[Social].[QueryPosts]";

        /// <summary>Publishes a post and attaches the author's own media.</summary>
        public const string AddPost = "[Social].[AddPost]";

        /// <summary>The author edits their own post: text, destination, which media stay.</summary>
        public const string SetPost = "[Social].[SetPost]";

        /// <summary>Records a review after a completed trip and updates the rating summary.</summary>
        public const string AddReview = "[Social].[AddReview]";

        /// <summary>Who a user may review on a completed trip.</summary>
        public const string QueryReviewable = "[Social].[QueryReviewable]";

        /// <summary>A person's public page: profile, hosted trips, reviews received.</summary>
        public const string GetPublicProfile = "[Social].[GetPublicProfile]";
    }

    public static class Safety
    {
        public const string Schema = "Safety";

        /// <summary>Raises an SOS for someone on a trip, returning context and the nearest help.</summary>
        public const string AddSosEvent = "[Safety].[AddSosEvent]";

        /// <summary>Police stations and hospitals, for the safety desk.</summary>
        public const string QueryEmergencyPoints = "[Safety].[QueryEmergencyPoints]";

        /// <summary>Adds or edits an emergency point.</summary>
        public const string SetEmergencyPoint = "[Safety].[SetEmergencyPoint]";

        /// <summary>The safety desk's live SOS board.</summary>
        public const string QuerySosBoard = "[Safety].[QuerySosBoard]";

        /// <summary>Marks overdue check-ins missed, once.</summary>
        public const string SetCheckInsMissed = "[Safety].[SetCheckInsMissed]";

        /// <summary>Changes a destination's status and records the alert.</summary>
        public const string SetDestinationStatus = "[Safety].[SetDestinationStatus]";

        /// <summary>The moderation queue.</summary>
        public const string QueryReports = "[Safety].[QueryReports]";

        /// <summary>Counts for the admin overview.</summary>
        public const string GetDashboardCounts = "[Safety].[GetDashboardCounts]";
    }
}
