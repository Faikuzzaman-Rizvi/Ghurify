using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;

namespace Ghurify.Application.Trips;

/// <summary>
/// Puts photos someone took on one of their visits, so they show with that place on their map.
/// The photos are uploaded first the way story photos are (checked, location data stripped), then
/// attached here by id. Only their own photos, on their own visit; only they see them.
/// </summary>
public sealed class AddVisitPhotosHandler(ITravelMapRepository maps, AccessService access)
{
    /// <summary>Photos on one visit: a handful of the best, not a camera roll.</summary>
    public const int MaxPhotosPerVisit = 12;

    public async Task<Result<Done>> HandleAsync(
        long userId,
        long visitId,
        VisitPhotosCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (CheckPhotos(command.MediaIds, out var mediaIds) is { } bad)
        {
            return bad;
        }

        if (mediaIds.Count == 0)
        {
            return AppError.Validation("photos_missing", "Choose at least one photo.");
        }

        if (!(await access.GetAsync(userId, cancellationToken)).IsActive)
        {
            return AppError.Forbidden();
        }

        return await maps.AddPhotosAsync(userId, visitId, mediaIds, MaxPhotosPerVisit, cancellationToken) switch
        {
            AddPhotosOutcome.Added => Done.Value,
            AddPhotosOutcome.VisitNotFound => AppError.NotFound("visit_not_found", "There is no such place on your map."),
            AddPhotosOutcome.Unusable => Unusable,
            _ => TooMany,
        };
    }

    internal static AppError Unusable =>
        AppError.Validation("photo_not_usable", "One of the photos is not yours, is not a photo, or did not upload.");

    internal static AppError TooMany =>
        AppError.Rule("too_many_photos", $"A place can have up to {MaxPhotosPerVisit} photos from one visit.");

    /// <summary>The distinct photo ids, or why they cannot be used. None at all is fine here.</summary>
    internal static AppError? CheckPhotos(IReadOnlyList<long>? raw, out IReadOnlyList<long> mediaIds)
    {
        mediaIds = [.. (raw ?? []).Distinct()];
        if (mediaIds.Any(id => id <= 0))
        {
            return Unusable;
        }

        return mediaIds.Count > MaxPhotosPerVisit ? TooMany : null;
    }
}

public sealed record VisitPhotosCommand(IReadOnlyList<long>? MediaIds);
