using Ghurify.Application.Abstractions;
using Ghurify.Application.Admin;
using Ghurify.Application.Identity;
using Ghurify.Application.Notifications;
using Ghurify.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Social;

/// <summary>
/// A moderator (or admin) removes anyone's story from the admin portal. A reason is required: it
/// goes in the audit log and to the author, who is told their story was taken down and why.
/// </summary>
public sealed class RemovePostHandler(
    ISocialRepository social,
    AccessService access,
    IAuditLog audit,
    NotificationService notifications,
    ILogger<RemovePostHandler> logger)
{
    public const int MaxReasonLength = 300;

    public async Task<Result<Done>> HandleAsync(long actorId, long postId, AdminReason command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.ModerationContentManage))
        {
            return AppError.Forbidden();
        }

        var reason = command.Reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > MaxReasonLength)
        {
            return AppError.Validation("reason_required", $"Write why, in up to {MaxReasonLength} characters.");
        }

        if (await social.RemovePostAsync(postId, actorId, cancellationToken) is not { } authorId)
        {
            return AppError.NotFound("post_not_found", "There is no such post.");
        }

        await audit.WriteAsync(new AuditRecord(actorId, "post.removed", "Post", postId, reason), cancellationToken);
        await notifications.NotifyAsync(authorId, "post.removed", $"post.removed:{postId}", new { postId, reason }, cancellationToken);
        logger.LogInformation("Moderator {ActorId} removed post {PostId}.", actorId, postId);

        return Done.Value;
    }
}
