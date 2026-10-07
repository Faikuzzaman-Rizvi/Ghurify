using Ghurify.Application.Abstractions;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Identity;

/// <summary>
/// Turns on hosting for the signed-in user. Self-service: anyone may become a host, but nobody
/// can publish a trip until they also pass the selfie identity check (the VerifiedHost rule).
/// </summary>
public sealed class BecomeHostHandler(IUserAccessRepository roles, AccessService access)
{
    public async Task<Result<Done>> HandleAsync(long userId, CancellationToken cancellationToken)
    {
        var current = await access.GetAsync(userId, cancellationToken);
        if (!current.IsActive)
        {
            return AppError.Forbidden();
        }

        await roles.GrantRoleAsync(userId, Role.Host, grantedById: userId, cancellationToken);
        access.Forget(userId);

        return Done.Value;
    }
}
