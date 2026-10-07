using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;

namespace Ghurify.Application.Admin;

/// <summary>One person in full, for an admin: account, roles, checks, trips, bookings and reports.</summary>
public sealed class GetUserDetailHandler(IAdminRepository admin, AccessService access)
{
    public async Task<Result<AdminUserDetail>> HandleAsync(long actorId, long userId, CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).IsAdmin)
        {
            return AppError.Forbidden();
        }

        var user = await admin.GetUserAsync(userId, cancellationToken);
        return user is null ? AppError.NotFound("user_not_found", "There is no such person.") : user;
    }
}
