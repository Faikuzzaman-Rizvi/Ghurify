using System.Security.Claims;
using Ghurify.Application.Chat;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// A trip's group chat over HTTP: history (paged), posting (the hub's fallback), read markers and
/// unread counts. Live delivery is over /hubs/chat. Members only, checked on every call.
/// </summary>
public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var chat = app.MapGroup("/api/v1/trips/{id:long}/chat")
            .WithTags("Chat")
            .RequireAuthorization();

        chat.MapGet("/", HistoryAsync)
            .WithName("GetChatHistory")
            .WithSummary("A page of the trip's chat, newest first, plus pinned announcements. Members only.")
            .Produces<ChatHistory>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        chat.MapPost("/", SendAsync)
            .WithName("SendChatMessage")
            .WithSummary("Posts to the trip's chat. Numbers are hidden while anyone has not paid.")
            .RequireRateLimiting(RateLimitPolicies.Content)
            .Produces<ChatMessageSent>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        chat.MapPost("/read", MarkReadAsync)
            .WithName("MarkChatRead")
            .WithSummary("Marks the chat read up to a message.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/v1/me/chats", UnreadAsync)
            .WithTags("Chat")
            .WithName("ListMyChats")
            .WithSummary("Every trip chat you are in, with unread counts.")
            .RequireAuthorization()
            .Produces<IReadOnlyList<ChatUnread>>();

        return app;
    }

    private static async Task<IResult> HistoryAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] GetChatHistoryHandler handler,
        CancellationToken cancellationToken,
        long? before = null) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, before, cancellationToken));

    private static async Task<IResult> SendAsync(
        long id,
        SendChatMessageCommand command,
        ClaimsPrincipal principal,
        [FromServices] SendChatMessageHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken));

    private static async Task<IResult> MarkReadAsync(
        long id,
        MarkChatReadRequest request,
        ClaimsPrincipal principal,
        [FromServices] MarkChatReadHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, request.LastReadId, cancellationToken));

    private static async Task<IResult> UnreadAsync(
        ClaimsPrincipal principal,
        [FromServices] ListChatUnreadHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    public sealed record MarkChatReadRequest(long LastReadId);
}
