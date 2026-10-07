using Ghurify.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Social;

/// <summary>
/// A review after a completed trip, between two people who were on it: travellers review the host,
/// the host reviews travellers. One per pair per trip. The rules are enforced in the database under
/// lock; this turns the outcome into the right answer.
/// </summary>
public sealed class AddReviewHandler(ISocialRepository social, ILogger<AddReviewHandler> logger)
{
    public const int MaxLength = 1000;

    public async Task<Result<ReviewAdded>> HandleAsync(
        long reviewerId,
        long tripId,
        AddReviewCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Rating is < 1 or > 5)
        {
            return AppError.Validation("rating_range", "Choose a rating from 1 to 5.");
        }

        var body = string.IsNullOrWhiteSpace(command.Body) ? null : command.Body.Trim();
        if (body is { Length: > MaxLength })
        {
            return AppError.Validation("review_length", $"Keep the review under {MaxLength} characters.");
        }

        if (command.Direction == ReviewDirection.TravelerToGuide)
        {
            return AppError.Rule("guide_reviews_unavailable", "Guide reviews open when guides can be booked on trips.");
        }

        if (!Enum.IsDefined(command.Direction))
        {
            return AppError.Validation("review_direction", "Unknown review type.");
        }

        var (outcome, id) = await social.AddReviewAsync(
            new NewReview(tripId, reviewerId, command.RevieweeId, command.Direction, command.Rating, body), cancellationToken);

        switch (outcome)
        {
            case ReviewOutcome.TripNotFound:
                return AppError.NotFound("trip_not_found", "This trip does not exist or is no longer available.");
            case ReviewOutcome.TripNotCompleted:
                return AppError.Rule("trip_not_completed", "Reviews open once the trip is over.");
            case ReviewOutcome.ReviewerNotOnTrip:
                return AppError.Forbidden("Only people who were on the trip can review it.");
            case ReviewOutcome.RevieweeNotOnTrip:
                return AppError.Rule("reviewee_not_on_trip", "You can only review people who were on this trip.");
            case ReviewOutcome.AlreadyReviewed:
                return AppError.Conflict("already_reviewed", "You have already reviewed this person for this trip.");
        }

        logger.LogInformation("User {ReviewerId} reviewed {RevieweeId} for trip {TripId}.", reviewerId, command.RevieweeId, tripId);
        return new ReviewAdded(id!.Value);
    }
}

public sealed record AddReviewCommand(long RevieweeId, ReviewDirection Direction, byte Rating, string? Body);

public sealed record ReviewAdded(long Id);
