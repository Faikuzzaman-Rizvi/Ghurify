using Ghurify.Application.Social;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Middleware;

/// <summary>
/// Blob storage could not be reached: answers 503 ProblemDetails with code
/// <c>storage_unavailable</c> and a Retry-After, which the web app shows as "photos cannot be
/// saved right now, try again in a minute". Logged as a warning: it is an outage to look at, not
/// a bug in the request.
/// </summary>
public sealed class StorageUnavailableHandler(
    IProblemDetailsService problemDetails,
    ILogger<StorageUnavailableHandler> logger) : IExceptionHandler
{
    private const int RetryAfterSeconds = 30;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not StorageUnavailableException)
        {
            return false;
        }

        logger.LogWarning(exception, "{Method} {Path} failed: blob storage is not reachable.", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        httpContext.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Storage unavailable",
                Detail = "Photos cannot be saved or shown right now. Please try again in a minute.",
                Extensions =
                {
                    ["code"] = "storage_unavailable",
                    ["retryAfterSeconds"] = RetryAfterSeconds,
                },
            },
        });
    }
}
