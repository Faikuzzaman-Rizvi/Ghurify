using Ghurify.Application.Abstractions;
using Ghurify.Domain.Chat;
using Microsoft.Extensions.Logging;

namespace Ghurify.Application.Chat;

/// <summary>
/// Posts a message to a trip's chat and delivers it live. Members only; only the host can pin an
/// announcement. While any member has not paid yet, phone and wallet numbers are hidden and the
/// sender is told why: paying outside escrow is the risk this guards against.
/// </summary>
public sealed class SendChatMessageHandler(
    IChatRepository chats,
    ChatMembership membership,
    IChatBroadcaster broadcaster,
    ILogger<SendChatMessageHandler> logger)
{
    public const int MaxLength = 2000;

    public async Task<Result<ChatMessageSent>> HandleAsync(
        long userId,
        long tripId,
        SendChatMessageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var body = command.Body?.Trim() ?? string.Empty;
        if (body.Length is 0 or > MaxLength)
        {
            return AppError.Validation("message_length", $"Write between 1 and {MaxLength} characters.");
        }

        var check = await membership.RequireAsync(tripId, userId, cancellationToken);
        if (!check.Succeeded)
        {
            return check.Error!;
        }

        var access = check.Value!;

        if (command.Pin && !access.IsHost)
        {
            return AppError.Forbidden("Only the host can pin an announcement.");
        }

        var masked = false;
        if (access.HasUnpaidMembers)
        {
            (body, masked) = ContactMasker.Apply(body);
        }

        var message = await chats.AddAsync(
            new NewChatMessage(
                tripId,
                userId,
                command.Pin ? ChatMessageKind.Announcement : ChatMessageKind.Message,
                body,
                command.Pin,
                masked),
            cancellationToken);

        await broadcaster.BroadcastAsync(tripId, message, cancellationToken);

        if (masked)
        {
            logger.LogInformation("Hid contact numbers in message {MessageId} on trip {TripId}.", message.Id, tripId);
        }

        return new ChatMessageSent(message, masked);
    }
}
