using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Admin;

/// <summary>Every emergency point, or one destination's, for the safety desk.</summary>
public sealed class ListEmergencyPointsHandler(IAdminRepository admin, AccessService access)
{
    public async Task<Result<IReadOnlyList<EmergencyPointView>>> HandleAsync(long actorId, string? destinationSlug, CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.SafetyPointsManage))
        {
            return AppError.Forbidden();
        }

        return Result.Ok(await admin.QueryEmergencyPointsAsync(destinationSlug, cancellationToken));
    }
}
