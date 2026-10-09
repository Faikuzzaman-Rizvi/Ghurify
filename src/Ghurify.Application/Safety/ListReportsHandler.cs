using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Safety;

/// <summary>
/// The moderation queue. Reports and disputes are separate permissions, because a dispute ends
/// in money moving: a role can be given the reports queue without the disputes in it.
/// </summary>
public sealed class ListReportsHandler(ISafetyRepository safety, AccessService access)
{
    public async Task<Result<IReadOnlyList<ReportView>>> HandleAsync(
        long actorId,
        ReportKind? kind,
        ReportStatus status,
        CancellationToken cancellationToken)
    {
        var actor = await access.GetAsync(actorId, cancellationToken);
        var canSeeDisputes = actor.Can(Permissions.ModerationDisputes);

        var allowed = kind == ReportKind.Dispute
            ? canSeeDisputes
            : actor.Can(Permissions.ModerationReportsView);

        if (!allowed)
        {
            return AppError.Forbidden();
        }

        var reports = await safety.QueryReportsAsync(kind, status, cancellationToken);

        // Without a kind filter, somebody who may not see disputes gets everything else.
        return Result.Ok<IReadOnlyList<ReportView>>(
            kind is null && !canSeeDisputes
                ? [.. reports.Where(report => report.Kind != ReportKind.Dispute)]
                : reports);
    }
}
