using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;

namespace Ghurify.Application.Safety;

/// <summary>The moderation queue. Moderators see reports; disputes (money) are for admins.</summary>
public sealed class ListReportsHandler(ISafetyRepository safety, AccessService access)
{
    public async Task<Result<IReadOnlyList<ReportView>>> HandleAsync(
        long actorId,
        ReportKind? kind,
        ReportStatus status,
        CancellationToken cancellationToken)
    {
        var actor = await access.GetAsync(actorId, cancellationToken);
        var allowed = kind == ReportKind.Dispute ? actor.IsAdmin : actor.IsModerator;

        if (!allowed)
        {
            return AppError.Forbidden();
        }

        var reports = await safety.QueryReportsAsync(kind, status, cancellationToken);

        // Without a kind filter a moderator sees everything but disputes.
        return Result.Ok<IReadOnlyList<ReportView>>(
            kind is null && !actor.IsAdmin ? [.. reports.Where(report => report.Kind != ReportKind.Dispute)] : reports);
    }
}
