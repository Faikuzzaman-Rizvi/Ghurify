using System.Security.Claims;
using Ghurify.Api.Authorization;
using Ghurify.Application.Admin;
using Ghurify.Application.Payments;
using Ghurify.Domain.Identity;
using Ghurify.Domain.Payments;
using Ghurify.Domain.Trips;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// The central admin portal: people, trips, bookings and money, destinations and emergency points.
/// Every change needs a reason and is audited by its use case; every read is role-checked here
/// and again in the use case.
/// </summary>
public static class AdminPortalEndpoints
{
    public static IEndpointRouteBuilder MapAdminPortalEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var admin = app.MapGroup("/api/v1/admin").WithTags("Admin").RequireAuthorization(Policies.Staff);

        admin.MapGet("/users", SearchUsersAsync)
            .WithName("SearchUsers")
            .WithSummary("Finds people by email (start), phone (end) or name.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces<AdminUserPage>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        admin.MapGet("/users/{id:long}", GetUserAsync)
            .WithName("GetUserForAdmin")
            .WithSummary("One person in full: account, roles, identity checks, trips, bookings and reports.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces<AdminUserDetail>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/users/{id:long}/status", SetUserStatusAsync)
            .WithName("SetUserStatus")
            .WithSummary("Suspends, reactivates or closes an account. Suspending signs them out everywhere.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        admin.MapPost("/users/{id:long}/require-password-reset", RequirePasswordResetAsync)
            .WithName("RequirePasswordReset")
            .WithSummary("Makes the password stop working and ends every session; the owner resets it by email.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapGet("/trips", SearchTripsAsync)
            .WithName("SearchAllTrips")
            .WithSummary("Trips in any status, by id, title, host or destination.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces<AdminTripPage>();

        admin.MapPost("/trips/{id:long}/cancel", CancelTripAsync)
            .WithName("CancelTripAsAdmin")
            .WithSummary("Cancels a trip and refunds every paid traveller in full.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        admin.MapGet("/bookings/lookup", LookupBookingAsync)
            .WithName("LookupBooking")
            .WithSummary("A booking with its payments, refunds and escrow balance, by number or payment reference.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces<AdminBookingDetail>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapGet("/payments", SearchPaymentsAsync)
            .WithName("SearchPayments")
            .WithSummary("Every payment, by number, transaction reference, gateway id, traveller email or name, or trip.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces<PaymentHistoryPage>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        admin.MapGet("/payments/{id:long}", GetPaymentAsync)
            .WithName("GetPaymentForAdmin")
            .WithSummary("One payment in full: receipt, payer, gateway settlement, refunds and the callbacks received.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces<AdminPaymentDetail>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/refunds/retry", RetryRefundsAsync)
            .WithName("RetryRefundsNow")
            .WithSummary("Retries every failed refund now.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces<RetriedRefunds>();

        admin.MapPut("/destinations/{slug}", SaveDestinationAsync)
            .WithName("SaveDestination")
            .WithSummary("Adds a destination, or edits its details in both languages.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces<DestinationSaved>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        admin.MapGet("/emergency-points", ListEmergencyPointsAsync)
            .WithName("ListEmergencyPoints")
            .WithSummary("Police stations and hospitals shown with an SOS, and whether each was checked.")
            .RequireAuthorization(Policies.SafetyDesk)
            .Produces<IReadOnlyList<EmergencyPointView>>();

        admin.MapPost("/emergency-points", SaveEmergencyPointAsync)
            .WithName("SaveEmergencyPoint")
            .WithSummary("Adds (no id) or edits an emergency point.")
            .RequireAuthorization(Policies.SafetyDesk)
            .Produces<EmergencyPointSaved>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> SearchUsersAsync(
        ClaimsPrincipal principal,
        [FromServices] SearchUsersHandler handler,
        CancellationToken cancellationToken,
        string? search = null,
        UserStatus? status = null,
        Role? role = null,
        int page = 1) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), search, status, role, page, cancellationToken));

    private static async Task<IResult> GetUserAsync(
        long id, ClaimsPrincipal principal, [FromServices] GetUserDetailHandler handler, CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> SetUserStatusAsync(
        long id, SetUserStatusCommand command, ClaimsPrincipal principal, [FromServices] SetUserStatusHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken));

    private static async Task<IResult> RequirePasswordResetAsync(
        long id, AdminReason command, ClaimsPrincipal principal, [FromServices] RequirePasswordResetHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken));

    private static async Task<IResult> SearchTripsAsync(
        ClaimsPrincipal principal,
        [FromServices] SearchAllTripsHandler handler,
        CancellationToken cancellationToken,
        string? search = null,
        TripStatus? status = null,
        int page = 1) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), search, status, page, cancellationToken));

    private static async Task<IResult> CancelTripAsync(
        long id, AdminReason command, ClaimsPrincipal principal, [FromServices] CancelTripAsAdminHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken));

    private static async Task<IResult> LookupBookingAsync(
        string q, ClaimsPrincipal principal, [FromServices] GetBookingForAdminHandler handler, CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), q, cancellationToken));

    private static async Task<IResult> SearchPaymentsAsync(
        ClaimsPrincipal principal,
        [FromServices] SearchPaymentsHandler handler,
        CancellationToken cancellationToken,
        string? search = null,
        PaymentStatus? status = null,
        int page = 1) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), search, status, page, cancellationToken));

    private static async Task<IResult> GetPaymentAsync(
        long id, ClaimsPrincipal principal, [FromServices] GetPaymentForAdminHandler handler, CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> RetryRefundsAsync(
        ClaimsPrincipal principal, [FromServices] RetryRefundsNowHandler handler, CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    private static async Task<IResult> SaveDestinationAsync(
        string slug, DestinationRequest request, ClaimsPrincipal principal, [FromServices] SaveDestinationHandler handler, CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(
            principal.RequireUserId(),
            new DestinationEdit(
                slug, request.Name, request.NameBn, request.Division, request.DivisionBn, request.Summary, request.SummaryBn,
                request.Kind, request.Latitude, request.Longitude),
            cancellationToken));

    private static async Task<IResult> ListEmergencyPointsAsync(
        ClaimsPrincipal principal, [FromServices] ListEmergencyPointsHandler handler, CancellationToken cancellationToken, string? destination = null) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), destination, cancellationToken));

    private static async Task<IResult> SaveEmergencyPointAsync(
        EmergencyPointEdit edit, ClaimsPrincipal principal, [FromServices] SaveEmergencyPointHandler handler, CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), edit, cancellationToken));

    public sealed record DestinationRequest(
        string Name,
        string NameBn,
        string Division,
        string DivisionBn,
        string Summary,
        string SummaryBn,
        DestinationKind Kind,
        decimal? Latitude,
        decimal? Longitude);
}
