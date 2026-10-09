using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Safety;

/// <summary>The admin overview: counts of what needs attention, over the last day where it matters.</summary>
public sealed class GetDashboardHandler(ISafetyRepository safety, AccessService access, IClock clock)
{
    public async Task<Result<DashboardCounts>> HandleAsync(long actorId, CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.DashboardView))
        {
            return AppError.Forbidden();
        }

        return await safety.GetDashboardAsync(clock.UtcNow.AddDays(-1), cancellationToken);
    }
}
