using Ghurify.Domain.Bookings;
using Ghurify.Domain.Payments;

namespace Ghurify.Application.Payments;

/// <summary>
/// Payment history and receipts. Every read names who is asking, and the procedures filter by it:
/// a traveller sees their own attempts, a host the paid bookings on their trips, the admin desk all.
/// </summary>
public interface IPaymentHistoryRepository
{
    Task<PaymentRecords> QueryAsync(PaymentQuery query, CancellationToken cancellationToken);

    /// <summary>One payment in full, or null when the viewer may not see it. Hosts are never shown one.</summary>
    Task<AdminPaymentDetail?> GetAsync(PaymentAudience audience, long viewerId, long paymentId, CancellationToken cancellationToken);
}

/// <summary>Who a payment read is for. Stored procedures take it as @Audience.</summary>
public enum PaymentAudience : byte
{
    Traveller = 1,
    Host = 2,
    Admin = 3,
}

public sealed record PaymentQuery(
    PaymentAudience Audience,
    long ViewerId,
    PaymentStatus? Status,
    long? TripId,
    string? Search,
    int Offset,
    int PageSize);

/// <summary>One page of payments as stored, with sums over everything the query can see.</summary>
public sealed record PaymentRecords(IReadOnlyList<PaymentHistoryItem> Items, int TotalCount, PaymentSums Sums);

/// <summary>
/// Over every payment visible to a query, whatever the status filter: how many, how many
/// succeeded, what was paid (total charged), the bookings' value (price only), and refunded.
/// </summary>
public sealed record PaymentSums(int Count, int Succeeded, decimal Paid, decimal BookingValue, decimal Refunded);

/// <summary>A payment attempt as a traveller's history and the admin desk list it.</summary>
public sealed record PaymentHistoryItem(
    long Id,
    long BookingId,
    long TripId,
    string TripTitle,
    long TravellerId,
    string? TravellerName,
    string Provider,
    string TransactionRef,
    string? ProviderTxnId,
    PaymentStatus Status,
    PaymentMethodType? MethodType,
    string? MethodName,
    string? AccountLast4,
    decimal Amount,
    decimal Fee,
    decimal Total,
    decimal? PaidAmount,
    string Currency,
    decimal Refunded,
    DateTimeOffset Created,
    DateTimeOffset? CompletedOn);

/// <summary>Paid, refunded and what is left: <c>Net</c> = <c>Paid</c> - <c>Refunded</c>.</summary>
public sealed record PaymentTotals(int Count, int Succeeded, decimal Paid, decimal Refunded, decimal Net)
{
    public static PaymentTotals From(PaymentSums sums)
    {
        ArgumentNullException.ThrowIfNull(sums);
        return new PaymentTotals(sums.Count, sums.Succeeded, sums.Paid, sums.Refunded, sums.Paid - sums.Refunded);
    }
}

public sealed record PaymentHistoryPage(
    IReadOnlyList<PaymentHistoryItem> Items,
    PaymentTotals Totals,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>
/// A paid booking on a host's trip. Only what the host needs to reconcile: no fee, gateway ids or
/// how the traveller paid.
/// </summary>
public sealed record ReceivedPayment(
    long Id,
    long BookingId,
    long TripId,
    string TripTitle,
    long TravellerId,
    string? TravellerName,
    string TransactionRef,
    decimal Amount,
    decimal Refunded,
    string Currency,
    DateTimeOffset? PaidOn);

/// <summary>Paid bookings, their value (price only) and what went back to travellers.</summary>
public sealed record ReceivedPaymentTotals(int Count, decimal BookingValue, decimal Refunded);

public sealed record ReceivedPaymentPage(
    IReadOnlyList<ReceivedPayment> Items,
    ReceivedPaymentTotals Totals,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>A payment as its receipt shows it, with every refund made against it.</summary>
public sealed record PaymentDetail(
    long Id,
    long BookingId,
    BookingStatus BookingStatus,
    long TripId,
    string TripTitle,
    DateOnly StartDate,
    DateOnly EndDate,
    string? HostName,
    string Provider,
    string TransactionRef,
    string? ProviderTxnId,
    string? ValidationId,
    PaymentStatus Status,
    string? FailureReason,
    PaymentMethodType? MethodType,
    string? MethodName,
    string? AccountLast4,
    string? Issuer,
    decimal Amount,
    decimal Fee,
    decimal Total,
    decimal? PaidAmount,
    string Currency,
    decimal Refunded,
    DateTimeOffset Created,
    DateTimeOffset? CompletedOn,
    DateTimeOffset? GatewayPaidOn,
    IReadOnlyList<PaymentRefundView> Refunds)
{
    /// <summary>What the traveller is out of pocket: paid, less refunds. Zero when nothing was paid.</summary>
    public decimal NetPaid => (PaidAmount ?? 0) - Refunded;
}

public sealed record PaymentRefundView(
    long Id,
    decimal Amount,
    decimal Shortfall,
    RefundReason Reason,
    RefundStatus Status,
    string? ProviderRefundRef,
    string? FailureReason,
    DateTimeOffset Created,
    DateTimeOffset? CompletedOn);

/// <summary>A callback the gateway sent for a payment, for the admin desk (never its payload).</summary>
public sealed record PaymentCallbackView(
    long Id,
    string EventId,
    bool SignatureValid,
    string? Outcome,
    DateTimeOffset Received,
    DateTimeOffset? ProcessedOn);

/// <summary>A payment for the admin desk: the receipt, who paid and who hosts, the gateway's settlement and its callbacks.</summary>
public sealed record AdminPaymentDetail(
    PaymentDetail Payment,
    long TravellerId,
    string? TravellerName,
    string TravellerEmail,
    long HostId,
    decimal? StoreAmount,
    bool? RiskFlagged,
    IReadOnlyList<PaymentCallbackView> Callbacks);
