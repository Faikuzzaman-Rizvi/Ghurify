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
}
