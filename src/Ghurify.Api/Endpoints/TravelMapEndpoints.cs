using System.Security.Claims;
using Ghurify.Application.Trips;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// Travel maps: the places in Bangladesh someone has been, from Ghurify trips (recorded as each trip
/// completes) and the ones they add themselves, with trips to come. Private to the owner; shared on
/// their public profile only if they choose, and then only the Ghurify destinations.
/// </summary>
public static class TravelMapEndpoints
{
    public static IEndpointRouteBuilder MapTravelMapEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var mine = app.MapGroup("/api/v1/me/travel-map").WithTags("Trips").RequireAuthorization();

        mine.MapGet("/", GetMineAsync)
            .WithName("GetMyTravelMap")
            .WithSummary("Your travel map: every place you have been, your notes and photos, and trips to come.")
            .Produces<TravelMap>();

        mine.MapPost("/visits", AddVisitAsync)
            .WithName("AddVisit")
            .WithSummary("Adds a place you have been: a Ghurify destination, or a named pin anywhere in Bangladesh.")
            .RequireRateLimiting(RateLimitPolicies.Content)
            .Produces<VisitCreated>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        mine.MapPut("/visits/{id:long}", EditVisitAsync)
            .WithName("EditVisit")
            .WithSummary("Changes a visit's note, and the date of one you added.")
            .RequireRateLimiting(RateLimitPolicies.Content)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        mine.MapDelete("/visits/{id:long}", RemoveVisitAsync)
            .WithName("RemoveVisit")
            .WithSummary("Takes a visit off your map.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        mine.MapPost("/visits/{id:long}/photos", AddPhotosAsync)
            .WithName("AddVisitPhotos")
            .WithSummary("Puts photos you uploaded on one of your visits. Only you see them on your map.")
            .RequireRateLimiting(RateLimitPolicies.Content)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        mine.MapDelete("/visits/{id:long}/photos/{mediaId:long}", RemovePhotoAsync)
            .WithName("RemoveVisitPhoto")
            .WithSummary("Takes a photo off one of your visits.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        mine.MapPut("/sharing", SetSharingAsync)
            .WithName("SetTravelMapSharing")
            .WithSummary("Shows or hides your travel map on your public profile.")
            .Produces(StatusCodes.Status204NoContent);

        // Anyone may look: an unshared map simply comes back empty, with shared = false.
        app.MapGet("/api/v1/users/{id:long}/travel-map", GetSharedAsync)
            .WithTags("Trips")
            .WithName("GetSharedTravelMap")
            .WithSummary("Someone's travel map, if they share it: the Ghurify destinations they have been to.")
            .AllowAnonymous()
            .Produces<TravelMap>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetMineAsync(ClaimsPrincipal principal, [FromServices] GetMyTravelMapHandler handler, CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    private static async Task<IResult> AddVisitAsync(AddVisitCommand command, ClaimsPrincipal principal, [FromServices] AddVisitHandler handler, CancellationToken cancellationToken) =>
        ApiResults.From(
            await handler.HandleAsync(principal.RequireUserId(), command, cancellationToken),
            created => Results.Created($"/api/v1/me/travel-map/visits/{created.Id}", created));

    private static async Task<IResult> EditVisitAsync(long id, EditVisitCommand command, ClaimsPrincipal principal, [FromServices] EditVisitHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken));

    private static async Task<IResult> RemoveVisitAsync(long id, ClaimsPrincipal principal, [FromServices] RemoveVisitHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> AddPhotosAsync(long id, VisitPhotosCommand command, ClaimsPrincipal principal, [FromServices] AddVisitPhotosHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken));

    private static async Task<IResult> RemovePhotoAsync(long id, long mediaId, ClaimsPrincipal principal, [FromServices] RemoveVisitPhotoHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, mediaId, cancellationToken));

    private static async Task<IResult> SetSharingAsync(TravelMapSharingCommand command, ClaimsPrincipal principal, [FromServices] SetTravelMapSharingHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), command, cancellationToken));

    private static async Task<IResult> GetSharedAsync(long id, [FromServices] GetSharedTravelMapHandler handler, CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(id, cancellationToken));
}
