using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Notifications;

/// <summary>
/// Tells a user something: stores it (so the bell shows it later) and pushes it live.
///
/// Idempotent by <c>dedupeKey</c>: the same event raised twice (a retried job, a webhook delivered
/// twice) is stored and pushed once. A failed push is logged and swallowed, because the
/// notification is already stored and the user will see it on their next visit.
/// </summary>
public sealed class NotificationService(
    INotificationRepository notifications,
    IRealtimeNotifier realtime,
    ILogger<NotificationService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task NotifyAsync(
        long userId,
        string kind,
        string dedupeKey,
        object? data,
        CancellationToken cancellationToken)
    {
        var json = data is null ? null : JsonSerializer.Serialize(data, Json);
        var stored = await notifications.AddAsync(userId, kind, json, dedupeKey, cancellationToken);

        if (stored is null)
        {
            logger.LogDebug("Notification {Kind} for user {UserId} already sent; skipped.", kind, userId);
            return;
        }

        await PushAsync(userId, stored, cancellationToken);
    }

    /// <summary>
    /// Tells many people at once (a trip cancelled, holds released): one database call for the whole
    /// batch, then a live push for each one actually stored.
    /// </summary>
    public async Task NotifyManyAsync(IReadOnlyCollection<NotificationRequest> requests, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0)
        {
            return;
        }

        var stored = await notifications.AddManyAsync(
            [.. requests
                .DistinctBy(request => (request.UserId, request.DedupeKey))
                .Select(request => (
                    request.UserId,
                    request.Kind,
                    request.Data is null ? null : JsonSerializer.Serialize(request.Data, Json),
                    request.DedupeKey))],
            cancellationToken);

        foreach (var (userId, item) in stored)
        {
            await PushAsync(userId, item, cancellationToken);
        }
    }

    private async Task PushAsync(long userId, NotificationItem item, CancellationToken cancellationToken)
    {
        try
        {
            await realtime.PushAsync(userId, item, cancellationToken);
        }
#pragma warning disable CA1031 // The notification is stored; a push failure must not fail the action that raised it.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "Could not push notification {NotificationId} to user {UserId}.", item.Id, userId);
        }
    }
}
