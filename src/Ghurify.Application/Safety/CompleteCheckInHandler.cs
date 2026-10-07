using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Safety;

/// <summary>Anyone on the trip says the group is safe at this check-in.</summary>
public sealed class CompleteCheckInHandler(ISafetyRepository safety)
{
    public async Task<Result<Done>> HandleAsync(long userId, long checkInId, string? note, CancellationToken cancellationToken)
    {
        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (text is { Length: > 300 })
        {
            return AppError.Validation("checkin_note", "Keep the note under 300 characters.");
        }

        return await safety.CompleteCheckInAsync(checkInId, userId, text, cancellationToken)
            ? Done.Value
            : AppError.NotFound("checkin_not_found", "There is no such check-in waiting on your trip.");
    }
}
