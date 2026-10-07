using Ghurify.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Safety;

/// <summary>
/// Anyone signed in can report a person, a story or a trip. A dispute is about a booking, so only
/// the traveller or the host on that booking can raise one.
/// </summary>
public sealed class FileReportHandler(ISafetyRepository safety, ILogger<FileReportHandler> logger)
{
    public async Task<Result<ReportFiled>> HandleAsync(long reporterId, FileReportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Enum.IsDefined(command.Kind) || !Enum.IsDefined(command.Reason) || command.TargetId <= 0)
        {
            return AppError.Validation("report_invalid", "Choose what you are reporting and why.");
        }

        var details = string.IsNullOrWhiteSpace(command.Details) ? null : command.Details.Trim();
        if (details is { Length: > 1000 })
        {
            return AppError.Validation("report_length", "Keep the details under 1000 characters.");
        }

        if (command.Kind == ReportKind.Dispute)
        {
            var booking = await safety.GetDisputeBookingAsync(command.TargetId, cancellationToken);
            if (booking is null || (booking.TravelerId != reporterId && booking.HostId != reporterId))
            {
                return AppError.NotFound("booking_not_found", "There is no such booking.");
            }
        }

        var id = await safety.AddReportAsync(
            new NewReport(reporterId, command.Kind, command.TargetId, command.Reason, details), cancellationToken);

        logger.LogInformation("User {ReporterId} filed report {ReportId} ({Kind}).", reporterId, id, command.Kind);
        return new ReportFiled(id);
    }
}

public sealed record FileReportCommand(ReportKind Kind, long TargetId, ReportReason Reason, string? Details);

public sealed record ReportFiled(long Id);
