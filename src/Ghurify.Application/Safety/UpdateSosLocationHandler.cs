using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Safety;

/// <summary>The person in trouble's phone sends its position while the SOS is open; the desk sees it move.</summary>
public sealed class UpdateSosLocationHandler(ISafetyRepository safety, ISafetyBroadcaster broadcaster)
{
    public async Task<Result<Done>> HandleAsync(long userId, long sosId, decimal latitude, decimal longitude, CancellationToken cancellationToken)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return AppError.Validation("sos_position", "The position is not valid.");
        }

        var updated = await safety.UpdateSosLocationAsync(sosId, userId, latitude, longitude, cancellationToken);
        if (updated is null)
        {
            return AppError.NotFound("sos_not_found", "There is no open SOS of yours with that id.");
        }

        await broadcaster.SosAsync(updated, cancellationToken);
        return Done.Value;
    }
}
