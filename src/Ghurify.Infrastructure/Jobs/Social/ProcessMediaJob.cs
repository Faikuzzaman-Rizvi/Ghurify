using Ghurify.Application.Social;

namespace Ghurify.Infrastructure.Jobs.Social;

/// <summary>Checks an upload and strips its location data. Queued with the media id only; idempotent.</summary>
public sealed class ProcessMediaJob(ProcessMediaHandler handler) : IProcessMediaJob
{
    public Task RunAsync(long id, CancellationToken cancellationToken) => handler.HandleAsync(id, cancellationToken);
}
