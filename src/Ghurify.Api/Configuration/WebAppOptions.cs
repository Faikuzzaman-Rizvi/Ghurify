namespace Ghurify.Api.Configuration;

/// <summary>
/// Whether this process also serves the built web app, so the site and the API share one origin.
///
/// Off by default, and off in the deployed setup: there the web app is an Azure Static Web App on
/// its own domain and this process is the API only (see docs/RUNBOOK.md). Turning it on is for
/// showing the site to a phone on the same Wi-Fi, or through an ngrok tunnel, where one origin
/// means no CORS list to keep up to date with changing addresses, no second server to run, and a
/// first-party sign-in cookie — which is what the browser requires for the refresh token to
/// survive a reload.
/// </summary>
public sealed class WebAppOptions
{
    public const string SectionName = "Hosting";

    /// <summary>
    /// Serves <see cref="WebRoot"/> for every path the API does not own, falling back to
    /// <c>index.html</c> so the client-side routes survive a refresh. Ignored when the folder is
    /// not there, so a build that never copied the web app in still starts as an API.
    /// </summary>
    public bool ServeWebApp { get; set; }

    /// <summary>
    /// Where the built web app is, relative to the content root. <c>Serve-Site.ps1</c> builds
    /// <c>web/ghurify-web</c> into it.
    /// </summary>
    public string WebRoot { get; set; } = "wwwroot";
}
