namespace Ghurify.Api.Middleware;

/// <summary>
/// Hardening headers on every response.
///
/// API responses are JSON, so their policy is as tight as it gets: nothing may be loaded from or
/// framed around one, the browser must not guess content types, and no referrer leaves for
/// another site.
///
/// When the API is also serving the built web app (the single-origin mode used for LAN and tunnel
/// hosting: see <c>Hosting:ServeWebApp</c> and <c>Serve-Site.ps1</c>) those responses are pages,
/// and a page needs its own stylesheet, scripts and map tiles. Those get <see cref="PageCsp"/>,
/// which is the same policy the Static Web App applies in the deployed setup
/// (<c>public/staticwebapp.config.json</c>), so what is tested locally matches what ships.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>Paths that answer with data rather than a page.</summary>
    private static readonly string[] ApiPrefixes = ["/api", "/hubs", "/openapi"];

    /// <summary>
    /// The web app's policy, deliberately the same shape as the one in
    /// <c>web/ghurify-web/public/staticwebapp.config.json</c>, which is what the deployed site
    /// gets: change one and change the other. This one allows <c>ws:</c>/<c>wss:</c> without a
    /// host, because the hub address here is whatever LAN address or tunnel URL is in use, while
    /// the deployed policy names the domain. 'unsafe-inline' is needed for styles only, because Google
    /// Fonts and the theme's own custom properties are applied as inline style; scripts have no
    /// such allowance.
    /// </summary>
    internal const string PageCsp =
        "default-src 'self'; "
        + "script-src 'self'; "
        + "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; "
        + "font-src 'self' https://fonts.gstatic.com; "
        + "img-src 'self' data: blob: https://*.tile.openstreetmap.org; "
        + "connect-src 'self' ws: wss:; "
        + "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    /// <summary>Nothing may be loaded from, or framed around, a JSON response.</summary>
    internal const string DataCsp = "default-src 'none'; frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";

            var isApi = IsApiPath(context.Request.Path);

            // A page keeps its origin on outbound links (analytics and support links rely on it);
            // an API response still gives nothing away.
            headers["Referrer-Policy"] = isApi ? "no-referrer" : "strict-origin-when-cross-origin";

            // The map and the SOS screen ask the browser for a location, and the ID-photo step for
            // the camera, so a page may request those. An API response may not.
            headers["Permissions-Policy"] = isApi
                ? "camera=(), microphone=(), geolocation=()"
                : "geolocation=(self), camera=(self), microphone=()";

            if (!headers.ContainsKey("Content-Security-Policy"))
            {
                headers.ContentSecurityPolicy = isApi ? DataCsp : PageCsp;
            }

            return Task.CompletedTask;
        });

        return next(context);
    }

    private static bool IsApiPath(PathString path)
    {
        foreach (var prefix in ApiPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
