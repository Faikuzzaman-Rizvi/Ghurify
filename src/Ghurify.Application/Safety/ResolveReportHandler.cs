using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Payments;
using Ghurify.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Safety;

/// <summary>
/// A moderator decides a report: dismiss it, hide the story, suspend the person, or (admins, for a
/// dispute) refund the booking in full. Every decision is audited with its reason.
/// </summary>
public sealed class ResolveReportHandler(
    ISafetyRepository safety,
    AccessService access,
    RefundService refunds,
    IAuditLog audit,
    ILogger<ResolveReportHandler> logger)
{
    public async Task<Result<Done>> HandleAsync(long actorId, long reportId, ResolveReportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = await access.GetAsync(actorId, cancellationToken);
        if (!actor.IsModerator)
        {
            return AppError.Forbidden();
        }

        var resolution = command.Resolution?.Trim() ?? string.Empty;
        if (resolution.Length is 0 or > 500)
        {
            return AppError.Validation("resolution_required", "Write what was decided and why, in up to 500 characters.");
        }

        var open = (await safety.QueryReportsAsync(kind: null, ReportStatus.Open, cancellationToken))
            .FirstOrDefault(report => report.Id == reportId);
        if (open is null)
        {
            return AppError.NotFound("report_not_found", "There is no such open report.");
        }

        if (open.Kind == ReportKind.Dispute && !actor.IsAdmin)
        {
            return AppError.Forbidden("Disputes are decided by an admin.");
        }

        var check = Validate(open, command.Action);
        if (check is not null)
        {
            return check;
        }

        switch (command.Action)
        {
            case ReportAction.HidePost:
                await safety.HidePostAsync(open.TargetId, actorId, cancellationToken);
                break;
            case ReportAction.SuspendUser:
                await safety.SuspendUserAsync(open.TargetId, actorId, cancellationToken);
                break;
            case ReportAction.RefundBooking:
                var booking = await safety.GetDisputeBookingAsync(open.TargetId, cancellationToken);
                if (booking is null || booking.Paid <= 0)
                {
                    return AppError.Rule("nothing_to_refund", "This booking has nothing in escrow to refund.");
                }

                await refunds.IssueAsync(
                    new NewRefund(booking.BookingId, booking.Paid, RefundReason.Admin, $"refund:dispute:{reportId}", actorId),
                    booking.TravelerId,
                    cancellationToken);
                break;
        }

        var status = command.Action == ReportAction.Dismiss ? ReportStatus.Dismissed : ReportStatus.Actioned;
        await safety.ResolveReportAsync(reportId, status, resolution, actorId, cancellationToken);

        await audit.WriteAsync(actorId, $"report.{command.Action.ToString().ToLowerInvariant()}", "Report", reportId, resolution, cancellationToken);
        logger.LogInformation("Moderator {ActorId} resolved report {ReportId}: {Action}.", actorId, reportId, command.Action);

        return Done.Value;
    }

    /// <summary>The action has to fit what was reported.</summary>
    private static AppError? Validate(ReportView report, ReportAction action) => action switch
    {
        ReportAction.HidePost when report.Kind != ReportKind.Post =>
            AppError.Validation("report_action", "Only a reported story can be hidden."),
        ReportAction.SuspendUser when report.Kind != ReportKind.User =>
            AppError.Validation("report_action", "Only a reported person can be suspended."),
        ReportAction.RefundBooking when report.Kind != ReportKind.Dispute =>
            AppError.Validation("report_action", "Only a dispute can be refunded."),
        _ when !Enum.IsDefined(action) => AppError.Validation("report_action", "Unknown action."),
        _ => null,
    };
}

public sealed record ResolveReportCommand(ReportAction Action, string? Resolution);
