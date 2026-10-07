using Ghurify.Application.Notifications;
using Ghurify.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Payments;

/// <summary>
/// Gives money back: takes it out of escrow (refund row + ledger Refund entry, atomically, capped at
/// what escrow holds), then asks the gateway to send it. A gateway failure leaves the refund for the
/// retry job; the ledger already shows the money as owed back, so nothing is lost or double-paid.
/// Idempotent on the reference: the same refund requested twice is created and sent once.
/// </summary>
public sealed class RefundService(
    IRefundRepository refunds,
    IPaymentGateway gateway,
    NotificationService notifications,
    ILogger<RefundService> logger)
{
    /// <summary>One refund (a mismatched or late payment, a traveller cancelling).</summary>
    public async Task<RefundCreated> IssueAsync(NewRefund refund, long travelerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refund);

        var created = await refunds.AddAsync(refund, cancellationToken);

        if (created.Outcome is RefundCreateOutcome.NothingHeld or RefundCreateOutcome.NoPayment)
        {
            logger.LogWarning(
                "Refund {Reference} for booking {BookingId}: nothing to refund ({Outcome}); shortfall {Shortfall}.",
                refund.Reference, refund.BookingId, created.Outcome, created.Shortfall);
            return created;
        }

        LogShortfall(created.RefundId!.Value, refund.BookingId, created.Shortfall);
        await SendManyAsync([created.RefundId.Value], cancellationToken);

        await notifications.NotifyAsync(
            travelerId,
            NotificationKinds.BookingRefunded,
            $"booking.refunded:{created.RefundId}",
            new { bookingId = refund.BookingId, amount = created.Amount },
            cancellationToken);

        return created;
    }

    /// <summary>
    /// Many refunds at once (a trip cancelled, a destination closed): one database call to create
    /// them all, the gateway calls, then one call to record every answer.
    /// </summary>
    public async Task<IReadOnlyList<RefundCreatedFor>> IssueManyAsync(
        IReadOnlyList<(NewRefund Refund, long TravelerId)> requests,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0)
        {
            return [];
        }

        var created = await refunds.AddManyAsync([.. requests.Select(request => request.Refund)], cancellationToken);
        var sent = created.Where(refund => refund.RefundId is not null).ToList();

        foreach (var refund in sent)
        {
            LogShortfall(refund.RefundId!.Value, refund.BookingId, refund.Shortfall);
        }

        await SendManyAsync([.. sent.Select(refund => refund.RefundId!.Value)], cancellationToken);

        var travellerByBooking = requests.ToDictionary(request => request.Refund.BookingId, request => request.TravelerId);
        await notifications.NotifyManyAsync(
            [.. sent.Select(refund => new NotificationRequest(
                travellerByBooking[refund.BookingId],
                NotificationKinds.BookingRefunded,
                $"booking.refunded:{refund.RefundId}",
                new { bookingId = refund.BookingId, amount = refund.Amount }))],
            cancellationToken);

        return created;
    }

    /// <summary>
    /// Sends (or re-sends) refunds to the gateway and records every answer in one call. Safe to repeat:
    /// a refund the gateway already confirmed is skipped, and each carries its own reference.
    /// Returns how many the gateway accepted.
    /// </summary>
    public async Task<int> SendManyAsync(IReadOnlyCollection<long> refundIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refundIds);

        if (refundIds.Count == 0)
        {
            return 0;
        }

        var targets = await refunds.GetTargetsAsync(refundIds, cancellationToken);
        var results = new List<(long RefundId, GatewayRefundResult Result)>();

        // One gateway call per refund: each is its own money movement at the gateway.
        foreach (var target in targets.Where(target => target.Status != RefundStatus.Succeeded))
        {
            GatewayRefundResult result;
            try
            {
                result = await gateway.RefundAsync(
                    new GatewayRefundRequest(target.ProviderTxnId, target.Amount, target.Reference, "Ghurify refund"),
                    cancellationToken);
            }
            catch (PaymentGatewayException ex)
            {
                logger.LogWarning(ex, "Gateway refund for refund {RefundId} failed; it will be retried.", target.RefundId);
                result = new GatewayRefundResult(false, null, "gateway_unavailable");
            }

            results.Add((target.RefundId, result));
            logger.LogInformation(
                "Refund {RefundId} of {Amount} for booking {BookingId}: {Result}.",
                target.RefundId, target.Amount, target.BookingId, result.Succeeded ? "sent" : "failed");
        }

        await refunds.SetResultsAsync(results, cancellationToken);
        return results.Count(result => result.Result.Succeeded);
    }

    private void LogShortfall(long refundId, long bookingId, decimal shortfall)
    {
        if (shortfall > 0)
        {
            logger.LogWarning(
                "Refund {RefundId} for booking {BookingId} is short by {Shortfall}: escrow no longer holds it all.",
                refundId, bookingId, shortfall);
        }
    }
}

/// <summary>One refund created as part of a batch; RefundId is null when nothing was owed or held.</summary>
public sealed record RefundCreatedFor(long BookingId, long? RefundId, decimal Amount, decimal Shortfall);
