using System.Net;
using System.Net.Http.Json;
using Ghurify.IntegrationTests.Infrastructure;

namespace Ghurify.IntegrationTests;

/// <summary>
/// End-to-end check of the skeleton: the API starts, the pipeline runs, and the health
/// endpoint reaches the database that was built from the dacpac.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class HealthEndpointTests(SqlServerFixture database)
{
    [Fact]
    public async Task GetHealth_WhenDatabaseIsReachable_ReportsHealthy()
    {
        await using var factory = new GhurifyApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal("Healthy", body.Status);
        Assert.Equal("Healthy", body.DatabaseStatus);
    }

    [Fact]
    public async Task GetHealth_WhenDatabaseIsUnreachable_Reports503WithProblemDetails()
    {
        // A valid but dead endpoint: the API must report unavailable rather than throw.
        const string Unreachable =
            "Server=127.0.0.1,14333;Database=Ghurify;User Id=sa;Password=NotTheRealPassword_1;"
            + "TrustServerCertificate=True;Encrypt=False;Connect Timeout=3;";

        await using var factory = new GhurifyApiFactory(Unreachable);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(problem);
        Assert.Equal("Service unavailable", problem.Title);
        Assert.Equal(503, problem.Status);
    }

    [Fact]
    public async Task Readiness_ReportsEachDependency_AndOptionalOnesDoNotFailIt()
    {
        // The test host has no blob storage and no SMTP account: both optional.
        await using var factory = new GhurifyApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReadinessResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(new ReadinessResponse("Healthy", "Healthy", "NotConfigured", "NotConfigured", "Inline"), body);
    }

    [Fact]
    public async Task Readiness_WithoutTheDatabase_Is503()
    {
        const string Unreachable =
            "Server=127.0.0.1,14333;Database=Ghurify;User Id=sa;Password=NotTheRealPassword_1;"
            + "TrustServerCertificate=True;Encrypt=False;Connect Timeout=3;";

        await using var factory = new GhurifyApiFactory(Unreachable);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", (await response.Content.ReadFromJsonAsync<ReadinessResponse>(TestContext.Current.CancellationToken))!.Database);
    }

    private sealed record HealthResponse(string Status, string DatabaseStatus);

    private sealed record ReadinessResponse(string Status, string Database, string Storage, string Email, string Jobs);

    private sealed record ProblemDetailsResponse(string? Title, int? Status, string? Detail);
}
