using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Trips;

/// <summary>
/// Someone changes a visit on their own map: the note, and the date of a visit they added (a
/// Ghurify trip's date is the trip's own). Anyone else's visit looks like a missing one.
/// </summary>
public sealed class EditVisitHandler(ITravelMapRepository maps, TripViewer viewer)
{
    public async Task<Result<Done>> HandleAsync(long userId, long visitId, EditVisitCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.VisitedOn is { } visitedOn && VisitRules.CheckDate(visitedOn, viewer.TodayInDhaka) is { } badDate)
        {
            return badDate;
        }

        if (VisitRules.CheckNote(command.Note, out var note) is { } badNote)
        {
            return badNote;
        }

        return await maps.UpdateVisitAsync(visitId, userId, command.VisitedOn, note, cancellationToken)
            ? Done.Value
            : AppError.NotFound("visit_not_found", "There is no such place on your map.");
    }
}

public sealed record EditVisitCommand(DateOnly? VisitedOn, string? Note);
