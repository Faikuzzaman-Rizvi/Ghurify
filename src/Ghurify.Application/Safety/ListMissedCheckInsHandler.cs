using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Safety;

/// <summary>Missed check-ins in the last day, for the safety desk.</summary>
public sealed class ListMissedCheckInsHandler(ISafetyRepository safety, AccessService access, IClock clock)
{
    public async Task<Result<IReadOnlyList<MissedCheckIn>>> HandleAsync(long actorId, CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.SafetyCheckInsView))
        {
            return AppError.Forbidden();
        }

        return Result.Ok(await safety.QueryMissedCheckInsAsync(clock.UtcNow.AddDays(-1), cancellationToken));
    }
}
