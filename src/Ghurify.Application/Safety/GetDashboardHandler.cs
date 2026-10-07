using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;

namespace Ghurify.Application.Safety;

/// <summary>The admin overview: counts of what needs attention, over the last day where it matters.</summary>
public sealed class GetDashboardHandler(ISafetyRepository safety, AccessService access, IClock clock)
{
    public async Task<Result<DashboardCounts>> HandleAsync(long actorId, CancellationToken cancellationToken)
    {
        var actor = await access.GetAsync(actorId, cancellationToken);
        if (!(actor.IsAdmin || actor.IsSafetyDesk || actor.IsModerator))
        {
            return AppError.Forbidden();
        }

        return await safety.GetDashboardAsync(clock.UtcNow.AddDays(-1), cancellationToken);
    }
}
