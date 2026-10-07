namespace Ghurify.Application.Identity;

/// <summary>The person's own photos waiting to be submitted with a check.</summary>
public sealed class ListMyDocumentsHandler(IVerificationDocumentRepository documents)
{
    public async Task<IReadOnlyList<MyDocumentView>> HandleAsync(long userId, CancellationToken cancellationToken) =>
        [.. (await documents.QueryUnsubmittedAsync(userId, cancellationToken))
            .Select(document => new MyDocumentView(document.Id, document.Kind, document.Status, document.FailureReason, document.Created))];
}
