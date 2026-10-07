using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Trips;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Safety;

/// <summary>
/// The safety desk sets a destination Open, Caution or Closed (audited). Closing it takes effect at
/// once for new bookings (requests and publishing are refused) and queues the closure job, which
/// cancels the trips there and refunds everyone in full.
/// </summary>
public sealed class ChangeDestinationStatusHandler(
    ISafetyRepository safety,
    AccessService access,
    IAuditLog audit,
    IBackgroundJobs jobs,
    ILogger<ChangeDestinationStatusHandler> logger)
{
    public async Task<Result<Done>> HandleAsync(long actorId, string slug, ChangeDestinationStatusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!(await access.GetAsync(actorId, cancellationToken)).IsSafetyDesk)
        {
            return AppError.Forbidden();
        }

        if (!Enum.IsDefined(command.Status))
        {
            return AppError.Validation("destination_status", "Choose Open, Caution or Closed.");
        }

        var note = Clean(command.Note);
        var noteBn = Clean(command.NoteBn);

        if (command.Status != DestinationStatus.Open && (note is null || noteBn is null))
        {
            return AppError.Validation("destination_note_required", "Explain the warning in both Bangla and English.");
        }

        if (note is { Length: > 300 } || noteBn is { Length: > 300 })
        {
            return AppError.Validation("destination_note_length", "Keep each note under 300 characters.");
        }

        var (alertId, _) = await safety.SetDestinationStatusAsync(slug, command.Status, note, noteBn, actorId, cancellationToken);
        if (alertId is null)
        {
            return AppError.NotFound("destination_not_found", "There is no such destination.");
        }

        await audit.WriteAsync(actorId, $"destination.{command.Status.ToString().ToLowerInvariant()}", "DestinationAlert", alertId.Value, note, cancellationToken);
        logger.LogWarning("Destination {Slug} set to {Status} by {ActorId}.", slug, command.Status, actorId);

        if (command.Status == DestinationStatus.Closed)
        {
            await jobs.EnqueueAsync<ICloseDestinationJob>(alertId.Value, cancellationToken);
        }

        return Done.Value;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record ChangeDestinationStatusCommand(DestinationStatus Status, string? Note, string? NoteBn);
