using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ghurify.Application.Abstractions;

/// <summary>
/// Records who did what on the admin and safety desks. Every admin action writes one entry,
/// so any decision about a user, a payout, a destination or the site's own settings can be
/// traced to a person, and anything they changed can be read back field by field.
/// </summary>
public interface IAuditLog
{
    Task WriteAsync(AuditRecord entry, CancellationToken cancellationToken);

    /// <summary>One page of the trail, newest first, with the matching total on every row.</summary>
    Task<AuditPage> QueryAsync(AuditQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// One thing that happened.
/// </summary>
/// <param name="Action">Dotted verb, e.g. <c>verification.approve</c>.</param>
/// <param name="EntityType">What it was done to: <c>User</c>, <c>Payout</c>, <c>StaffRole</c>.</param>
/// <param name="Note">Free text the actor gave, such as a rejection reason. Never personal data.</param>
/// <param name="Changes">
/// What changed, as JSON from <see cref="AuditChanges"/>. Null where the action is the whole
/// story. Never personal data and never a secret: a setting holding a credential records that
/// it changed, not to what.
/// </param>
public sealed record AuditRecord(
    long ActorId,
    string Action,
    string EntityType,
    long EntityId,
    string? Note = null,
    string? Changes = null);

/// <summary>
/// Which entries to read. Every filter is optional; an <see cref="Action"/> ending in a dot
/// (<c>payout.</c>) matches every action in that group.
/// </summary>
public sealed record AuditQuery(
    long? ActorId = null,
    string? Action = null,
    string? EntityType = null,
    long? EntityId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 50);

public sealed record AuditPage(IReadOnlyList<AuditEntry> Entries, int Total, int Page, int PageSize);

/// <param name="Changes">The before/after pairs, already parsed, so the viewer renders them directly.</param>
public sealed record AuditEntry(
    long Id,
    long ActorId,
    string? ActorName,
    string Action,
    string EntityType,
    long EntityId,
    string? Note,
    IReadOnlyList<AuditChange> Changes,
    DateTimeOffset Created);

/// <summary>One field that moved, as the audit viewer shows it.</summary>
public sealed record AuditChange(string Field, string? From, string? To);

/// <summary>
/// Collects the fields a use case actually changed, so the audit row says what moved rather than
/// only that something did.
///
/// Values are kept as text because the trail has to stay readable years later, when the type
/// that produced them may be long gone. Only genuine changes are recorded: saving a form without
/// touching anything writes no change set at all, which keeps the viewer honest about what
/// happened.
/// </summary>
public sealed class AuditChanges
{
    private static readonly JsonSerializerOptions Format = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<string, AuditChange> _changes = [];

    /// <summary>Records <paramref name="field"/> moving, or does nothing if the two are equal.</summary>
    public AuditChanges Set(string field, string? from, string? to)
    {
        if (!string.Equals(from, to, StringComparison.Ordinal))
        {
            _changes[field] = new AuditChange(field, from, to);
        }

        return this;
    }

    /// <summary>The same for anything with a sensible text form: numbers, enums, dates, bools.</summary>
    public AuditChanges Set<T>(string field, T? from, T? to) =>
        Set(field, Text(from), Text(to));

    /// <summary>
    /// Records that a set of things changed, listing both sides in a stable order. Used for a
    /// role's permissions, where the interesting part is which ones came and went.
    /// </summary>
    public AuditChanges SetMany(string field, IEnumerable<string> from, IEnumerable<string> to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        return Set(
            field,
            string.Join(", ", from.Order(StringComparer.Ordinal)),
            string.Join(", ", to.Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// Records that something changed without saying to what, for a value that must never be
    /// written down: an SMTP password, a gateway key.
    /// </summary>
    public AuditChanges SetSecret(string field, bool changed)
    {
        if (changed)
        {
            _changes[field] = new AuditChange(field, null, "(changed)");
        }

        return this;
    }

    public bool Any => _changes.Count > 0;

    /// <summary>The JSON for <see cref="AuditRecord.Changes"/>, or null if nothing changed.</summary>
    public string? ToJson() =>
        _changes.Count == 0
            ? null
            : JsonSerializer.Serialize(
                _changes.Values
                    .OrderBy(change => change.Field, StringComparer.Ordinal)
                    .ToDictionary(
                        change => change.Field,
                        change => new Pair(change.From, change.To),
                        StringComparer.Ordinal),
                Format);

    /// <summary>Reads a stored change set back. An unreadable one yields nothing rather than throwing.</summary>
    public static IReadOnlyList<AuditChange> FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var pairs = JsonSerializer.Deserialize<Dictionary<string, Pair>>(json);
            return pairs is null
                ? []
                : [.. pairs
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new AuditChange(pair.Key, pair.Value.From, pair.Value.To))];
        }
        catch (JsonException)
        {
            // Written by an older release in a shape this one cannot read. The row itself still
            // says who did what and when, which is the part that must never be lost.
            return [];
        }
    }

    private static string? Text<T>(T? value) => value switch
    {
        null => null,
        bool flag => flag ? "true" : "false",
        DateTimeOffset moment => moment.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        IFormattable number => number.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    private sealed record Pair(
        [property: JsonPropertyName("from")] string? From,
        [property: JsonPropertyName("to")] string? To);
}
