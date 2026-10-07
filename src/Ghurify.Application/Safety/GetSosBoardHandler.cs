using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;

namespace Ghurify.Application.Safety;

/// <summary>The safety desk's live board. Safety desk and admins only.</summary>
public sealed class GetSosBoardHandler(ISafetyRepository safety, AccessService access)
{
    public async Task<Result<IReadOnlyList<SosBoardItem>>> HandleAsync(long actorId, bool includeResolved, CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).IsSafetyDesk)
        {
            return AppError.Forbidden();
        }

        return Result.Ok(await safety.QuerySosBoardAsync(includeResolved, cancellationToken));
    }
}
