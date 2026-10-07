using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;

namespace Ghurify.Application.Safety;

/// <summary>The host schedules a safety check-in for the group. Their own trips only.</summary>
public sealed class ScheduleCheckInHandler(ISafetyRepository safety, IClock clock)
{
    public async Task<Result<CheckInScheduled>> HandleAsync(long hostId, long tripId, ScheduleCheckInCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var label = command.Label?.Trim() ?? string.Empty;
        if (label.Length is 0 or > 150)
        {
            return AppError.Validation("checkin_label", "Name the check-in in up to 150 characters.");
        }

        if (command.DueAt <= clock.UtcNow)
        {
            return AppError.Validation("checkin_in_past", "A check-in must be in the future.");
        }

        var id = await safety.AddCheckInAsync(tripId, hostId, label, command.DueAt, cancellationToken);
        return id is null
            ? AppError.NotFound("trip_not_found", "This trip does not exist or is no longer available.")
            : new CheckInScheduled(id.Value);
    }
}

public sealed record ScheduleCheckInCommand(string? Label, DateTimeOffset DueAt);

public sealed record CheckInScheduled(long Id);
