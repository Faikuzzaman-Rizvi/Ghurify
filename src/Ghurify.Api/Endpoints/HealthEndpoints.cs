using Ghurify.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// Liveness and readiness for the API. Anonymous on purpose: load balancers and uptime
/// checks call it without credentials.
/// </summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/health")
            .WithTags("Health")
            .AllowAnonymous();

        group.MapGet("/", GetHealthAsync)
            .WithName("GetHealth")
            .WithSummary("Reports whether the API is running and can reach the database.")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> GetHealthAsync(
        [FromServices] IDatabaseHealthProbe probe,
        CancellationToken cancellationToken)
    {
        var databaseReachable = await probe.CanReachDatabaseAsync(cancellationToken);

        if (!databaseReachable)
        {
            // The caller learns the API is not ready; the cause stays in the logs.
            return Results.Problem(
                title: "Service unavailable",
                detail: "The API cannot reach the database.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new HealthResponse("Healthy", DatabaseStatus: "Healthy"));
    }

    /// <summary>Shape of the health response. Deliberately free of version or host detail.</summary>
    public sealed record HealthResponse(string Status, string DatabaseStatus);
}
