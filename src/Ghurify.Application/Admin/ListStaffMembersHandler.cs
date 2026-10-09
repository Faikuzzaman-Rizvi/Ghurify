using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Admin;

/// <summary>Everybody on the admin desk, with the roles each one holds and who granted them.</summary>
public sealed class ListStaffMembersHandler(IStaffRoleRepository roles, AccessService access)
{
    public async Task<Result<StaffMembersView>> HandleAsync(long actorId, CancellationToken cancellationToken)
    {
        var actor = await access.GetAsync(actorId, cancellationToken);
        if (!actor.Can(Permissions.StaffView))
        {
            return AppError.Forbidden();
        }

        var members = await roles.QueryMembersAsync(cancellationToken);

        return new StaffMembersView(
            [.. members.Select(member => new StaffMemberView(
                member.UserId,
                member.Email,
                member.DisplayName,
                member.Status,
                member.AvatarUpdatedOn,
                member.StaffSince,
                member.Roles))]);
    }
}
