using System.Security.Claims;
using System.Text.Json;
using Ghurify.Application.Payments;
using Ghurify.Domain.Payments;
using Ghurify.Infrastructure.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// Paying into escrow. A traveller sees the total (price + fee) at checkout, starts a payment with
/// an Idempotency-Key, and is sent to the gateway. The gateway's callbacks (IPN and the browser
/// return) settle the payment; neither the browser nor any amount it sends is ever trusted.
/// </summary>
public static class PaymentsEndpoints
{
    public const string IdempotencyHeader = "Idempotency-Key";

    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder app, bool sandboxEnabled)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/bookings/{id:long}/checkout", GetCheckoutAsync)
            .WithTags("Payments")
            .WithName("GetCheckout")
            .WithSummary("Your booking's price, service fee and total, before paying.")
            .RequireAuthorization()
            .Produces<BookingCheckout>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/api/v1/bookings/{id:long}/payments", StartPaymentAsync)
            .WithTags("Payments")
            .WithName("StartPayment")
            .WithSummary("Starts paying for your held booking. Send an Idempotency-Key header; returns the gateway URL.")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Payments)
            .Produces<PaymentStarted>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        // Gateways call these without a user token: the signature and the server-to-server
        // validation are the credentials.
        app.MapPost("/api/v1/payments/webhooks/{provider}", WebhookAsync)
            .WithTags("Payments")
            .WithName("PaymentWebhook")
            .WithSummary("Gateway IPN. Verified by signature and server-to-server validation; idempotent.")
            .AllowAnonymous()
            .DisableAntiforgery()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapMethods("/api/v1/payments/return/{provider}/{outcome}", ["GET", "POST"], ReturnAsync)
            .WithTags("Payments")
            .WithName("PaymentReturn")
            .WithSummary("Where the gateway sends the traveller back. Settles like the IPN, then redirects to the web app.")
            .AllowAnonymous()
            .DisableAntiforgery()
            .Produces(StatusCodes.Status303SeeOther);

        app.MapGet("/api/v1/bookings/{id:long}/cancellation", CancellationQuoteAsync)
            .WithTags("Payments")
            .WithName("GetCancellationQuote")
            .WithSummary("What cancelling your paid booking now would refund, by the refund rules.")
            .RequireAuthorization()
            .Produces<CancellationQuote>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/api/v1/bookings/{id:long}/cancel", CancelBookingAsync)
            .WithTags("Payments")
            .WithName("CancelBooking")
            .WithSummary("Cancels your paid booking before the trip; refunds by days before departure.")
            .RequireAuthorization()
            .Produces<CancellationQuote>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/api/v1/me/payouts", ListPayoutsAsync)
            .WithTags("Payments")
            .WithName("ListMyPayouts")
            .WithSummary("Payouts released to you, by trip and stage.")
            .RequireAuthorization(Authorization.Policies.Host)
            .Produces<IReadOnlyList<PayoutView>>();

        app.MapGet("/api/v1/me/refunds", ListRefundsAsync)
            .WithTags("Payments")
            .WithName("ListMyRefunds")
            .WithSummary("Your refunds and where each stands.")
            .RequireAuthorization()
            .Produces<IReadOnlyList<RefundView>>();

        app.MapGet("/api/v1/me/payments", ListMyPaymentsAsync)
            .WithTags("Payments")
            .WithName("ListMyPayments")
            .WithSummary("Your payment history: every attempt, newest first, with what you paid and had refunded.")
            .RequireAuthorization()
            .Produces<PaymentHistoryPage>();

        app.MapGet("/api/v1/me/payments/{id:long}", GetMyPaymentAsync)
            .WithTags("Payments")
            .WithName("GetMyPayment")
            .WithSummary("One of your payments as a receipt: amounts, method, gateway references and refunds.")
            .RequireAuthorization()
            .Produces<PaymentDetail>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/v1/me/received-payments", ListReceivedPaymentsAsync)
            .WithTags("Payments")
            .WithName("ListReceivedPayments")
            .WithSummary("Paid bookings on your trips, newest first, optionally for one trip.")
            .RequireAuthorization(Authorization.Policies.Host)
            .Produces<ReceivedPaymentPage>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        if (sandboxEnabled)
        {
            MapSandbox(app);
        }

        return app;
    }

    /// <summary>
    /// The fake gateway's "payment page" calls these. They exist only when the fake gateway is
    /// configured, which the host refuses in production.
    /// </summary>
    private static void MapSandbox(IEndpointRouteBuilder app)
    {
        var sandbox = app.MapGroup("/api/v1/payments/sandbox")
            .WithTags("Payments")
            .RequireAuthorization();

        sandbox.MapGet("/{reference}", GetSandboxAsync)
            .WithName("GetSandboxPayment")
            .WithSummary("Sandbox only: the payment the sandbox page is showing.")
            .Produces<SandboxPayment>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        sandbox.MapPost("/{reference}/complete", CompleteSandboxAsync)
            .WithName("CompleteSandboxPayment")
            .WithSummary("Sandbox only: pays or fails the payment, through the real callback handler.")
            .Produces<CallbackHandled>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetCheckoutAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] GetCheckoutHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> StartPaymentAsync(
        long id,
        HttpRequest request,
        ClaimsPrincipal principal,
        [FromServices] StartPaymentHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(
            principal.RequireUserId(), id, request.Headers[IdempotencyHeader].ToString(), cancellationToken));

    private static async Task<IResult> WebhookAsync(
        string provider,
        HttpRequest request,
        [FromServices] HandlePaymentCallbackHandler handler,
        CancellationToken cancellationToken)
    {
        var fields = await ReadFieldsAsync(request, cancellationToken);
        var result = await handler.HandleAsync(provider, fields, cancellationToken);

        // A plain 200 is what gateways expect; anything else makes them retry.
        return ApiResults.From(result, _ => Results.Ok());
    }

    private static async Task<IResult> ReturnAsync(
        string provider,
        string outcome,
        HttpRequest request,
        [FromServices] HandlePaymentCallbackHandler handler,
        [FromServices] IPaymentRepository payments,
        [FromServices] IOptions<PaymentsOptions> options,
        [FromServices] ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var fields = await ReadFieldsAsync(request, cancellationToken);
        long? bookingId = null;

        try
        {
            var result = await handler.HandleAsync(provider, fields, cancellationToken);
            bookingId = result.Value?.BookingId;
        }
        catch (PaymentGatewayException ex)
        {
            // Validation is down. The IPN (retried by the gateway) will settle it; the result page
            // shows "we are confirming your payment" and polls.
            loggers.CreateLogger(nameof(PaymentsEndpoints))
                .LogWarning(ex, "Could not settle a returning payment now; the IPN will retry.");
        }

        // Not settled now (validation down, or a callback that did not check out): still send the
        // traveller to their booking's result page, which polls the API and only ever shows what
        // the verified callback recorded. The reference picks the page; it never moves money.
        if (bookingId is null && fields.TryGetValue("tran_id", out var reference) && !string.IsNullOrWhiteSpace(reference))
        {
            bookingId = (await payments.FindByReferenceAsync(reference, cancellationToken))?.BookingId;
        }

        var web = options.Value.WebBaseUrl.TrimEnd('/');
        var safeOutcome = outcome is "success" or "fail" or "cancel" ? outcome : "fail";
        var target = bookingId is { } id
            ? $"{web}/payments/result?booking={id}&outcome={safeOutcome}"
            : $"{web}/payments/result?outcome={safeOutcome}";

        // 303: the browser follows with a GET, whatever method the gateway used to post back.
        return Results.Redirect(target, permanent: false, preserveMethod: false);
    }

    private static async Task<IResult> CancellationQuoteAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] GetCancellationQuoteHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> CancelBookingAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] CancelBookingHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> ListPayoutsAsync(
        ClaimsPrincipal principal,
        [FromServices] ListMyPayoutsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    private static async Task<IResult> ListRefundsAsync(
        ClaimsPrincipal principal,
        [FromServices] ListMyRefundsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    private static async Task<IResult> ListMyPaymentsAsync(
        ClaimsPrincipal principal,
        [FromServices] ListMyPaymentsHandler handler,
        CancellationToken cancellationToken,
        PaymentStatus? status = null,
        int page = 1) =>
        Results.Ok(await handler.HandleAsync(principal.RequireUserId(), status, page, cancellationToken));

    private static async Task<IResult> GetMyPaymentAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] GetMyPaymentHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> ListReceivedPaymentsAsync(
        ClaimsPrincipal principal,
        [FromServices] ListReceivedPaymentsHandler handler,
        CancellationToken cancellationToken,
        long? tripId = null,
        int page = 1) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), tripId, page, cancellationToken));

    private static async Task<IResult> GetSandboxAsync(
        string reference,
        ClaimsPrincipal principal,
        [FromServices] IPaymentRepository payments,
        CancellationToken cancellationToken)
    {
        var payment = await payments.FindByReferenceAsync(reference, cancellationToken);

        return payment is null || payment.UserId != principal.RequireUserId()
            ? Results.Problem(title: "Not found", detail: "No such payment.", statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(new SandboxPayment(reference, payment.BookingId, payment.Total, payment.Status.ToString()));
    }

    private static async Task<IResult> CompleteSandboxAsync(
        string reference,
        SandboxCompletion completion,
        ClaimsPrincipal principal,
        [FromServices] IPaymentRepository payments,
        [FromServices] FakePaymentGateway gateway,
        [FromServices] HandlePaymentCallbackHandler handler,
        CancellationToken cancellationToken)
    {
        var payment = await payments.FindByReferenceAsync(reference, cancellationToken);

        if (payment is null || payment.UserId != principal.RequireUserId())
        {
            return Results.Problem(title: "Not found", detail: "No such payment.", statusCode: StatusCodes.Status404NotFound);
        }

        var fields = gateway.SignedCallback(reference, completion.Succeed, payment.Total, method: completion.Method ?? "card");
        return ApiResults.Ok(await handler.HandleAsync(gateway.Name, fields, cancellationToken));
    }

    /// <summary>Gateways post form fields (SSLCommerz) or JSON; both become a flat dictionary.</summary>
    private static async Task<IReadOnlyDictionary<string, string>> ReadFieldsAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(cancellationToken);
            foreach (var (key, value) in form)
            {
                fields[key] = value.ToString();
            }
        }
        else if (request.ContentLength is > 0 && request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    fields[property.Name] = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString() ?? string.Empty
                        : property.Value.GetRawText();
                }
            }
        }

        foreach (var (key, value) in request.Query)
        {
            fields.TryAdd(key, value.ToString());
        }

        return fields;
    }

    public sealed record SandboxPayment(string Reference, long BookingId, decimal Total, string Status);

    /// <summary>Pay or fail; <c>Method</c> is bkash, nagad, rocket or card (the default).</summary>
    public sealed record SandboxCompletion(bool Succeed, string? Method = null);
}
