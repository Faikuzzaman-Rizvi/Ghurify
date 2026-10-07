using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Trips;

namespace Ghurify.Application.Social;

/// <summary>
/// The author changes their own story: the text, the destination, and which photos stay (new
/// photos are a new story). Someone else's post, or a removed one, looks like a missing one.
/// </summary>
public sealed class EditPostHandler(ISocialRepository social, IDestinationRepository destinations, AccessService access)
{
    public async Task<Result<Done>> HandleAsync(long userId, long postId, EditPostCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var body = command.Body?.Trim() ?? string.Empty;
        var keep = command.KeepMediaIds ?? [];

        if (body.Length > CreatePostHandler.MaxLength || (body.Length == 0 && keep.Count == 0))
        {
            return AppError.Validation("post_length", $"Write up to {CreatePostHandler.MaxLength} characters, or keep a photo.");
        }

        if (!(await access.GetAsync(userId, cancellationToken)).IsActive)
        {
            return AppError.Forbidden();
        }

        long? destinationId = null;
        if (!string.IsNullOrWhiteSpace(command.DestinationSlug))
        {
            var destination = await destinations.FindBySlugAsync(command.DestinationSlug, cancellationToken);
            if (destination is null)
            {
                return AppError.Validation("destination_unknown", "Choose one of the listed destinations.");
            }

            destinationId = destination.Id;
        }

        return await social.SetPostAsync(new PostEdit(postId, userId, body, destinationId, [.. keep.Distinct()]), cancellationToken) switch
        {
            PostEditOutcome.Saved => Done.Value,
            PostEditOutcome.WouldBeEmpty => AppError.Validation("post_length", $"Write up to {CreatePostHandler.MaxLength} characters, or keep a photo."),
            _ => AppError.NotFound("post_not_found", "There is no such post."),
        };
    }
}

public sealed record EditPostCommand(string? Body, string? DestinationSlug, IReadOnlyList<long>? KeepMediaIds);
