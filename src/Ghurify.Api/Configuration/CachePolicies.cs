using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Net.Http.Headers;

namespace Ghurify.Api.Configuration;

/// <summary>
/// Server-side caching for the handful of reads that are the same for every visitor.
///
/// The public lists (destinations, the trip search) are what a first-time visitor loads before
/// anything is personalised, and they are identical for everybody. Answering them from memory
/// takes the database out of that path entirely; the lifetimes are short enough that a host
/// publishing a trip sees it almost at once.
///
/// Nothing user-specific is ever cached: <see cref="Anonymous"/> refuses any request that carries
/// credentials, so a signed-in caller always gets a freshly built answer and one person's data can
/// never be served to another.
/// </summary>
public static class CachePolicies
{
    /// <summary>Destinations: effectively a lookup table, changed by an admin now and then.</summary>
    public const string Destinations = "cache-destinations";

    /// <summary>
    /// The trip search. Short, because the seat counts in it move as people book.
    /// </summary>
    public const string TripSearch = "cache-trip-search";

    public static OutputCacheOptions AddGhurifyPolicies(this OutputCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // A burst of identical requests (every visitor's first page load) collapses onto one
        // database read rather than one each.
        options.AddBasePolicy(builder => builder.NoCache(), excludeDefaultPolicy: true);

        options.AddPolicy(Destinations, builder => Anonymous(builder).Expire(TimeSpan.FromMinutes(2)));
        options.AddPolicy(TripSearch, builder => Anonymous(builder).Expire(TimeSpan.FromSeconds(15)));

        return options;
    }

    /// <summary>
    /// Caches only plain anonymous GETs, keyed by the whole query string so two different
    /// searches never share an answer.
    /// </summary>
    private static OutputCachePolicyBuilder Anonymous(OutputCachePolicyBuilder builder) =>
        builder
            .SetVaryByQuery("*")
            // Content negotiation and language both change the body.
            .SetVaryByHeader(HeaderNames.AcceptLanguage, HeaderNames.Accept)
            .With(context =>
                HttpMethods.IsGet(context.HttpContext.Request.Method)
                // A credentialled request is personal by definition: never cached, never served
                // from the cache. Cookies are checked too because the refresh token is one.
                && !context.HttpContext.Request.Headers.ContainsKey(HeaderNames.Authorization)
                && context.HttpContext.Request.Cookies.Count == 0
                && context.HttpContext.User.Identity?.IsAuthenticated != true);
}
