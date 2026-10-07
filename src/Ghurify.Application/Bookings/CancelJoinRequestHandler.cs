using Ghurify.Application.Abstractions;
using Ghurify.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Bookings;

/// <summary>
/// A traveller withdraws their own request before paying; an unpaid seat goes back to the trip
/// and the host is told. A paid booking is cancelled through the refund rules instead.
/// </summary>
public sealed class CancelJoinRequestHandler(
    IJoinRequestRepository requests,
    NotificationService notifications,
    ILogger<CancelJoinRequestHandler> logger)
{
    public async Task<Result<Done>> HandleAsync(long userId, long requestId, CancellationToken cancellationToken)
    {
        var decision = await requests.CancelAsync(requestId, userId, cancellationToken);

        switch (decision.Outcome)
        {
            case JoinRequestOutcome.NotFound:
                return AppError.NotFound("request_not_found", "There is no such request.");
            case JoinRequestOutcome.NotOpen:
                return AppError.Conflict("request_closed", "This request is already closed.");
            case JoinRequestOutcome.AlreadyPaid:
                return AppError.Rule("booking_paid", "You have paid for this seat. Cancel the booking to get a refund.");
        }

        await notifications.NotifyAsync(
            decision.OtherPartyId!.Value,
            NotificationKinds.JoinRequestCancelled,
            $"join_request.cancelled:{requestId}",
            new { tripId = decision.TripId, requestId },
            cancellationToken);

        logger.LogInformation("User {UserId} withdrew request {RequestId}.", userId, requestId);
        return Done.Value;
    }
}
