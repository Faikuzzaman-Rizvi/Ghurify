using System.Security.Claims;
using FluentValidation;
using Ghurify.Application.Trips;
using Ghurify.Domain.Trips;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// Trip discovery: search, a trip's page, and destinations. Anonymous on purpose, because people
/// browse before they sign up. A bearer token is still read when one is sent, since who is
/// asking decides whether women-only trips are shown.
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
            .Produces<TripPage>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        trips.MapGet("/{id:long}", GetTripAsync)
            .WithName("GetTrip")
            .WithSummary("A live trip with its cost breakdown and day-by-day plan.")
            .Produces<TripDetail>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var destinations = app.MapGroup("/api/v1/destinations")
            .WithTags("Destinations")
            .AllowAnonymous();

        destinations.MapGet("/", ListDestinationsAsync)
            .WithName("ListDestinations")
            .WithSummary("Every destination with its safety status and upcoming trips.")
            .Produces<IReadOnlyList<DestinationSummary>>(StatusCodes.Status200OK);

        destinations.MapGet("/{slug}", GetDestinationAsync)
            .WithName("GetDestination")
            .WithSummary("One destination by its slug.")
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
            request.PageSize ?? SearchTripsQuery.DefaultPageSize);

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
        int? PageSize);
}
