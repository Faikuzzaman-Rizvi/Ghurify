using Ghurify.Domain.Bookings;
using Ghurify.Domain.Payments;

namespace Ghurify.Application.Payments;

/// <summary>
/// A payment gateway: SSLCommerz in sandbox or live, or the fake used in development and tests.
/// Every call that moves money carries our own reference, so a retried call is recognised.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Short stable name, stored with each payment: "fake", "sslcommerz".</summary>
    string Name { get; }

    /// <summary>Opens a payment session and returns where to send the traveller.</summary>
    Task<GatewaySession> StartAsync(GatewayPaymentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a callback (IPN or browser return) and verifies its signature. Null when the
    /// signature is missing or wrong: the caller must treat that as forged.
    /// </summary>
    GatewayCallback? ReadCallback(IReadOnlyDictionary<string, string> fields);

    /// <summary>
    /// Confirms a reported success with the gateway server-to-server, which is what the money
    /// decision rests on: a signed callback says what happened, this says it really did.
    /// </summary>
    Task<GatewayValidation> ValidateAsync(GatewayCallback callback, CancellationToken cancellationToken);

    /// <summary>Returns money to the traveller. <c>Reference</c> makes a retried refund idempotent.</summary>
    Task<GatewayRefundResult> RefundAsync(GatewayRefundRequest request, CancellationToken cancellationToken);
}

public sealed record GatewayPaymentRequest(
    string TransactionRef,
    decimal Total,
    string Currency,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string ProductName,
    string SuccessUrl,
    string FailUrl,
    string CancelUrl,
    string IpnUrl);

public sealed record GatewaySession(string SessionId, string RedirectUrl);

public enum GatewayOutcome
{
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3,
}

/// <summary>A callback whose signature checked out.</summary>
public sealed record GatewayCallback(
    string EventId,
    string TransactionRef,
    GatewayOutcome Outcome,
    string? ValidationId,
    decimal? Amount,
    string? Currency,
    string? Reason);

/// <summary>The gateway's own answer about a payment.</summary>
public sealed record GatewayValidation(
    bool IsValid,
    string TransactionRef,
    string ProviderTxnId,
    decimal Amount,
    string Currency)
{
    /// <summary>How the traveller paid, when the gateway said. Recorded with the payment; never trusted for money.</summary>
    public GatewayPaymentDetails? Details { get; init; }
}

/// <summary>
/// What a gateway's validation says about how a payment was made. <c>AccountLast4</c> is the only
/// part of a card or wallet number ever kept.
/// </summary>
public sealed record GatewayPaymentDetails(
    PaymentMethodType MethodType,
    string? MethodName,
    string? AccountLast4,
    string? Issuer,
    string? ValidationId,
    DateTimeOffset? PaidOn,
    decimal? StoreAmount,
    bool? RiskFlagged);

public sealed record GatewayRefundRequest(string ProviderTxnId, decimal Amount, string Reference, string Remarks);

public sealed record GatewayRefundResult(bool Succeeded, string? ProviderRefundRef, string? FailureReason);

/// <summary>Thrown when the gateway cannot be reached or answers with nonsense.</summary>
public sealed class PaymentGatewayException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Payment attempts, webhook events and the checkout view.</summary>
public interface IPaymentRepository
{
    Task<PaymentAttempt> AddAsync(NewPayment payment, CancellationToken cancellationToken);

    Task SetPendingAsync(long paymentId, string sessionId, string redirectUrl, CancellationToken cancellationToken);

    Task SetFailedAsync(long? paymentId, string? transactionRef, string reason, CancellationToken cancellationToken);

    /// <summary>Settles a payment the gateway validated, recording how it was paid.</summary>
    Task<PaymentSettlement> SetSucceededAsync(GatewayValidation validation, CancellationToken cancellationToken);

    /// <summary>Stores a callback once. Returns its id and whether it was already processed.</summary>
    Task<(long Id, bool AlreadyProcessed)> AddWebhookEventAsync(
        string provider,
        string eventId,
        string? transactionRef,
        string payload,
        bool signatureValid,
        CancellationToken cancellationToken);

    Task MarkWebhookProcessedAsync(long eventId, string outcome, CancellationToken cancellationToken);

    /// <summary>The attempt a transaction reference belongs to (its booking, owner and total).</summary>
    Task<PaymentReference?> FindByReferenceAsync(string transactionRef, CancellationToken cancellationToken);

    /// <summary>A traveller's own booking as checkout shows it. Null if not theirs.</summary>
    Task<BookingCheckout?> GetCheckoutAsync(long bookingId, long userId, CancellationToken cancellationToken);

    /// <summary>Every ledger entry for a booking, oldest first.</summary>
    Task<IReadOnlyList<LedgerEntry>> QueryLedgerAsync(long bookingId, CancellationToken cancellationToken);
}

/// <summary>Refunds and their gateway status.</summary>
public interface IRefundRepository
{
    Task<RefundCreated> AddAsync(NewRefund refund, CancellationToken cancellationToken);

    /// <summary>Creates many refunds in one call, each capped at its booking's escrow balance.</summary>
    Task<IReadOnlyList<RefundCreatedFor>> AddManyAsync(IReadOnlyList<NewRefund> refunds, CancellationToken cancellationToken);

    /// <summary>Records the gateway's answers for many refunds in one call.</summary>
    Task SetResultsAsync(
        IReadOnlyList<(long RefundId, GatewayRefundResult Result)> results,
        CancellationToken cancellationToken);

    /// <summary>What the gateway needs to send each refund: the payment's provider transaction id.</summary>
    Task<IReadOnlyList<RefundTarget>> GetTargetsAsync(IReadOnlyCollection<long> refundIds, CancellationToken cancellationToken);

    /// <summary>Refunds not yet confirmed by the gateway, for the retry job.</summary>
    Task<IReadOnlyList<long>> QueryRetryableAsync(int maxAttempts, CancellationToken cancellationToken);

    /// <summary>A traveller's own refunds, newest first.</summary>
    Task<IReadOnlyList<RefundView>> QueryForUserAsync(long userId, CancellationToken cancellationToken);
}

public sealed record NewPayment(
    long BookingId,
    long UserId,
    string IdempotencyKey,
    string Provider,
    string TransactionRef,
    decimal FeePercent,
    DateTimeOffset Now);

public enum PaymentStartOutcome
{
    Created = 0,
    Replay = 1,
    BookingNotFound = 2,
    AlreadyPaid = 3,
    NotPayable = 4,
    KeyReused = 5,
}

public sealed record PaymentAttempt(
    PaymentStartOutcome Outcome,
    long? PaymentId,
    string? TransactionRef,
    decimal Amount,
    decimal Fee,
    decimal Total,
    PaymentStatus? Status,
    string? RedirectUrl);

public enum SettlementOutcome
{
    Confirmed = 0,
    NotFound = 1,
    AlreadySettled = 2,
    AmountMismatch = 3,
    NeedsRefund = 4,
    ProviderTxnReused = 5,
}

public sealed record PaymentSettlement(
    SettlementOutcome Outcome,
    long? PaymentId,
    long? BookingId,
    long? UserId,
    long? HostId,
    long? TripId);

public sealed record PaymentReference(long PaymentId, long BookingId, long UserId, decimal Total, PaymentStatus Status);

public sealed record BookingCheckout(
    long BookingId,
    long TripId,
    string TripTitle,
    DateOnly StartDate,
    DateOnly EndDate,
    string? HostName,
    decimal Amount,
    decimal Fee,
    decimal Total,
    BookingStatus Status,
    DateTimeOffset HoldExpiresAt,
    PaymentStatus? LatestPaymentStatus,
    string? LatestPaymentFailure)
{
    /// <summary>Whether this payment is real; a sandbox checkout shows which test card to use.</summary>
    public PaymentMode Mode { get; init; } = PaymentMode.Live;
}

public sealed record LedgerEntry(
    long Id,
    LedgerEntryType EntryType,
    decimal Amount,
    LedgerCounterparty Counterparty,
    string Reference,
    DateTimeOffset Created);

public sealed record NewRefund(
    long BookingId,
    decimal Amount,
    RefundReason Reason,
    string Reference,
    long? ActorId = null,
    long? PaymentId = null);

public enum RefundCreateOutcome
{
    Created = 0,
    Existing = 1,
    NothingHeld = 2,
    NoPayment = 3,
}

public sealed record RefundCreated(RefundCreateOutcome Outcome, long? RefundId, decimal Amount, decimal Shortfall);

public sealed record RefundTarget(long RefundId, long BookingId, string ProviderTxnId, decimal Amount, string Reference, RefundStatus Status);

public sealed record RefundView(
    long Id,
    long BookingId,
    string TripTitle,
    decimal Amount,
    decimal Shortfall,
    RefundReason Reason,
    RefundStatus Status,
    DateTimeOffset Created,
    DateTimeOffset? CompletedOn);
