using System.Net;
using System.Net.Http.Json;
using Dapper;
using Ghurify.Infrastructure.Payments;
using Microsoft.Extensions.DependencyInjection;

namespace Ghurify.IntegrationTests.Infrastructure;

/// <summary>
/// The usual starting points, built through the real API: a verified host's live trip with a
/// traveller whose seat is held, or already paid through the sandbox gateway.
/// </summary>
public static class Scenes
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public sealed record Scene(TestUser Host, TestUser Traveller, long TripId, long BookingId);

    public static async Task<Scene> HeldBookingAsync(TestData data, GhurifyApiFactory api, short seats = 10, int startIn = 20, string destination = "sajek")
    {
        ArgumentNullException.ThrowIfNull(data);
        var host = await data.CreateVerifiedHostAsync();
        var tripId = await data.CreatePublishedTripAsync(host, seats, startIn: startIn, destination: destination);
        var traveller = await data.CreateVerifiedTravelerAsync();
        var bookingId = await HoldAsync(api, host, traveller, tripId);
        return new Scene(host, traveller, tripId, bookingId);
    }

    /// <summary>A held seat for a new traveller on an existing trip. Returns the booking id.</summary>
    public static async Task<long> HeldBookingForTripAsync(TestData data, GhurifyApiFactory api, TestUser host, long tripId)
    {
        ArgumentNullException.ThrowIfNull(data);
        var traveller = await data.CreateVerifiedTravelerAsync();
        return await HoldAsync(api, host, traveller, tripId);
    }

    /// <summary>A seat paid in full (6,000 + 120 fee) through the sandbox gateway.</summary>
    public static async Task<Scene> PaidBookingAsync(TestData data, GhurifyApiFactory api, int startIn = 20, string destination = "sajek")
    {
        ArgumentNullException.ThrowIfNull(api);
        var scene = await HeldBookingAsync(data, api, startIn: startIn, destination: destination);

        using var client = TestData.ClientFor(api, scene.Traveller);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/bookings/{scene.BookingId}/payments");
        request.Headers.Add("Idempotency-Key", "scene-" + Guid.NewGuid().ToString("N"));
        using var started = await client.SendAsync(request, Token);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        await using var connection = await data.OpenAsync();
        var payment = await connection.QuerySingleAsync<(string Reference, decimal Total)>(new CommandDefinition(
            "SELECT TOP (1) [TransactionRef], [Total] FROM [Pay].[Payment] WHERE [BookingId] = @Id ORDER BY [Id] DESC;",
            new { Id = scene.BookingId },
            cancellationToken: Token));

        var gateway = api.Services.GetRequiredService<FakePaymentGateway>();
        using var anonymous = api.CreateClient();
        using var webhook = await anonymous.PostAsJsonAsync(
            "/api/v1/payments/webhooks/fake", gateway.SignedCallback(payment.Reference, succeeded: true, payment.Total), Token);
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        return scene;
    }

    private static async Task<long> HoldAsync(GhurifyApiFactory api, TestUser host, TestUser traveller, long tripId)
    {
        using var travellerClient = TestData.ClientFor(api, traveller);
        using var requested = await travellerClient.PostAsJsonAsync($"/api/v1/trips/{tripId}/join-requests", new { message = (string?)null }, Token);
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var requestId = (await requested.Content.ReadFromJsonAsync<IdResponse>(TestData.Json, Token))!.Id;

        using var hostClient = TestData.ClientFor(api, host);
        using var approved = await hostClient.PostAsync(new Uri($"/api/v1/join-requests/{requestId}/approve", UriKind.Relative), null, Token);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        return (await approved.Content.ReadFromJsonAsync<ApprovedResponse>(TestData.Json, Token))!.BookingId;
    }

    private sealed record IdResponse(long Id);

    private sealed record ApprovedResponse(long BookingId);
}
