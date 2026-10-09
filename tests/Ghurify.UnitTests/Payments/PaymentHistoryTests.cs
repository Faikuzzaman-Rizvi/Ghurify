using System.Net;
using System.Text;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Admin;
using Ghurify.Application.Identity;
using Ghurify.Application.Payments;
using Ghurify.Domain.Bookings;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Payments;
using Ghurify.Infrastructure.Payments;
using Ghurify.UnitTests.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ghurify.UnitTests.Payments;

/// <summary>
/// How a payment was made, as each gateway reports it, and who may read payment history: a
/// traveller their own, a host the paid bookings on their trips (without payment details), the
/// admin desk everything.
/// </summary>
public sealed class PaymentHistoryTests
{
    private const long TravellerId = 10;
    private const long HostId = 20;
    private const long AdminId = 30;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly FakeAccessRepository _access = new();
    private readonly FakeHistoryRepository _history = new();

    public PaymentHistoryTests()
    {
        _access.Add(TravellerId);
        _access.Add(HostId, [Role.Host]);
        _access.AddStaff(AdminId, StaffRoleDefaults.Admin);
    }

    // --- SSLCommerz validation ------------------------------------------------------------

    [Fact]
    public async Task SslCommerz_ABkashPayment_IsRecordedAsMobileBanking_WithTheGatewayTimeInUtc()
    {
        using var gateway = SslCommerzAnswering(
            """
            {"status":"VALID","tran_id":"GHR1","val_id":"VAL123","bank_tran_id":"BT999","amount":"6120.00",
             "store_amount":"5966.70","currency":"BDT","currency_type":"BDT","tran_date":"2026-10-07 15:30:00",
             "card_type":"BKASH-BKash","card_no":"01711XXXX78","card_brand":"MOBILEBANKING",
             "card_issuer":"BKash Mobile Banking","risk_level":"0"}
            """);

        var validation = await gateway.ValidateAsync(Callback(), Token);

        Assert.True(validation.IsValid);
        var details = Assert.IsType<GatewayPaymentDetails>(validation.Details);
        Assert.Equal(PaymentMethodType.MobileBanking, details.MethodType);
        Assert.Equal("bKash", details.MethodName);
        Assert.Equal("1178", details.AccountLast4);
        Assert.Equal("BKash Mobile Banking", details.Issuer);
        Assert.Equal("VAL123", details.ValidationId);
        // 15:30 in Dhaka (UTC+6) is 09:30 UTC.
        Assert.Equal(new DateTimeOffset(2026, 10, 7, 9, 30, 0, TimeSpan.Zero), details.PaidOn);
        Assert.Equal(5966.70m, details.StoreAmount);
        Assert.False(details.RiskFlagged);
    }

    [Theory]
    [InlineData("VISA-Dutch Bangla", "VISA", "432149XXXXXX0667", PaymentMethodType.Card, "Visa", "0667")]
    [InlineData("MASTER-City Bank", "MASTERCARD", "512345XXXXXX1234", PaymentMethodType.Card, "Mastercard", "1234")]
    [InlineData("NAGAD-Nagad", "MOBILEBANKING", "", PaymentMethodType.MobileBanking, "Nagad", null)]
    [InlineData("DBBLMOBILEB-Dbbl Mobile Banking", "MOBILEBANKING", "0191XXXX345", PaymentMethodType.MobileBanking, "Rocket", "1345")]
    [InlineData("CITYTOUCHIB-City Bank", "IB", "", PaymentMethodType.InternetBanking, "City Bank", null)]
    [InlineData("SOMEPAY-Some Pay", "", "", PaymentMethodType.Other, "Some Pay", null)]
    public async Task SslCommerz_TheMethodIsNamedFromCardTypeAndBrand(
        string cardType, string cardBrand, string cardNo, PaymentMethodType expectedType, string expectedName, string? expectedLast4)
    {
        using var gateway = SslCommerzAnswering(
            $$"""
            {"status":"VALID","tran_id":"GHR1","val_id":"VAL1","bank_tran_id":"BT1","amount":"100.00","currency":"BDT",
             "card_type":"{{cardType}}","card_brand":"{{cardBrand}}","card_no":"{{cardNo}}"}
            """);

        var details = (await gateway.ValidateAsync(Callback(), Token)).Details!;

        Assert.Equal(expectedType, details.MethodType);
        Assert.Equal(expectedName, details.MethodName);
        // Only the last four digits survive, whatever masking the gateway used.
        Assert.Equal(expectedLast4, details.AccountLast4);
    }

    [Fact]
    public async Task SslCommerz_ANumericOrMissingDetailField_NeverFailsTheValidation()
    {
        using var gateway = SslCommerzAnswering(
            """
            {"status":"VALIDATED","tran_id":"GHR1","bank_tran_id":"BT1","amount":"100.00","currency":"BDT",
             "risk_level":1,"store_amount":97.5,"tran_date":"not a date"}
            """);

        var validation = await gateway.ValidateAsync(Callback(), Token);

        Assert.True(validation.IsValid);
        Assert.Equal(100m, validation.Amount);
        var details = validation.Details!;
        Assert.True(details.RiskFlagged);
        Assert.Equal(97.5m, details.StoreAmount);
        Assert.Null(details.PaidOn);
        Assert.Equal(PaymentMethodType.Other, details.MethodType);
        // No val_id in the answer: the callback's own is kept.
        Assert.Equal("VAL-FROM-CALLBACK", details.ValidationId);
    }

    [Fact]
    public async Task SslCommerz_AnInvalidPayment_CarriesNoDetails()
    {
        using var gateway = SslCommerzAnswering("""{"status":"INVALID_TRANSACTION","card_type":"BKASH-BKash"}""");

        var validation = await gateway.ValidateAsync(Callback(), Token);

        Assert.False(validation.IsValid);
        Assert.Null(validation.Details);
    }

    // --- Sandbox gateway ------------------------------------------------------------------

    [Theory]
    [InlineData("bkash", PaymentMethodType.MobileBanking, "bKash")]
    [InlineData("nagad", PaymentMethodType.MobileBanking, "Nagad")]
    [InlineData("rocket", PaymentMethodType.MobileBanking, "Rocket")]
    [InlineData("card", PaymentMethodType.Card, "Visa")]
    [InlineData("anything-else", PaymentMethodType.Card, "Visa")]
    public async Task Sandbox_ReportsTheMethodChosenOnItsPage(string method, PaymentMethodType expectedType, string expectedName)
    {
        var gateway = new FakePaymentGateway(Options.Create(new PaymentsOptions()));
        var callback = gateway.ReadCallback(gateway.SignedCallback("GHR1", succeeded: true, 1000m, method: method))!;

        var validation = await gateway.ValidateAsync(callback, Token);

        var details = validation.Details!;
        Assert.Equal(expectedType, details.MethodType);
        Assert.Equal(expectedName, details.MethodName);
        Assert.Equal(4, details.AccountLast4!.Length);
        Assert.Equal(980m, details.StoreAmount);
    }

    // --- Who may read what -------------------------------------------------------------------

    [Fact]
    public async Task Traveller_History_AsksForTheirOwnPayments_OnTheRightPage()
    {
        var page = await new ListMyPaymentsHandler(_history).HandleAsync(TravellerId, PaymentStatus.Failed, page: 3, Token);

        var query = _history.LastQuery!;
        Assert.Equal((PaymentAudience.Traveller, TravellerId, PaymentStatus.Failed), (query.Audience, query.ViewerId, query.Status));
        Assert.Equal(40, query.Offset);
        Assert.Null(query.Search);
        Assert.Equal(3, page.Page);
        Assert.Equal(6120m - 3000m, page.Totals.Net);
    }

    [Fact]
    public async Task Traveller_APageBelowOne_IsTheFirstPage()
    {
        var page = await new ListMyPaymentsHandler(_history).HandleAsync(TravellerId, null, page: -4, Token);

        Assert.Equal(0, _history.LastQuery!.Offset);
        Assert.Equal(1, page.Page);
    }

    [Fact]
    public async Task Traveller_SomeoneElsesPayment_IsNotFound()
    {
        var result = await new GetMyPaymentHandler(_history).HandleAsync(TravellerId, paymentId: 999, Token);

        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
        Assert.Equal((PaymentAudience.Traveller, TravellerId), _history.LastDetailViewer);
    }

    [Fact]
    public async Task Host_SeesPaidBookingsOnTheirTrips_WithoutHowAnyonePaid()
    {
        var result = await new ListReceivedPaymentsHandler(_history, new AccessService(_access))
            .HandleAsync(HostId, tripId: 7, page: 1, Token);

        Assert.True(result.Succeeded);
        var query = _history.LastQuery!;
        Assert.Equal((PaymentAudience.Host, HostId, (long?)7), (query.Audience, query.ViewerId, query.TripId));
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(6000m, item.Amount);
        Assert.Equal(6000m, result.Value.Totals.BookingValue);
        // The host's view has no fee, gateway id or method: the type itself has no such members.
        Assert.DoesNotContain(typeof(ReceivedPayment).GetProperties(), property =>
            property.Name is "Fee" or "ProviderTxnId" or "MethodName" or "AccountLast4" or "Total");
    }

    [Fact]
    public async Task Host_ATravellerWhoIsNotAHost_IsForbidden()
    {
        var result = await new ListReceivedPaymentsHandler(_history, new AccessService(_access))
            .HandleAsync(TravellerId, tripId: null, page: 1, Token);

        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
        Assert.Null(_history.LastQuery);
    }

    [Fact]
    public async Task Admin_Search_IsTrimmedAndCapped_AndOnlyForAdmins()
    {
        var handler = new SearchPaymentsHandler(_history, new AccessService(_access));

        var refused = await handler.HandleAsync(HostId, "GHR1", null, 1, Token);
        Assert.Equal(ErrorKind.Forbidden, refused.Error!.Kind);
        Assert.Null(_history.LastQuery);

        var found = await handler.HandleAsync(AdminId, "  " + new string('a', 300) + "  ", PaymentStatus.Succeeded, 2, Token);
        Assert.True(found.Succeeded);
        var query = _history.LastQuery!;
        Assert.Equal(PaymentAudience.Admin, query.Audience);
        Assert.Equal(200, query.Search!.Length);
        Assert.Equal(SearchPaymentsHandler.PageSize, query.Offset);

        await handler.HandleAsync(AdminId, "   ", null, 1, Token);
        Assert.Null(_history.LastQuery!.Search);
    }

    [Fact]
    public async Task Admin_PaymentDetail_IsForbiddenToOthers()
    {
        var result = await new GetPaymentForAdminHandler(_history, new AccessService(_access)).HandleAsync(TravellerId, 1, Token);

        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
        Assert.Null(_history.LastDetailViewer);
    }

    // --- helpers ---------------------------------------------------------------------------

    private static GatewayCallback Callback() =>
        new("VAL-FROM-CALLBACK", "GHR1", GatewayOutcome.Succeeded, "VAL-FROM-CALLBACK", 100m, "BDT", null);

    private static SslCommerzGateway SslCommerzAnswering(string json) => new(
        Options.Create(new PaymentsOptions
        {
            Provider = PaymentsOptions.SslCommerzProvider,
            SslCommerz = new SslCommerzOptions { StoreId = "test", StorePassword = "secret" },
        }),
        NullLogger<SslCommerzGateway>.Instance,
#pragma warning disable CA2000 // The gateway owns the handler and disposes it with itself.
        new JsonAnswer(json));
#pragma warning restore CA2000

    /// <summary>Answers every request with the same JSON, as SSLCommerz's validation API would.</summary>
    private sealed class JsonAnswer(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    /// <summary>One succeeded payment of 6120 (6000 + fee) with 3000 refunded; remembers what was asked.</summary>
    private sealed class FakeHistoryRepository : IPaymentHistoryRepository
    {
        public PaymentQuery? LastQuery { get; private set; }

        public (PaymentAudience, long)? LastDetailViewer { get; private set; }

        public Task<PaymentRecords> QueryAsync(PaymentQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            var item = new PaymentHistoryItem(
                1, 2, 7, "Sajek", TravellerId, "Traveller", "fake", "GHR1", "FAKE-VAL", PaymentStatus.Succeeded,
                PaymentMethodType.MobileBanking, "bKash", "7788", 6000m, 120m, 6120m, 6120m, "BDT", 3000m,
                DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

            return Task.FromResult(new PaymentRecords([item], 1, new PaymentSums(1, 1, 6120m, 6000m, 3000m)));
        }

        public Task<AdminPaymentDetail?> GetAsync(
            PaymentAudience audience, long viewerId, long paymentId, CancellationToken cancellationToken)
        {
            LastDetailViewer = (audience, viewerId);
            return Task.FromResult<AdminPaymentDetail?>(null);
        }
    }
}
