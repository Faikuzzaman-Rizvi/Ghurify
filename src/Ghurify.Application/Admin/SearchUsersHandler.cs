using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.Application.Admin;

/// <summary>Finds people by email, phone or name, for an admin. 25 to a page.</summary>
public sealed class SearchUsersHandler(IAdminRepository admin, AccessService access)
{
    public const int PageSize = 25;

    public async Task<Result<AdminUserPage>> HandleAsync(
        long actorId, string? search, UserStatus? status, Role? role, int page, CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).IsAdmin)
        {
            return AppError.Forbidden();
        }

        page = Math.Max(page, 1);
        var term = search?.Trim();
        return await admin.SearchUsersAsync(
            string.IsNullOrEmpty(term) ? null : term[..Math.Min(term.Length, 256)],
            status,
            role,
            (page - 1) * PageSize,
            PageSize,
            cancellationToken);
    }
}
