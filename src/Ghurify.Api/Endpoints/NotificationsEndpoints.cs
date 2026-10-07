using System.Security.Claims;
using Ghurify.Application.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Endpoints;

/// <summary>The bell: the signed-in user's notifications. Live delivery is over /hubs/notify.</summary>
public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/me/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        group.MapGet("/", ListAsync)
            .WithName("ListMyNotifications")
            .WithSummary("Your newest notifications and the unread count.")
            .Produces<NotificationPage>();

        group.MapPost("/read", MarkReadAsync)
            .WithName("MarkMyNotificationsRead")
            .WithSummary("Marks your notifications read, up to and including the given id.")
            .Produces(StatusCodes.Status204NoContent);

        return app;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal,
        [FromServices] ListNotificationsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    private static async Task<IResult> MarkReadAsync(
        MarkReadRequest request,
        ClaimsPrincipal principal,
        [FromServices] MarkNotificationsReadHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(principal.RequireUserId(), request.UpToId, cancellationToken);
        return Results.NoContent();
    }

    public sealed record MarkReadRequest(long UpToId);
}
