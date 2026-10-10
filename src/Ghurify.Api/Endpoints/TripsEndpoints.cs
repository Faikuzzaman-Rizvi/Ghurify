using System.Security.Claims;
using FluentValidation;
using Ghurify.Api.Authorization;
using Ghurify.Api.Configuration;
using Ghurify.Application.Trips;
using Ghurify.Domain.Trips;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// Trips and destinations. Discovery (search, a trip's page, destinations) is anonymous on
/// purpose, because people browse before they sign up; a bearer token is still read when one is
/// sent, since who is asking decides whether women-only trips are shown, and lets a host see their
/// own draft. Writing trips needs the Host role, and publishing the VerifiedHost policy.
/// </summary>
public static class TripsEndpoints
{
    public static IEndpointRouteBuilder MapTripsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var trips = app.MapGroup("/api/v1/trips")
            .WithTags("Trips")
            .AllowAnonymous();

        trips.MapGet("/", SearchTripsAsync)
            .WithName("SearchTrips")
            .WithSummary("Searches live trips, soonest first unless another sort is asked for.")
            // Held in memory for a few seconds, for anonymous callers only. Who is asking changes
            // the result (women-only trips, a host's own draft), so CachePolicies refuses any
            // request that carries a token or a cookie: those are always built fresh.
            .CacheOutput(CachePolicies.TripSearch)
            .Produces<TripPage>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        trips.MapGet("/{id:long}", GetTripAsync)
            .WithName("GetTrip")
            .WithSummary("A live trip with its cost breakdown and day-by-day plan.")
            .Produces<TripDetail>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Writes live in their own group: the reads above are anonymous, and AllowAnonymous on a
        // group would silently override any authorization added to an endpoint inside it.
        var hosting = app.MapGroup("/api/v1/trips")
            .WithTags("Trips")
            .RequireAuthorization(Policies.Host);

        hosting.MapPost("/", CreateTripAsync)
            .WithName("CreateTrip")
            .WithSummary("Saves a new trip as a draft.")
            .Produces<TripCreated>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        hosting.MapPut("/{id:long}", UpdateTripAsync)
            .WithName("UpdateTrip")
            .WithSummary("Saves changes to one of your own trips.")
            .Produces<TripDetail>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        hosting.MapPost("/{id:long}/publish", PublishTripAsync)
            .WithName("PublishTrip")
            .WithSummary("Publishes one of your own drafts. Needs the VerifiedHost policy.")
            .RequireAuthorization(Policies.VerifiedHost)
            .Produces<TripDetail>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        hosting.MapPost("/{id:long}/cancel", CancelTripAsync)
            .WithName("CancelTrip")
            .WithSummary("Cancels one of your trips. Every paid traveller is refunded in full, fee included.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/api/v1/me/trips", ListMyHostedTripsAsync)
            .WithTags("Trips")
            .WithName("ListMyHostedTrips")
            .WithSummary("Every trip you host, drafts included.")
            .RequireAuthorization(Policies.Host)
            .Produces<IReadOnlyList<HostTripSummary>>();

        var destinations = app.MapGroup("/api/v1/destinations")
            .WithTags("Destinations")
            .AllowAnonymous();

        destinations.MapGet("/", ListDestinationsAsync)
            .WithName("ListDestinations")
            .WithSummary("Every destination with its safety status and upcoming trips.")
            // A lookup table in practice, and on the home page of every first visit.
            .CacheOutput(CachePolicies.Destinations)
            .Produces<IReadOnlyList<DestinationSummary>>(StatusCodes.Status200OK);

        destinations.MapGet("/{slug}", GetDestinationAsync)
            .WithName("GetDestination")
            .WithSummary("One destination by its slug.")
            .CacheOutput(CachePolicies.Destinations)
            .Produces<DestinationSummary>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> SearchTripsAsync(
        [AsParameters] SearchTripsRequest request,
        ClaimsPrincipal principal,
        [FromServices] SearchTripsHandler handler,
        [FromServices] IValidator<SearchTripsQuery> validator,
        CancellationToken cancellationToken)
    {
        var query = new SearchTripsQuery(
            request.Destination,
            request.From,
            request.To,
            request.MaxPrice,
            request.GroupType,
            request.MinSeats,
            request.Sort ?? TripSort.Soonest,
            request.Page ?? 1,
            request.PageSize ?? SearchTripsQuery.DefaultPageSize,
            request.VerifiedHostsOnly ?? false);

        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        return Results.Ok(await handler.HandleAsync(query, principal.FindUserId(), cancellationToken));
    }

    private static async Task<IResult> GetTripAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] GetTripHandler handler,
        CancellationToken cancellationToken)
    {
        var trip = await handler.HandleAsync(id, principal.FindUserId(), cancellationToken);

        return trip is null ? TripNotFound() : Results.Ok(trip);
    }

    private static async Task<IResult> CreateTripAsync(
        SaveTripCommand command,
        ClaimsPrincipal principal,
        [FromServices] CreateTripHandler handler,
        [FromServices] IValidator<SaveTripCommand> validator,
        CancellationToken cancellationToken)
    {
        if (await ApiResults.ValidateAsync(validator, command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return ApiResults.From(
            await handler.HandleAsync(principal.RequireUserId(), command, cancellationToken),
            created => Results.Created($"/api/v1/trips/{created.Id}", created));
    }

    private static async Task<IResult> UpdateTripAsync(
        long id,
        SaveTripCommand command,
        ClaimsPrincipal principal,
        [FromServices] UpdateTripHandler handler,
        [FromServices] IValidator<SaveTripCommand> validator,
        CancellationToken cancellationToken)
    {
        if (await ApiResults.ValidateAsync(validator, command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken));
    }

    private static async Task<IResult> PublishTripAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] PublishTripHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> CancelTripAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] CancelTripHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> ListMyHostedTripsAsync(
        ClaimsPrincipal principal,
        [FromServices] ListHostTripsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    private static async Task<IResult> ListDestinationsAsync(
        ClaimsPrincipal principal,
        [FromServices] ListDestinationsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(principal.FindUserId(), cancellationToken));

    private static async Task<IResult> GetDestinationAsync(
        string slug,
        ClaimsPrincipal principal,
        [FromServices] ListDestinationsHandler handler,
        CancellationToken cancellationToken)
    {
        var destination = await handler.HandleAsync(slug, principal.FindUserId(), cancellationToken);

        return destination is null
            ? Results.Problem(
                title: "Destination not found",
                detail: "There is no destination with that name.",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(destination);
    }

    /// <summary>
    /// The same answer for a trip that does not exist, a draft, and a trip hidden from this
    /// viewer, so the endpoint reveals nothing about trips the caller may not see.
    /// </summary>
    private static IResult TripNotFound() => Results.Problem(
        title: "Trip not found",
        detail: "This trip does not exist or is no longer available.",
        statusCode: StatusCodes.Status404NotFound);

    /// <summary>Query-string filters for trip search. Every one is optional.</summary>
    public sealed record SearchTripsRequest(
        string? Destination,
        DateOnly? From,
        DateOnly? To,
        decimal? MaxPrice,
        GroupType? GroupType,
        int? MinSeats,
        TripSort? Sort,
        int? Page,
        int? PageSize,
        bool? VerifiedHostsOnly);
}
