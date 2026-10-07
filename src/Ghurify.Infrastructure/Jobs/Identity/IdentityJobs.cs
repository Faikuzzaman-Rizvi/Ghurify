using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;

namespace Ghurify.Infrastructure.Jobs.Identity;

/// <summary>
/// Daily: deletes identity document images 30 days after their check was decided, and uploads
/// never submitted after 7 days. Idempotent.
/// </summary>
public sealed class PurgeVerificationDocumentsJob(PurgeVerificationDocumentsHandler handler) : IRecurringJob
{
    public const string Id = "identity.purge-verification-documents";

    public Task RunAsync(CancellationToken cancellationToken) => handler.HandleAsync(cancellationToken);
}
