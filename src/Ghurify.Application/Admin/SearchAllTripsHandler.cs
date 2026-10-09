using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Trips;

namespace Ghurify.Application.Admin;

/// <summary>Finds trips in any status, for the admin desk. 25 to a page.</summary>
public sealed class SearchAllTripsHandler(IAdminRepository admin, AccessService access)
{
    public const int PageSize = 25;

    public async Task<Result<AdminTripPage>> HandleAsync(
        long actorId, string? search, TripStatus? status, int page, CancellationToken cancellationToken)
    {
        if (!(await access.GetAsync(actorId, cancellationToken)).Can(Permissions.TripsView))
        {
            return AppError.Forbidden();
        }

        page = Math.Max(page, 1);
        var term = search?.Trim();
        return await admin.SearchTripsAsync(
            string.IsNullOrEmpty(term) ? null : term[..Math.Min(term.Length, 200)],
            status,
            (page - 1) * PageSize,
            PageSize,
            cancellationToken);
    }
}
