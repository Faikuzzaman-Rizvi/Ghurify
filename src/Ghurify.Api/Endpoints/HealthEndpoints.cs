using Ghurify.Application.Abstractions;
using Ghurify.Infrastructure.Email;
using Ghurify.Infrastructure.Jobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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

        // For deploy smoke tests and the load balancer: every dependency, one by one. Only the
        // database is required; storage and email are optional and report Degraded when broken.
        group.MapGet("/ready", GetReadinessAsync)
            .WithName("GetReadiness")
            .WithSummary("Reports each dependency: database (required), blob storage and email (optional), jobs.")
            .Produces<ReadinessResponse>(StatusCodes.Status200OK)
            .Produces<ReadinessResponse>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> GetReadinessAsync(
        [FromServices] IDatabaseHealthProbe database,
        [FromServices] IStorageHealthProbe storage,
        [FromServices] IOptions<EmailOptions> email,
        [FromServices] IOptions<JobsOptions> jobs,
        CancellationToken cancellationToken)
    {
        var databaseHealthy = await database.CanReachDatabaseAsync(cancellationToken);
        var storageHealth = await storage.CheckAsync(cancellationToken);

        var status = !databaseHealthy ? "Unhealthy" : storageHealth == ComponentHealth.Unhealthy ? "Degraded" : "Healthy";
        var response = new ReadinessResponse(
            status,
            databaseHealthy ? nameof(ComponentHealth.Healthy) : nameof(ComponentHealth.Unhealthy),
            storageHealth.ToString(),
            email.Value.IsConfigured ? "Configured" : nameof(ComponentHealth.NotConfigured),
            jobs.Value.Enabled ? (jobs.Value.RunServer ? "Running" : "QueueOnly") : "Inline");

        return databaseHealthy
            ? Results.Ok(response)
            : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
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

    public sealed record ReadinessResponse(string Status, string Database, string Storage, string Email, string Jobs);
}
