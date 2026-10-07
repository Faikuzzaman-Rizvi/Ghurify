using Ghurify.Application.Abstractions;
using Ghurify.Application.Social;

namespace Ghurify.Application.Identity;

/// <summary>
/// Removes a profile picture: the owner's own, or anyone's by an admin or moderator (an
/// inappropriate picture), which is audited.
/// </summary>
public sealed class RemoveAvatarHandler(IMediaStorage storage, IProfileRepository profiles, AccessService access, IAuditLog audit)
{
    public async Task<Result<Done>> HandleAsync(long actorId, long userId, CancellationToken cancellationToken)
    {
        if (actorId != userId)
        {
            if (!(await access.GetAsync(actorId, cancellationToken)).IsModerator)
            {
                return AppError.Forbidden();
            }

            await audit.WriteAsync(actorId, "user.avatar_removed", "User", userId, null, cancellationToken);
        }

        var previous = await profiles.SetAvatarAsync(userId, null, actorId, cancellationToken);
        if (previous is not null)
        {
            await storage.DeleteAsync(previous, cancellationToken);
        }

        return Done.Value;
    }
}
