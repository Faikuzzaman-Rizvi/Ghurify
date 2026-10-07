using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghurify.Application.Bookings;

/// <summary>
/// The host approves a request: a seat is reserved and held for the traveller until the payment
/// deadline, and the traveller is told how long they have. The seat is taken atomically in the
/// database, so two approvals for the last seat cannot both succeed.
/// </summary>
public sealed class ApproveJoinRequestHandler(
    IJoinRequestRepository requests,
    AccessService access,
    NotificationService notifications,
    IClock clock,
    IOptions<BookingOptions> options,
    ILogger<ApproveJoinRequestHandler> logger)
{
    public async Task<Result<JoinRequestApproved>> HandleAsync(
        long hostId,
        long requestId,
        CancellationToken cancellationToken)
    {
        var host = await access.GetAsync(hostId, cancellationToken);
        if (!host.IsActive)
        {
            return AppError.Forbidden();
        }

        // Second resolution, not a fraction: the deadline is shown to the traveller and stored
        // in DATETIME2(0), and they must agree.
        var now = clock.UtcNow;
        var holdExpiresAt = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, TimeSpan.Zero)
            .AddMinutes(options.Value.HoldMinutes);

        var approval = await requests.ApproveAsync(requestId, hostId, holdExpiresAt, cancellationToken);

        switch (approval.Outcome)
        {
            case JoinRequestOutcome.NotFound:
                // Not the caller's trip, or no such request: the same answer for both.
                return AppError.NotFound("request_not_found", "There is no such request on your trips.");
            case JoinRequestOutcome.NotOpen:
                return AppError.Conflict("request_not_pending", "This request has already been answered.");
            case JoinRequestOutcome.Full:
                return AppError.Conflict("trip_full", "This trip has no seats left.");
        }

        var bookingId = approval.BookingId!.Value;

        await notifications.NotifyAsync(
            approval.TravelerId!.Value,
            NotificationKinds.JoinRequestApproved,
            $"join_request.approved:{requestId}",
            new { tripId = approval.TripId, bookingId, holdExpiresAt },
            cancellationToken);

        logger.LogInformation(
            "Host {HostId} approved request {RequestId}: booking {BookingId} held until {HoldExpiresAt:O}.",
            hostId, requestId, bookingId, holdExpiresAt);

        return new JoinRequestApproved(bookingId, holdExpiresAt);
    }
}
