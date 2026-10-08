using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;

namespace Ghurify.Application.Trips;

/// <summary>
/// Shows or hides someone's travel map on their public profile. Off until they turn it on; shared,
/// it shows only the Ghurify destinations they have been to.
/// </summary>
public sealed class SetTravelMapSharingHandler(ITravelMapRepository maps, AccessService access)
{
    public async Task<Result<Done>> HandleAsync(long userId, TravelMapSharingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!(await access.GetAsync(userId, cancellationToken)).IsActive)
        {
            return AppError.Forbidden();
        }

        await maps.SetSharingAsync(userId, command.Share, cancellationToken);
        return Done.Value;
    }
}

public sealed record TravelMapSharingCommand(bool Share);
