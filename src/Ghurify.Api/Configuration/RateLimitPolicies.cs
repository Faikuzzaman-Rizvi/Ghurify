namespace Ghurify.Api.Endpoints;

/// <summary>
/// Named rate-limit policies. Auth, join requests and payments each get their own, because
/// the cost of abusing them differs.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Sign-in endpoints. This caps requests per caller IP; the real per-address limit
    /// (3 codes per 10 minutes) is enforced in the database, where it is accurate across
    /// restarts and across every API instance.
    /// </summary>
    public const string Auth = "auth";

    /// <summary>
    /// Refresh, logout and whoami: called on every page load, so far more generous than Auth.
    /// Sharing Auth's budget signed people out when a few of them shared one IP (a mobile
    /// carrier's NAT) or opened several pages quickly.
    /// </summary>
    public const string Session = "session";

    /// <summary>
    /// Starting an identity check costs money at the e-KYC provider and invites NID guessing,
    /// so it is limited per signed-in user, not per IP.
    /// </summary>
    public const string Verification = "verification";

    /// <summary>Join requests, per user: stops one account spamming every host at once.</summary>
    public const string JoinRequests = "join-requests";

    /// <summary>Starting payments, per user.</summary>
    public const string Payments = "payments";

    /// <summary>Writing content (posts, comments, chat), per user.</summary>
    public const string Content = "content";
}
