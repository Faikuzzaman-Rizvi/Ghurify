namespace Ghurify.Api.Middleware;

/// <summary>
/// Hardening headers on every API response. The API serves JSON, never pages, so the policy is as
/// tight as it gets: nothing may be loaded from or framed around a response, the browser must not
/// guess content types, and no referrer leaves for another site.
///
/// The web app is served separately (static hosting); its own headers are set there (see
/// docs/RUNBOOK.md).
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers.XFrameOptions = "DENY";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            // The OpenAPI document is read by tools, not rendered; everything else is data.
            if (!headers.ContainsKey("Content-Security-Policy"))
            {
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
