namespace Ghurify.Application.Abstractions;

/// <summary>
/// Records who did what on the admin and safety desks. Every admin action writes one entry,
/// so any decision about a user, a payout or a destination can be traced to a person.
/// </summary>
public interface IAuditLog
{
    /// <param name="action">Dotted verb, e.g. <c>verification.approve</c>.</param>
    /// <param name="note">Free text the actor gave, such as a rejection reason. Never personal data.</param>
    Task WriteAsync(
        long actorId,
        string action,
        string entityType,
        long entityId,
        string? note,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEntry>> QueryAsync(string? entityType, long? entityId, int take, CancellationToken cancellationToken);
}

public sealed record AuditEntry(
    long Id,
    long ActorId,
    string? ActorName,
    string Action,
    string EntityType,
    long EntityId,
    string? Note,
    DateTimeOffset Created);
