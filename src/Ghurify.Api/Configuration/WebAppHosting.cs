using Microsoft.Extensions.FileProviders;

namespace Ghurify.Api.Configuration;

/// <summary>
/// Serves the built web app from this process, so the site and the API share one origin.
/// Opt-in; see <see cref="WebAppOptions"/> for when and why.
///
/// Split into two halves on purpose, because the order matters:
/// <see cref="UseBuiltWebApp"/> goes early, before routing, and <see cref="MapWebAppFallback"/>
/// goes last, after every API endpoint. See each for the reason.
/// </summary>
internal static class WebAppHosting
{
    /// <summary>
    /// Serves the files, and reports the provider to hand to <see cref="MapWebAppFallback"/>, or
    /// null when this process is API-only (the default, and how it is deployed).
    ///
    /// Called before routing. Static files cannot be served after it: once routing has matched
    /// the catch-all that <see cref="MapWebAppFallback"/> adds, the static-file middleware stands
    /// down rather than compete with the chosen endpoint, and the request would fall through to
    /// that endpoint for every asset.
    ///
    /// Caching is split by what the file name promises. Vite gives everything under
    /// <c>/assets</c> a content hash, so those can be cached for a year and never revalidated: a
    /// changed file has a different name. <c>index.html</c> names those files, so it must be
    /// revalidated every time, or a browser would keep loading the previous release's bundle.
    /// </summary>
    internal static IFileProvider? UseBuiltWebApp(WebApplication app)
    {
        var hosting = app.Configuration.GetSection(WebAppOptions.SectionName).Get<WebAppOptions>()
            ?? new WebAppOptions();

        if (!hosting.ServeWebApp)
        {
            return null;
        }

        var root = Path.Combine(app.Environment.ContentRootPath, hosting.WebRoot);

        if (!File.Exists(Path.Combine(root, "index.html")))
        {
            app.Logger.LogWarning(
                "Hosting:ServeWebApp is on but no built web app was found at {Root}. Serving the "
                + "API only. Run Serve-Site.ps1, or 'npm run build' and copy dist there.",
                root);
            return null;
        }

        var files = new PhysicalFileProvider(root);

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(OptionsFor(files));

        app.Logger.LogInformation("Serving the built web app from {Root} on this origin.", root);
        return files;
    }

    /// <summary>
    /// How the web app's files are served. Shared by the middleware and by the fallback endpoint,
    /// because both of them can be the one that answers with <c>index.html</c> (the middleware for
    /// <c>/index.html</c>, the endpoint for <c>/</c> and for every client-side route) and the
    /// caching rule has to be the same either way.
    /// </summary>
    private static StaticFileOptions OptionsFor(IFileProvider files) => new()
    {
        FileProvider = files,
        OnPrepareResponse = context =>
        {
            var hashedName = context.Context.Request.Path
                .StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase);

            context.Context.Response.Headers.CacheControl = hashedName
                ? "public, max-age=31536000, immutable"
                : "no-cache";
        },
    };

    /// <summary>
    /// Answers every path no API endpoint claimed with <c>index.html</c>, so a client-side route
    /// (<c>/trips/12</c>, <c>/admin/payouts</c>) can be opened directly or refreshed.
    ///
    /// Anonymous explicitly: the authorization fallback policy demands a signed-in user for any
    /// endpoint that does not say otherwise, which is right for the API and wrong for the page
    /// that contains the sign-in form. What the page may then *do* is unchanged — every API call
    /// it makes is still authorized, and the admin screens are still behind their permissions.
    /// </summary>
    internal static void MapWebAppFallback(WebApplication app, IFileProvider? files)
    {
        if (files is null)
        {
            return;
        }

        app.MapFallbackToFile("index.html", OptionsFor(files)).AllowAnonymous();
    }
}
