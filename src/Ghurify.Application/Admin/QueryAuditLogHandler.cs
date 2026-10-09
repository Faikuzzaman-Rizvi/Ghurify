using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Admin;

/// <summary>
/// The audit viewer. Reading the trail is itself a permission, because it says who decided what
/// about whom.
/// </summary>
public sealed class QueryAuditLogHandler(IAuditLog audit, AccessService access)
{
    public async Task<Result<AuditPage>> HandleAsync(
        long actorId,
        AuditQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.AuditView))
        {
            return AppError.Forbidden();
        }

        return await audit.QueryAsync(query, cancellationToken);
    }
}
