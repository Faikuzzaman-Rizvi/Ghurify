using Ghurify.Application.Abstractions;
using Ghurify.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Bookings;

/// <summary>The host declines a pending request; the traveller is told.</summary>
public sealed class DeclineJoinRequestHandler(
    IJoinRequestRepository requests,
    NotificationService notifications,
    ILogger<DeclineJoinRequestHandler> logger)
{
    public async Task<Result<Done>> HandleAsync(long hostId, long requestId, CancellationToken cancellationToken)
    {
        var decision = await requests.DeclineAsync(requestId, hostId, cancellationToken);

        switch (decision.Outcome)
        {
            case JoinRequestOutcome.NotFound:
                return AppError.NotFound("request_not_found", "There is no such request on your trips.");
            case JoinRequestOutcome.NotOpen:
                return AppError.Conflict("request_not_pending", "This request has already been answered.");
        }

        await notifications.NotifyAsync(
            decision.OtherPartyId!.Value,
            NotificationKinds.JoinRequestDeclined,
            $"join_request.declined:{requestId}",
            new { tripId = decision.TripId },
            cancellationToken);

        logger.LogInformation("Host {HostId} declined request {RequestId}.", hostId, requestId);
        return Done.Value;
    }
}
