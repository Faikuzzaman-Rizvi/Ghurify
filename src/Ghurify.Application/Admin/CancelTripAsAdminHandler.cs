using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Trips;
using Ghurify.Domain.Payments;

namespace Ghurify.Application.Admin;

/// <summary>
/// An admin cancels a trip (a host who vanished, a trip that breaks the rules): every paid
/// traveller is refunded in full, everyone is told, and the reason is audited.
/// </summary>
public sealed class CancelTripAsAdminHandler(TripCancellationService cancellation, AccessService access, IAuditLog audit)
{
    public async Task<Result<Done>> HandleAsync(long actorId, long tripId, AdminReason command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!(await access.GetAsync(actorId, cancellationToken)).IsAdmin)
        {
            return AppError.Forbidden();
        }

        var reason = command.Reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > 300)
        {
            return AppError.Validation("reason_required", "Write why, in up to 300 characters.");
        }

        var cancelled = await cancellation.CancelAsync([tripId], hostId: null, actorId, RefundReason.Admin, cancellationToken);
        if (cancelled.Trips.Count == 0)
        {
            return AppError.Rule("trip_not_cancellable", "This trip has already ended or been cancelled.");
        }

        await audit.WriteAsync(actorId, "trip.cancelled_by_admin", "Trip", tripId, reason, cancellationToken);
        return Done.Value;
    }
}
