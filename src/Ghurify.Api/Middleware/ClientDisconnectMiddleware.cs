namespace Ghurify.Api.Middleware;

/// <summary>
/// Swallows the cancellation that happens when the caller goes away mid-request.
///
/// When a browser navigates away, reloads, or React drops a query, the HTTP connection is
/// aborted. ASP.NET Core signals that through <see cref="HttpContext.RequestAborted"/>, and
/// anything taking that token — a SQL connection being opened, a query in flight — throws
/// <see cref="OperationCanceledException"/>. That is the system working correctly: the work
/// was abandoned because nobody is waiting for the answer any more.
///
/// Left alone it still looks like a fault. The global exception handler turns it into a 500,
/// Serilog records an error, and Visual Studio breaks on it as "user-unhandled" — which is
/// exactly what you see in development, where React's StrictMode mounts every component twice
/// and aborts the first request every time.
///
/// Catching it here, close to the endpoint, keeps a disconnected client out of the error path.
/// It deliberately does NOT catch cancellation caused by anything else: a server-side timeout
/// is a real failure and must still surface.
/// </summary>
public sealed class ClientDisconnectMiddleware(RequestDelegate next, ILogger<ClientDisconnectMiddleware> logger)
{
    /// <summary>
    /// 499 "Client Closed Request". Non-standard, from nginx, and widely understood by log
    /// tooling. Nothing receives it — the client has already gone — but it keeps the request
    /// out of the 5xx bucket that alerting watches.
    /// </summary>
    private const int ClientClosedRequest = 499;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await next(context);
        }
        // Not only OperationCanceledException: SQL Server reports a cancelled command as a
        // SqlException ("the batch is aborted"). Once the caller is gone, whatever failed did so
        // because they left, and there is nobody to report it to.
#pragma warning disable CA1031 // Deliberately broad, and only while the request is already aborted.
        catch (Exception) when (context.RequestAborted.IsCancellationRequested)
#pragma warning restore CA1031
        {
            // Debug, not warning: on a busy site this is constant and completely normal.
            logger.LogDebug(
                "{Method} {Path} was abandoned by the caller before it finished.",
                context.Request.Method,
                context.Request.Path);

            // Only safe if nothing has been written yet; otherwise the status is already sent.
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = ClientClosedRequest;
            }
        }
    }
}

public static class ClientDisconnectMiddlewareExtensions
{
    /// <summary>
    /// Register this INSIDE the exception handler (that is, after <c>UseExceptionHandler</c>),
    /// so an abandoned request is dealt with before the global handler ever sees it.
    /// </summary>
    public static IApplicationBuilder UseClientDisconnectHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ClientDisconnectMiddleware>();
}
