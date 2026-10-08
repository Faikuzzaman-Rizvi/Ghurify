using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Trips;

namespace Ghurify.Application.Trips;

/// <summary>
/// Someone adds a place they have been to their travel map: a Ghurify destination, or anywhere else
/// in Bangladesh as a named pin with its division. The date must have happened. Photos they
/// uploaded for it go on the new visit together with it.
/// </summary>
public sealed class AddVisitHandler(ITravelMapRepository maps, AccessService access, TripViewer viewer)
{
    /// <summary>Places one person may add themselves. Generous; it stops a runaway script, not a traveller.</summary>
    public const int MaxAddedPlaces = 2000;

    public async Task<Result<VisitCreated>> HandleAsync(long userId, AddVisitCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var slug = string.IsNullOrWhiteSpace(command.DestinationSlug) ? null : command.DestinationSlug.Trim();
        string? name = null;
        Division? division = null;
        double? latitude = null;
        double? longitude = null;

        if (slug is null)
        {
            name = command.PlaceName?.Trim();
            if (name is not { Length: >= 2 and <= 120 })
            {
                return AppError.Validation("place_name", "Name the place, in 2 to 120 characters.");
            }

            if (command.Division is not { } chosen || !Enum.IsDefined(chosen))
            {
                return AppError.Validation("place_division", "Choose the division the place is in.");
            }

            if (command.Latitude is not { } lat || command.Longitude is not { } lng || !BangladeshArea.Contains(lat, lng))
            {
                return AppError.Validation("place_outside_bangladesh", "Put the pin on the place, inside Bangladesh.");
            }

            division = chosen;
            latitude = Math.Round(lat, 6);
            longitude = Math.Round(lng, 6);
        }

        if (VisitRules.CheckDate(command.VisitedOn, viewer.TodayInDhaka) is { } badDate)
        {
            return badDate;
        }

        if (VisitRules.CheckNote(command.Note, out var note) is { } badNote)
        {
            return badNote;
        }

        if (AddVisitPhotosHandler.CheckPhotos(command.MediaIds, out var mediaIds) is { } badPhotos)
        {
            return badPhotos;
        }

        if (!(await access.GetAsync(userId, cancellationToken)).IsActive)
        {
            return AppError.Forbidden();
        }

        var added = await maps.AddVisitAsync(
            new NewVisit(
                userId,
                slug,
                name,
                division,
                latitude,
                longitude,
                command.VisitedOn,
                note,
                MaxAddedPlaces,
                mediaIds,
                AddVisitPhotosHandler.MaxPhotosPerVisit),
            cancellationToken);

        return added.Outcome switch
        {
            AddVisitOutcome.Added => new VisitCreated(added.Id!.Value),
            AddVisitOutcome.UnknownDestination => AppError.Validation("destination_unknown", "Choose one of the listed destinations."),
            AddVisitOutcome.AlreadyOnMap => AppError.Conflict("visit_exists", "That place is already on your map for that day."),
            AddVisitOutcome.PhotoUnusable => AddVisitPhotosHandler.Unusable,
            AddVisitOutcome.TooManyPhotos => AddVisitPhotosHandler.TooMany,
            _ => AppError.Rule("too_many_places", $"You can add up to {MaxAddedPlaces} places yourself."),
        };
    }
}

/// <summary>What a visit's date and note may be, for adding and for editing.</summary>
internal static class VisitRules
{
    public const int MaxNoteLength = 500;

    /// <summary>Nothing before this: a travel map, not a family history.</summary>
    public static readonly DateOnly Earliest = new(1950, 1, 1);

    public static AppError? CheckDate(DateOnly visitedOn, DateOnly today) =>
        visitedOn > today
            ? AppError.Validation("visit_in_future", "Choose a day that has already happened.")
            : visitedOn < Earliest
                ? AppError.Validation("visit_date", "Choose a day from 1950 on.")
                : null;

    /// <summary>The trimmed note, or null when blank.</summary>
    public static AppError? CheckNote(string? raw, out string? note)
    {
        note = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        return note is { Length: > MaxNoteLength }
            ? AppError.Validation("visit_note", $"Keep the note to {MaxNoteLength} characters.")
            : null;
    }
}

public sealed record AddVisitCommand(
    string? DestinationSlug,
    string? PlaceName,
    Division? Division,
    double? Latitude,
    double? Longitude,
    DateOnly VisitedOn,
    string? Note,
    IReadOnlyList<long>? MediaIds = null);

public sealed record VisitCreated(long Id);
