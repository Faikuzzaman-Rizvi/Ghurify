using Ghurify.Application.Abstractions;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Identity;

/// <summary>The admin verification queue: pending checks first, oldest first.</summary>
public sealed class QueryVerificationQueueHandler(IVerificationRepository verifications, AccessService access)
{
    public const int PageSize = 25;

    public async Task<Result<VerificationQueuePage>> HandleAsync(
        long actorId,
        VerificationStatus status,
        int page,
        CancellationToken cancellationToken)
    {
        var actor = await access.GetAsync(actorId, cancellationToken);
        if (!actor.IsAdmin)
        {
            return AppError.Forbidden();
        }

        page = Math.Max(page, 1);
        var result = await verifications.QueryQueueAsync(status, (page - 1) * PageSize, PageSize, cancellationToken);
        return result with { Page = page };
    }
}
