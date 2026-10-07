using System.Net;
using System.Net.Http.Json;
using Dapper;
using Ghurify.Application.Payments;
using Ghurify.Infrastructure.Payments;
using Ghurify.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Ghurify.IntegrationTests;

/// <summary>
/// Paying into escrow against a real database, with the fake gateway: success, failure, gateway
/// timeouts, duplicate and forged webhooks, amount mismatches, late payments, idempotency, and the
/// ledger balancing for every booking.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class PaymentsEndpointTests(SqlServerFixture database)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Checkout_ShowsThePriceTheFeeAndTheTotal_AndIsHiddenFromOthers()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await HeldBookingAsync(data, api);

        using var client = TestData.ClientFor(api, scene.Traveller);
        var checkout = await client.GetFromJsonAsync<CheckoutResponse>($"/api/v1/bookings/{scene.BookingId}/checkout", TestData.Json, Token);

        Assert.Equal(6000m, checkout!.Amount);
        Assert.Equal(120m, checkout.Fee);
        Assert.Equal(6120m, checkout.Total);
        Assert.Equal("Held", checkout.Status);

        var stranger = await data.CreateVerifiedTravelerAsync();
        using var strangerClient = TestData.ClientFor(api, stranger);
        using var hidden = await strangerClient.GetAsync(new Uri($"/api/v1/bookings/{scene.BookingId}/checkout", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task Pay_Success_ConfirmsTheBookingAndHoldsTheTotalInEscrow()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await HeldBookingAsync(data, api);
        var gateway = api.Services.GetRequiredService<FakePaymentGateway>();

        var started = await StartAsync(api, scene, "key-success-0001");
        Assert.Contains("/payments/sandbox?ref=", started.RedirectUrl, StringComparison.Ordinal);
        Assert.Equal(6120m, started.Total);

        var reference = await ReferenceAsync(data, started.PaymentId);
        using var webhook = await WebhookAsync(api, gateway.SignedCallback(reference, succeeded: true, started.Total));
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        Assert.Equal(2, await BookingStatusAsync(data, scene.BookingId));
        var ledger = await LedgerAsync(api, scene.BookingId);
        var hold = Assert.Single(ledger);
        Assert.Equal(("Hold", 6120m), (hold.EntryType.ToString(), hold.Amount));
        await AssertLedgerBalancedAsync(data);
    }

    [Fact]
    public async Task Pay_Failure_LeavesTheSeatHeldAndMovesNoMoney()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await HeldBookingAsync(data, api);
        var gateway = api.Services.GetRequiredService<FakePaymentGateway>();

        var started = await StartAsync(api, scene, "key-failure-0001");
        var reference = await ReferenceAsync(data, started.PaymentId);
        using var webhook = await WebhookAsync(api, gateway.SignedCallback(reference, succeeded: false, started.Total));

        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        Assert.Equal(1, await BookingStatusAsync(data, scene.BookingId));
        Assert.Equal(4, await PaymentStatusAsync(data, started.PaymentId));
        Assert.Empty(await LedgerAsync(api, scene.BookingId));
    }

    [Fact]
    public async Task Pay_WhenTheGatewayTimesOutOnStart_Returns502AndTheTravellerCanRetry()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await HeldBookingAsync(data, api);
        var gateway = api.Services.GetRequiredService<FakePaymentGateway>();
        using var client = TestData.ClientFor(api, scene.Traveller);

        gateway.FailStart = true;
        using var failed = await PostPaymentAsync(client, scene.BookingId, "key-timeout-0001");
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Equal(1, await BookingStatusAsync(data, scene.BookingId));

        gateway.FailStart = false;
        using var retried = await PostPaymentAsync(client, scene.BookingId, "key-timeout-0002");
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
    }

    [Fact]
    public async Task Webhook_WhenValidationTimesOut_FailsSoTheGatewayRetries_AndTheRetrySettles()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await HeldBookingAsync(data, api);
        var gateway = api.Services.GetRequiredService<FakePaymentGateway>();

        var started = await StartAsync(api, scene, "key-validate-0001");
        var reference = await ReferenceAsync(data, started.PaymentId);
        var callback = gateway.SignedCallback(reference, succeeded: true, started.Total);

        gateway.FailValidation = true;
        using var first = await WebhookAsync(api, callback);
        Assert.Equal(HttpStatusCode.InternalServerError, first.StatusCode);
        Assert.Equal(1, await BookingStatusAsync(data, scene.BookingId));

        gateway.FailValidation = false;
        using var retry = await WebhookAsync(api, callback);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(2, await BookingStatusAsync(data, scene.BookingId));
    }

    [Fact]
    public async Task Webhook_DeliveredTwice_SettlesOnce()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await HeldBookingAsync(data, api);
        var gateway = api.Services.GetRequiredService<FakePaymentGateway>();

        var started = await StartAsync(api, scene, "key-duplicate-01");
        var reference = await ReferenceAsync(data, started.PaymentId);
        var callback = gateway.SignedCallback(reference, succeeded: true, started.Total);

        using var first = await WebhookAsync(api, callback);
        using var second = await WebhookAsync(api, callback);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Single(await LedgerAsync(api, scene.BookingId));

        await using var connection = await data.OpenAsync();
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Main].[Notification] WHERE [UserId] = @Id AND [Kind] = 'booking.confirmed';",
            new { scene.Traveller.Id },
            cancellationToken: Token)));
    }

    [Fact]
    public async Task Webhook_WithAForgedSignature_IsRejectedAndChangesNothing()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await HeldBookingAsync(data, api);
        var gateway = api.Services.GetRequiredService<FakePaymentGateway>();

        var started = await StartAsync(api, scene, "key-forged-00001");
        var reference = await ReferenceAsync(data, started.PaymentId);
        var forged = gateway.SignedCallback(reference, succeeded: true, started.Total);
        forged["signature"] = new string('0', 64);

        using var response = await WebhookAsync(api, forged);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, await BookingStatusAsync(data, scene.BookingId));
        Assert.Empty(await LedgerAsync(api, scene.BookingId));

        await using var connection = await data.OpenAsync();
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [Pay].[WebhookEvent] WHERE [TransactionRef] = @Ref AND [SignatureValid] = 1;",
            new { Ref = reference },
            cancellationToken: Token)));
    }

    [Fact]
    public async Task Webhook_WithTheWrongAmount_DoesNotConfirmAndRefundsWhatWasPaid()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await HeldBookingAsync(data, api);
        var gateway = api.Services.GetRequiredService<FakePaymentGateway>();

        var started = await StartAsync(api, scene, "key-mismatch-001");
        var reference = await ReferenceAsync(data, started.PaymentId);

        // The gateway reports a charge of 10 taka instead of the total.
        using var response = await WebhookAsync(api, gateway.SignedCallback(reference, succeeded: true, amount: 10m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await BookingStatusAsync(data, scene.BookingId));

        var refund = Assert.Single(gateway.Refunds);
        Assert.Equal(10m, refund.Amount);
        await AssertLedgerBalancedAsync(data);
        Assert.Equal(0m, Balance(await LedgerAsync(api, scene.BookingId)));
    }

    [Fact]
    public async Task Pay_WithTheSameIdempotencyKey_ReturnsTheSameAttempt_AndAnotherBookingCannotReuseIt()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await HeldBookingAsync(data, api);
        var other = await HeldBookingAsync(data, api);

        var first = await StartAsync(api, scene, "key-replay-00001");
        var again = await StartAsync(api, scene, "key-replay-00001");
        Assert.Equal(first.PaymentId, again.PaymentId);
        Assert.Equal(first.RedirectUrl, again.RedirectUrl);

        using var otherClient = TestData.ClientFor(api, other.Traveller);
        using var reused = await PostPaymentAsync(otherClient, other.BookingId, "key-replay-00001");
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
    }

    [Fact]
    public async Task Pay_WithoutAnIdempotencyKey_Returns400()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await HeldBookingAsync(data, api);
        using var client = TestData.ClientFor(api, scene.Traveller);

        using var response = await client.PostAsync(new Uri($"/api/v1/bookings/{scene.BookingId}/payments", UriKind.Relative), null, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pay_ForSomeoneElsesBooking_Returns404()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await HeldBookingAsync(data, api);
        var stranger = await data.CreateVerifiedTravelerAsync();
        using var client = TestData.ClientFor(api, stranger);

        using var response = await PostPaymentAsync(client, scene.BookingId, "key-stranger-001");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Pay_AfterTheHoldExpired_IsRefused()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await HeldBookingAsync(data, api);
        await ExpireHoldAsync(data, scene.BookingId);

        using var client = TestData.ClientFor(api, scene.Traveller);
        using var response = await PostPaymentAsync(client, scene.BookingId, "key-expired-0001");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task LatePayment_WhenTheSeatWasTaken_IsRefundedInFull()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await HeldBookingAsync(data, api, seats: 1);
        var gateway = api.Services.GetRequiredService<FakePaymentGateway>();

        var started = await StartAsync(api, scene, "key-late-0000001");
        var reference = await ReferenceAsync(data, started.PaymentId);

        // The hold runs out while the traveller is on the gateway page, and someone else takes the seat.
        await ExpireHoldAsync(data, scene.BookingId);
        await using (var scope = api.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Application.Bookings.ReleaseExpiredHoldsHandler>().HandleAsync(Token);
        }

        await HeldBookingForTripAsync(data, api, scene.Host, scene.TripId);

        using var webhook = await WebhookAsync(api, gateway.SignedCallback(reference, succeeded: true, started.Total));

        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        Assert.Equal(3, await BookingStatusAsync(data, scene.BookingId));
        Assert.Equal(started.Total, Assert.Single(gateway.Refunds).Amount);
        Assert.Equal(0m, Balance(await LedgerAsync(api, scene.BookingId)));
        await AssertLedgerBalancedAsync(data);
    }

    [Fact]
    public async Task LatePayment_WhenTheSeatIsStillFree_TakesItBackAndConfirms()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await HeldBookingAsync(data, api, seats: 2);
        var gateway = api.Services.GetRequiredService<FakePaymentGateway>();

        var started = await StartAsync(api, scene, "key-late-0000002");
        var reference = await ReferenceAsync(data, started.PaymentId);
        await ExpireHoldAsync(data, scene.BookingId);
        await using (var scope = api.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Application.Bookings.ReleaseExpiredHoldsHandler>().HandleAsync(Token);
        }

        using var webhook = await WebhookAsync(api, gateway.SignedCallback(reference, succeeded: true, started.Total));

        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        Assert.Equal(2, await BookingStatusAsync(data, scene.BookingId));
        Assert.Empty(gateway.Refunds);
    }

    [Fact]
    public async Task Return_FromTheGateway_SettlesAndRedirectsToTheResultPage()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await HeldBookingAsync(data, api);
        var gateway = api.Services.GetRequiredService<FakePaymentGateway>();

        var started = await StartAsync(api, scene, "key-return-00001");
        var reference = await ReferenceAsync(data, started.PaymentId);

        using var client = api.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var form = new FormUrlEncodedContent(gateway.SignedCallback(reference, succeeded: true, started.Total));
        using var response = await client.PostAsync(new Uri("/api/v1/payments/return/fake/success", UriKind.Relative), form, Token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith($"/payments/result?booking={scene.BookingId}&outcome=success", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal(2, await BookingStatusAsync(data, scene.BookingId));
    }

    // --- helpers ---------------------------------------------------------------------------

    private sealed record Scene(TestUser Host, TestUser Traveller, long TripId, long BookingId);

    private static async Task<Scene> HeldBookingAsync(TestData data, GhurifyApiFactory api, short seats = 10)
    {
        var host = await data.CreateVerifiedHostAsync();
        var tripId = await data.CreatePublishedTripAsync(host, seats);
        var (traveller, bookingId) = await HeldBookingForTripAsync(data, api, host, tripId);
        return new Scene(host, traveller, tripId, bookingId);
    }

    private static async Task<(TestUser Traveller, long BookingId)> HeldBookingForTripAsync(
        TestData data, GhurifyApiFactory api, TestUser host, long tripId)
    {
        var traveller = await data.CreateVerifiedTravelerAsync();

        using var travellerClient = TestData.ClientFor(api, traveller);
        using var requested = await travellerClient.PostAsJsonAsync($"/api/v1/trips/{tripId}/join-requests", new { message = (string?)null }, Token);
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var requestId = (await requested.Content.ReadFromJsonAsync<IdResponse>(TestData.Json, Token))!.Id;

        using var hostClient = TestData.ClientFor(api, host);
        using var approved = await hostClient.PostAsync(new Uri($"/api/v1/join-requests/{requestId}/approve", UriKind.Relative), null, Token);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var bookingId = (await approved.Content.ReadFromJsonAsync<ApprovedResponse>(TestData.Json, Token))!.BookingId;

        return (traveller, bookingId);
    }

    private static async Task<StartedResponse> StartAsync(GhurifyApiFactory api, Scene scene, string key)
    {
        using var client = TestData.ClientFor(api, scene.Traveller);
        using var response = await PostPaymentAsync(client, scene.BookingId, key);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StartedResponse>(TestData.Json, Token))!;
    }

    private static Task<HttpResponseMessage> PostPaymentAsync(HttpClient client, long bookingId, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/bookings/{bookingId}/payments");
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request, Token);
    }

    private static Task<HttpResponseMessage> WebhookAsync(GhurifyApiFactory api, Dictionary<string, string> fields)
    {
        var client = api.CreateClient();
        return client.PostAsJsonAsync("/api/v1/payments/webhooks/fake", fields, Token);
    }

    private static async Task<IReadOnlyList<LedgerEntry>> LedgerAsync(GhurifyApiFactory api, long bookingId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPaymentRepository>().QueryLedgerAsync(bookingId, Token);
    }

    private static decimal Balance(IEnumerable<LedgerEntry> ledger) =>
        ledger.Sum(entry => entry.EntryType == Domain.Payments.LedgerEntryType.Hold ? entry.Amount : -entry.Amount);

    /// <summary>The invariant: no booking's escrow balance is ever negative.</summary>
    private static async Task AssertLedgerBalancedAsync(TestData data)
    {
        await using var connection = await data.OpenAsync();
        var negative = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1)
            FROM  (SELECT [BookingId],
                          SUM(CASE WHEN [EntryType] = 1 THEN [Amount] ELSE -[Amount] END) AS [Balance]
                   FROM   [Pay].[EscrowLedger]
                   GROUP BY [BookingId]) AS [b]
            WHERE  [b].[Balance] < 0;
            """,
            cancellationToken: Token));

        Assert.Equal(0, negative);
    }

    private static async Task<string> ReferenceAsync(TestData data, long paymentId)
    {
        await using var connection = await data.OpenAsync();
        return await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT [TransactionRef] FROM [Pay].[Payment] WHERE [Id] = @Id;", new { Id = paymentId }, cancellationToken: Token)) ?? string.Empty;
    }

    private static async Task<byte> BookingStatusAsync(TestData data, long bookingId)
    {
        await using var connection = await data.OpenAsync();
        return await connection.ExecuteScalarAsync<byte>(new CommandDefinition(
            "SELECT [Status] FROM [Pay].[Booking] WHERE [Id] = @Id;", new { Id = bookingId }, cancellationToken: Token));
    }

    private static async Task<byte> PaymentStatusAsync(TestData data, long paymentId)
    {
        await using var connection = await data.OpenAsync();
        return await connection.ExecuteScalarAsync<byte>(new CommandDefinition(
            "SELECT [Status] FROM [Pay].[Payment] WHERE [Id] = @Id;", new { Id = paymentId }, cancellationToken: Token));
    }

    private static async Task ExpireHoldAsync(TestData data, long bookingId)
    {
        await using var connection = await data.OpenAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE [Pay].[Booking] SET [HoldExpiresAt] = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE [Id] = @Id;",
            new { Id = bookingId },
            cancellationToken: Token));
    }

    private sealed record IdResponse(long Id);

    private sealed record ApprovedResponse(long BookingId);

    private sealed record StartedResponse(long PaymentId, string RedirectUrl, decimal Amount, decimal Fee, decimal Total);

    private sealed record CheckoutResponse(long BookingId, decimal Amount, decimal Fee, decimal Total, string Status);
}
