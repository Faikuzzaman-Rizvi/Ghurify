using System.Globalization;
using System.Security.Claims;
using Ghurify.Api.Authorization;
using Ghurify.Application.Site;
using Ghurify.Domain.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// The site's own configuration: what it is called, how to reach it, what colour it is.
///
/// Two halves. Reading it is anonymous, because the header and the footer are painted before
/// anybody has signed in; changing it needs <c>settings.branding</c> or <c>settings.theme</c>.
/// </summary>
public static class SiteEndpoints
{
    /// <summary>
    /// How long a browser may keep an uploaded logo. Long, because the URL carries the version:
    /// a replaced image has a different address, so a stale one is never shown.
    /// </summary>
    private const int AssetCacheSeconds = 60 * 60 * 24 * 30;

    public static IEndpointRouteBuilder MapSiteEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var site = app.MapGroup("/api/v1/site").WithTags("Site");

        site.MapGet("/config", GetConfigAsync)
            .WithName("GetSiteConfig")
            .WithSummary("The site's name, contact details, theme and images. Public: every page needs it.")
            .AllowAnonymous()
            .Produces<SiteConfig>();

        site.MapGet("/assets/{kind}", GetAssetAsync)
            .WithName("GetSiteAsset")
            .WithSummary("One of the site's uploaded images. Public, cached, and versioned by its URL.")
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK, contentType: "image/png")
            .Produces(StatusCodes.Status404NotFound);

        var settings = app.MapGroup("/api/v1/admin/settings")
            .WithTags("Admin")
            .RequireAuthorization(Policies.Staff);

        settings.MapGet("/", GetSettingsAsync)
            .WithName("GetSiteSettings")
            .WithSummary("Every setting with its value and its default, the fonts on offer, and the state of each image.")
            .Produces<SettingsView>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        settings.MapPut("/", SaveSettingsAsync)
            .WithName("SaveSiteSettings")
            .WithSummary("Saves a batch of settings. All or nothing, and refused if the colours would be unreadable.")
            .Produces<SettingsSaved>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        settings.MapGet("/history/{key}", GetHistoryAsync)
            .WithName("GetSiteSettingHistory")
            .WithSummary("What one setting used to be, and who changed it.")
            .Produces<IReadOnlyList<SettingChangeRecord>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        settings.MapPut("/assets/{kind}", UploadAssetAsync)
            .WithName("UploadSiteAsset")
            .WithSummary("Replaces the logo, an icon or the link-preview picture.")
            .RequireAuthorization(Policies.Require(Permissions.SettingsBranding))
            .RequireRateLimiting(RateLimitPolicies.Content)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        settings.MapDelete("/assets/{kind}", RemoveAssetAsync)
            .WithName("RemoveSiteAsset")
            .WithSummary("Puts one image back to the one the app shipped with.")
            .RequireAuthorization(Policies.Require(Permissions.SettingsBranding))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>
    /// The configuration, with an ETag so a browser that already has the current one is answered
    /// with 304 rather than the whole document. The tag is the configuration's own version, which
    /// is a hash of everything stored, so it is stable across restarts and across instances.
    /// </summary>
    private static async Task<IResult> GetConfigAsync(
        HttpContext context,
        [FromServices] GetSiteConfigHandler handler,
        CancellationToken cancellationToken)
    {
        var config = await handler.HandleAsync(cancellationToken);
        var tag = $"\"{config.Version}\"";

        if (context.Request.Headers.IfNoneMatch.Any(value => value == tag))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        context.Response.Headers.ETag = tag;
        // Revalidate every time: the tag makes that cheap, and a branding change should not wait
        // behind a cached copy. Private, because a proxy serving one site's config for another
        // would be worse than a few extra requests.
        context.Response.Headers.CacheControl = "private, no-cache";

        return Results.Ok(config);
    }

    private static async Task<IResult> GetAssetAsync(
        string kind,
        HttpContext context,
        [FromServices] GetSiteAssetHandler handler,
        CancellationToken cancellationToken)
    {
        var asset = await handler.HandleAsync(kind, cancellationToken);

        if (asset is null)
        {
            // Either an unknown kind or one that has never been replaced. The configuration
            // points at the app's own file in that case, so nothing should ask for this.
            return Results.NotFound();
        }

        var tag = new EntityTagHeaderValue(
            $"\"{asset.UpdatedOn.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}\"");

        context.Response.Headers.CacheControl =
            $"public, max-age={AssetCacheSeconds.ToString(CultureInfo.InvariantCulture)}, immutable";

        return Results.File(
            asset.Bytes,
            contentType: asset.ContentType,
            lastModified: asset.UpdatedOn,
            entityTag: tag);
    }

    private static async Task<IResult> GetSettingsAsync(
        ClaimsPrincipal principal,
        [FromServices] GetSettingsHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    private static async Task<IResult> SaveSettingsAsync(
        SaveSettingsCommand command,
        ClaimsPrincipal principal,
        [FromServices] SaveSettingsHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), command, cancellationToken));

    private static async Task<IResult> GetHistoryAsync(
        string key,
        ClaimsPrincipal principal,
        [FromServices] GetSettingHistoryHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), key, cancellationToken));

    private static async Task<IResult> UploadAssetAsync(
        string kind,
        UploadSiteAssetCommand command,
        ClaimsPrincipal principal,
        [FromServices] UploadSiteAssetHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), kind, command, cancellationToken));

    private static async Task<IResult> RemoveAssetAsync(
        string kind,
        ClaimsPrincipal principal,
        [FromServices] RemoveSiteAssetHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), kind, cancellationToken));
}
