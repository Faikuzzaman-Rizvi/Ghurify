using System.Net;
using System.Net.Http.Json;
using Dapper;
using Ghurify.Infrastructure.Payments;
using Ghurify.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Ghurify.IntegrationTests;

/// <summary>
/// Payment history against a real database, paying through the sandbox gateway: a traveller's own
/// attempts and receipts, a host's paid bookings, the admin desk's search and payment detail, and
/// that nobody sees anyone else's.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class PaymentHistoryEndpointTests(SqlServerFixture database)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Traveller_History_ListsEveryAttempt_NewestFirst_WithHowTheyPaid()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.HeldBookingAsync(data, api);

        // A declined card first, then bKash.
        await PayAsync(data, api, scene, "history-fail-0001", succeed: false, method: "card");
        await PayAsync(data, api, scene, "history-pass-0001", succeed: true, method: "bkash");

        using var traveller = TestData.ClientFor(api, scene.Traveller);
        var history = await traveller.GetFromJsonAsync<HistoryPage>("/api/v1/me/payments", TestData.Json, Token);

        Assert.Equal(2, history!.TotalCount);
        Assert.Equal(["Succeeded", "Failed"], history.Items.Select(item => item.Status));

        var paid = history.Items[0];
        Assert.Equal(("bKash", "MobileBanking", "7788"), (paid.MethodName, paid.MethodType, paid.AccountLast4));
        Assert.Equal((6000m, 120m, 6120m, 6120m), (paid.Amount, paid.Fee, paid.Total, paid.PaidAmount));
        Assert.StartsWith("GHR", paid.TransactionRef, StringComparison.Ordinal);
        Assert.StartsWith("FAKE-VAL-BKASH-", paid.ProviderTxnId, StringComparison.Ordinal);
        Assert.Equal("BDT", paid.Currency);
        Assert.NotNull(paid.CompletedOn);

        var failed = history.Items[1];
        Assert.Null(failed.MethodName);
        Assert.Null(failed.PaidAmount);

        Assert.Equal((2, 1, 6120m, 0m, 6120m), (history.Totals.Count, history.Totals.Succeeded, history.Totals.Paid, history.Totals.Refunded, history.Totals.Net));

        var onlyFailed = await traveller.GetFromJsonAsync<HistoryPage>("/api/v1/me/payments?status=Failed", TestData.Json, Token);
        Assert.Equal("Failed", Assert.Single(onlyFailed!.Items).Status);
        // The totals describe everything, whatever the filter shows.
        Assert.Equal(6120m, onlyFailed.Totals.Paid);
    }

    [Fact]
    public async Task Traveller_Receipt_ShowsEveryDetail_AndTheRefundAfterCancelling()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api, startIn: 20, method: "nagad");

        using var traveller = TestData.ClientFor(api, scene.Traveller);
        using var cancelled = await traveller.PostAsync(new Uri($"/api/v1/bookings/{scene.BookingId}/cancel", UriKind.Relative), null, Token);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        var paymentId = (await traveller.GetFromJsonAsync<HistoryPage>("/api/v1/me/payments", TestData.Json, Token))!.Items[0].Id;
        var receipt = await traveller.GetFromJsonAsync<Receipt>($"/api/v1/me/payments/{paymentId}", TestData.Json, Token);

        Assert.Equal((scene.BookingId, scene.TripId, "Refunded"), (receipt!.BookingId, receipt.TripId, receipt.BookingStatus));
        Assert.Equal(("Succeeded", "Nagad", "MobileBanking", "4455", "Nagad"), (receipt.Status, receipt.MethodName, receipt.MethodType, receipt.AccountLast4, receipt.Issuer));
        Assert.StartsWith("VAL-NAGAD-", receipt.ValidationId, StringComparison.Ordinal);
        Assert.NotNull(receipt.GatewayPaidOn);
        Assert.False(string.IsNullOrWhiteSpace(receipt.HostName));

        // 20 days out: the trip price comes back, the service fee does not.
        var refund = Assert.Single(receipt.Refunds);
        Assert.Equal((6000m, "TravelerCancelled"), (refund.Amount, refund.Reason));
        Assert.Equal((6000m, 120m), (receipt.Refunded, receipt.NetPaid));
    }

    [Fact]
    public async Task Traveller_SomeoneElsesPayment_IsNotFound_AndNeverListed()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api);
        var paymentId = await PaymentIdAsync(data, scene.BookingId);

        var stranger = await data.CreateVerifiedTravelerAsync();
        using var strangerClient = TestData.ClientFor(api, stranger);
        using var hidden = await strangerClient.GetAsync(new Uri($"/api/v1/me/payments/{paymentId}", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var strangersHistory = await strangerClient.GetFromJsonAsync<HistoryPage>("/api/v1/me/payments", TestData.Json, Token);
        Assert.Equal(0, strangersHistory!.TotalCount);
        Assert.Empty(strangersHistory.Items);

        // Hosting the trip does not open the traveller's receipt either.
        using var hostClient = TestData.ClientFor(api, scene.Host);
        using var notTheHosts = await hostClient.GetAsync(new Uri($"/api/v1/me/payments/{paymentId}", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.NotFound, notTheHosts.StatusCode);
    }

    [Fact]
    public async Task Host_SeesPaidBookingsOnTheirOwnTrips_WithoutPaymentDetails()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api, method: "bkash");

        // An unpaid seat on the same trip is not money received.
        var heldBooking = await Scenes.HeldBookingForTripAsync(data, api, scene.Host, scene.TripId);

        using var hostClient = TestData.ClientFor(api, scene.Host);
        using var response = await hostClient.GetAsync(new Uri($"/api/v1/me/received-payments?tripId={scene.TripId}", UriKind.Relative), Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(Token);
        Assert.DoesNotContain("methodName", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("providerTxnId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("7788", json, StringComparison.Ordinal);

        var received = await response.Content.ReadFromJsonAsync<ReceivedPage>(TestData.Json, Token);
        var item = Assert.Single(received!.Items);
        Assert.Equal((scene.BookingId, 6000m, 0m), (item.BookingId, item.Amount, item.Refunded));
        Assert.NotEqual(heldBooking, item.BookingId);
        Assert.Equal((1, 6000m), (received.Totals.Count, received.Totals.BookingValue));

        // Another host sees none of it.
        var otherHost = await data.CreateVerifiedHostAsync();
        using var otherClient = TestData.ClientFor(api, otherHost);
        var theirs = await otherClient.GetFromJsonAsync<ReceivedPage>($"/api/v1/me/received-payments?tripId={scene.TripId}", TestData.Json, Token);
        Assert.Empty(theirs!.Items);
    }

    [Fact]
    public async Task Host_ReceivedPayments_AreForbiddenToTravellers()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var traveller = await data.CreateVerifiedTravelerAsync();

        using var client = TestData.ClientFor(api, traveller);
        using var response = await client.GetAsync(new Uri("/api/v1/me/received-payments", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_FindsAPaymentByItsReferenceOrTheTravellersEmail_AndSeesItInFull()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api, method: "rocket");
        var paymentId = await PaymentIdAsync(data, scene.BookingId);
        var reference = await ReferenceAsync(data, paymentId);
        var admin = await data.CreateAdminAsync();
        using var adminClient = TestData.ClientFor(api, admin);

        var byReference = await adminClient.GetFromJsonAsync<HistoryPage>(
            $"/api/v1/admin/payments?search={Uri.EscapeDataString(reference)}", TestData.Json, Token);
        Assert.Equal(paymentId, Assert.Single(byReference!.Items).Id);

        var byEmail = await adminClient.GetFromJsonAsync<HistoryPage>(
            $"/api/v1/admin/payments?search={Uri.EscapeDataString(scene.Traveller.Email.ToUpperInvariant())}", TestData.Json, Token);
        Assert.Contains(byEmail!.Items, item => item.Id == paymentId);

        var detail = await adminClient.GetFromJsonAsync<AdminDetail>($"/api/v1/admin/payments/{paymentId}", TestData.Json, Token);
        Assert.Equal((paymentId, "Rocket"), (detail!.Payment.Id, detail.Payment.MethodName));
        Assert.Equal((scene.Traveller.Id, scene.Traveller.Email, scene.Host.Id), (detail.TravellerId, detail.TravellerEmail, detail.HostId));
        // The sandbox keeps 2% of 6,120 as its own charge.
        Assert.Equal(5997.60m, detail.StoreAmount);
        Assert.False(detail.RiskFlagged);
        var callback = Assert.Single(detail.Callbacks);
        Assert.True(callback.SignatureValid);
        Assert.Equal("confirmed", callback.Outcome);
        Assert.NotNull(callback.ProcessedOn);
    }

    [Fact]
    public async Task Admin_Payments_AreForbiddenToEveryoneElse()
    {
        await using var data = new TestData(database.ConnectionString);
        await using var api = new GhurifyApiFactory(database.ConnectionString);
        var scene = await Scenes.PaidBookingAsync(data, api);
        var paymentId = await PaymentIdAsync(data, scene.BookingId);

        foreach (var user in new[] { scene.Host, scene.Traveller })
        {
            using var client = TestData.ClientFor(api, user);
            using var list = await client.GetAsync(new Uri("/api/v1/admin/payments", UriKind.Relative), Token);
            using var one = await client.GetAsync(new Uri($"/api/v1/admin/payments/{paymentId}", UriKind.Relative), Token);

            Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, one.StatusCode);
        }
    }

    // --- helpers ---------------------------------------------------------------------------

    /// <summary>Starts a payment for the scene's booking and settles it through a signed sandbox callback.</summary>
    private static async Task PayAsync(TestData data, GhurifyApiFactory api, Scenes.Scene scene, string key, bool succeed, string method)
    {
        using var client = TestData.ClientFor(api, scene.Traveller);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/bookings/{scene.BookingId}/payments");
        request.Headers.Add("Idempotency-Key", key);
        using var started = await client.SendAsync(request, Token);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var payment = (await started.Content.ReadFromJsonAsync<Started>(TestData.Json, Token))!;

        var gateway = api.Services.GetRequiredService<FakePaymentGateway>();
        using var anonymous = api.CreateClient();
        using var webhook = await anonymous.PostAsJsonAsync(
            "/api/v1/payments/webhooks/fake",
            gateway.SignedCallback(await ReferenceAsync(data, payment.PaymentId), succeed, payment.Total, method: method),
            Token);
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
    }

    private static async Task<long> PaymentIdAsync(TestData data, long bookingId)
    {
        await using var connection = await data.OpenAsync();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT TOP (1) [Id] FROM [Pay].[Payment] WHERE [BookingId] = @Id ORDER BY [Id] DESC;", new { Id = bookingId }, cancellationToken: Token));
    }

    private static async Task<string> ReferenceAsync(TestData data, long paymentId)
    {
        await using var connection = await data.OpenAsync();
        return await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT [TransactionRef] FROM [Pay].[Payment] WHERE [Id] = @Id;", new { Id = paymentId }, cancellationToken: Token)) ?? string.Empty;
    }

    private sealed record Started(long PaymentId, decimal Total);

    private sealed record HistoryItem(
        long Id,
        long BookingId,
        string Status,
        string? MethodType,
        string? MethodName,
        string? AccountLast4,
        string TransactionRef,
        string? ProviderTxnId,
        decimal Amount,
        decimal Fee,
        decimal Total,
        decimal? PaidAmount,
        string Currency,
        DateTimeOffset? CompletedOn);

    private sealed record Totals(int Count, int Succeeded, decimal Paid, decimal Refunded, decimal Net);

    private sealed record HistoryPage(List<HistoryItem> Items, Totals Totals, int TotalCount);

    private sealed record RefundLine(decimal Amount, string Reason, string Status);

    private sealed record Receipt(
        long Id,
        long BookingId,
        string BookingStatus,
        long TripId,
        string? HostName,
        string Status,
        string? MethodType,
        string? MethodName,
        string? AccountLast4,
        string? Issuer,
        string? ValidationId,
        DateTimeOffset? GatewayPaidOn,
        decimal Refunded,
        decimal NetPaid,
        List<RefundLine> Refunds);

    private sealed record CallbackLine(bool SignatureValid, string? Outcome, DateTimeOffset? ProcessedOn);

    private sealed record AdminDetail(
        Receipt Payment,
        long TravellerId,
        string TravellerEmail,
        long HostId,
        decimal? StoreAmount,
        bool? RiskFlagged,
        List<CallbackLine> Callbacks);

    private sealed record ReceivedItem(long BookingId, decimal Amount, decimal Refunded);

    private sealed record ReceivedTotals(int Count, decimal BookingValue, decimal Refunded);

    private sealed record ReceivedPage(List<ReceivedItem> Items, ReceivedTotals Totals);
}
