using System.Security.Claims;
using Ghurify.Api.Authorization;
using Ghurify.Application.Bookings;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// Joining trips: a traveller asks, the host approves (holding a seat for 30 minutes) or declines,
/// and either side sees where things stand. Ownership is checked in each use case and again in SQL.
/// </summary>
public static class BookingsEndpoints
{
    public static IEndpointRouteBuilder MapBookingsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/api/v1/trips/{id:long}/join-requests", RequestToJoinAsync)
            .WithTags("Bookings")
            .WithName("RequestToJoin")
            .WithSummary("Asks to join a live trip. Needs a verified national ID.")
            .RequireAuthorization(Policies.VerifiedTraveler)
            .RequireRateLimiting(RateLimitPolicies.JoinRequests)
            .Produces<JoinRequestCreated>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/api/v1/trips/{id:long}/join-requests", ListTripRequestsAsync)
            .WithTags("Bookings")
            .WithName("ListTripJoinRequests")
            .WithSummary("Every request for one of your trips, pending first.")
            .RequireAuthorization(Policies.Host)
            .Produces<IReadOnlyList<JoinRequestForHost>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var requests = app.MapGroup("/api/v1/join-requests")
            .WithTags("Bookings")
            .RequireAuthorization();

        requests.MapPost("/{id:long}/approve", ApproveAsync)
            .WithName("ApproveJoinRequest")
            .WithSummary("Approves a request on your trip and holds a seat until the payment deadline.")
            .RequireAuthorization(Policies.Host)
            .Produces<JoinRequestApproved>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        requests.MapPost("/{id:long}/decline", DeclineAsync)
            .WithName("DeclineJoinRequest")
            .WithSummary("Declines a pending request on your trip.")
            .RequireAuthorization(Policies.Host)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        requests.MapPost("/{id:long}/cancel", CancelAsync)
            .WithName("CancelJoinRequest")
            .WithSummary("Withdraws your own request before paying; an unpaid seat is released.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/api/v1/me/bookings", ListMineAsync)
            .WithTags("Bookings")
            .WithName("ListMyBookings")
            .WithSummary("Your requests and bookings, newest trip first.")
            .RequireAuthorization()
            .Produces<IReadOnlyList<MyTripBooking>>();

        return app;
    }

    private static async Task<IResult> RequestToJoinAsync(
        long id,
        RequestToJoinCommand command,
        ClaimsPrincipal principal,
        [FromServices] RequestToJoinHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken),
            created => Results.Created($"/api/v1/join-requests/{created.Id}", created));

    private static async Task<IResult> ListTripRequestsAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] ListTripJoinRequestsHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> ApproveAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] ApproveJoinRequestHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> DeclineAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] DeclineJoinRequestHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> CancelAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] CancelJoinRequestHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> ListMineAsync(
        ClaimsPrincipal principal,
        [FromServices] ListMyBookingsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));
}
