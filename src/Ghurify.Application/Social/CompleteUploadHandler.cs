using Ghurify.Application.Abstractions;

namespace Ghurify.Application.Social;

/// <summary>
/// The browser says its upload finished: the media moves to Processing and the processing job is
/// queued with its id. Only the owner can do this, and only once.
/// </summary>
public sealed class CompleteUploadHandler(ISocialRepository social, IBackgroundJobs jobs)
{
    public async Task<Result<Done>> HandleAsync(long userId, long mediaId, CancellationToken cancellationToken)
    {
        if (!await social.SetMediaProcessingAsync(mediaId, userId, cancellationToken))
        {
            return AppError.NotFound("media_not_found", "There is no such upload waiting to be finished.");
        }

        await jobs.EnqueueAsync<IProcessMediaJob>(mediaId, cancellationToken);
        return Done.Value;
    }
}

/// <summary>The media processing job, queued by id. Implemented in Infrastructure.</summary>
public interface IProcessMediaJob : IIdJob;
