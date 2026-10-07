using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Trips;

namespace Ghurify.Application.Social;

/// <summary>Publishes a story, with the author's own uploads attached.</summary>
public sealed class CreatePostHandler(ISocialRepository social, IDestinationRepository destinations, AccessService access)
{
    public const int MaxLength = 2000;
    public const int MaxMedia = 10;

    public async Task<Result<PostCreated>> HandleAsync(long userId, CreatePostCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var body = command.Body?.Trim() ?? string.Empty;
        var mediaIds = command.MediaIds ?? [];

        if (body.Length > MaxLength || (body.Length == 0 && mediaIds.Count == 0))
        {
            return AppError.Validation("post_length", $"Write up to {MaxLength} characters, or add a photo.");
        }

        if (mediaIds.Count > MaxMedia)
        {
            return AppError.Validation("too_many_media", $"Add up to {MaxMedia} photos or videos.");
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

        var id = await social.AddPostAsync(
            new NewPost(userId, body, destinationId, command.TripId, [.. mediaIds.Distinct()]), cancellationToken);

        return id is null
            ? AppError.Validation("media_not_usable", "One of the photos is not yours, failed to upload, or is on another post.")
            : new PostCreated(id.Value);
    }
}

public sealed record CreatePostCommand(string? Body, string? DestinationSlug, long? TripId, IReadOnlyList<long>? MediaIds);

public sealed record PostCreated(long Id);
