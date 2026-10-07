using System.Security.Claims;
using Ghurify.Api.Authorization;
using Ghurify.Application.Safety;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// Safety: SOS (raise, live position, "I'm safe"), check-ins, reports, and the safety desk's side of
/// each. Every admin or desk action is audited by the use case.
/// </summary>
public static class SafetyEndpoints
{
    public static IEndpointRouteBuilder MapSafetyEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var trips = app.MapGroup("/api/v1/trips/{id:long}").WithTags("Safety").RequireAuthorization();

        trips.MapPost("/sos", RaiseSosAsync)
            .WithName("RaiseSos")
            .WithSummary("Raises an SOS: the safety desk sees it live and your emergency contact is texted.")
            .Produces<SosRaisedView>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        trips.MapGet("/check-ins", ListCheckInsAsync)
            .WithName("ListCheckIns")
            .WithSummary("The trip's safety check-ins. For the people on the trip.")
            .Produces<IReadOnlyList<CheckInView>>();

        trips.MapPost("/check-ins", ScheduleCheckInAsync)
            .WithName("ScheduleCheckIn")
            .WithSummary("Schedules a safety check-in on your own trip.")
            .RequireAuthorization(Policies.Host)
            .Produces<CheckInScheduled>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var sos = app.MapGroup("/api/v1/sos/{id:long}").WithTags("Safety").RequireAuthorization();

        sos.MapPost("/location", UpdateLocationAsync)
            .WithName("UpdateSosLocation")
            .WithSummary("Sends your current position while your SOS is open.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sos.MapPost("/resolve", ResolveSosAsync)
            .WithName("ResolveSos")
            .WithSummary("Closes an SOS: the person who raised it is safe, or the desk has handled it.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/api/v1/check-ins/{id:long}/done", CompleteCheckInAsync)
            .WithTags("Safety")
            .WithName("CompleteCheckIn")
            .WithSummary("Says the group is safe at this check-in.")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/api/v1/reports", FileReportAsync)
            .WithTags("Safety")
            .WithName("FileReport")
            .WithSummary("Reports a person, a story or a trip, or raises a dispute about a booking.")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Content)
            .Produces<ReportFiled>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var admin = app.MapGroup("/api/v1/admin").WithTags("Admin").RequireAuthorization(Policies.Staff);

        admin.MapGet("/dashboard", DashboardAsync)
            .WithName("GetAdminDashboard")
            .Produces<DashboardCounts>();

        admin.MapGet("/sos", SosBoardAsync)
            .WithName("GetSosBoard")
            .RequireAuthorization(Policies.SafetyDesk)
            .Produces<IReadOnlyList<SosBoardItem>>();

        admin.MapPost("/sos/{id:long}/acknowledge", AcknowledgeSosAsync)
            .WithName("AcknowledgeSos")
            .RequireAuthorization(Policies.SafetyDesk)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapGet("/check-ins/missed", MissedCheckInsAsync)
            .WithName("ListMissedCheckIns")
            .RequireAuthorization(Policies.SafetyDesk)
            .Produces<IReadOnlyList<MissedCheckIn>>();

        admin.MapPost("/destinations/{slug}/status", ChangeDestinationStatusAsync)
            .WithName("ChangeDestinationStatus")
            .WithSummary("Sets a destination Open, Caution or Closed. Closing cancels its trips and refunds everyone.")
            .RequireAuthorization(Policies.SafetyDesk)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapGet("/reports", ReportsAsync)
            .WithName("ListReports")
            .RequireAuthorization(Policies.Moderator)
            .Produces<IReadOnlyList<ReportView>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        admin.MapPost("/reports/{id:long}/resolve", ResolveReportAsync)
            .WithName("ResolveReport")
            .RequireAuthorization(Policies.Moderator)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> RaiseSosAsync(long id, RaiseSosCommand command, ClaimsPrincipal principal, [FromServices] RaiseSosHandler handler, CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken));

    private static async Task<IResult> ListCheckInsAsync(long id, ClaimsPrincipal principal, [FromServices] ListCheckInsHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> ScheduleCheckInAsync(long id, ScheduleCheckInCommand command, ClaimsPrincipal principal, [FromServices] ScheduleCheckInHandler handler, CancellationToken cancellationToken) =>
        ApiResults.From(
            await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken),
            scheduled => Results.Created($"/api/v1/check-ins/{scheduled.Id}", scheduled));

    private static async Task<IResult> UpdateLocationAsync(long id, SosLocationRequest request, ClaimsPrincipal principal, [FromServices] UpdateSosLocationHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, request.Latitude, request.Longitude, cancellationToken));

    private static async Task<IResult> ResolveSosAsync(long id, ClaimsPrincipal principal, [FromServices] SetSosStatusHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, SosStatus.Resolved, cancellationToken));

    private static async Task<IResult> AcknowledgeSosAsync(long id, ClaimsPrincipal principal, [FromServices] SetSosStatusHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, SosStatus.Acknowledged, cancellationToken));

    private static async Task<IResult> CompleteCheckInAsync(long id, CheckInDoneRequest request, ClaimsPrincipal principal, [FromServices] CompleteCheckInHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, request.Note, cancellationToken));

    private static async Task<IResult> FileReportAsync(FileReportCommand command, ClaimsPrincipal principal, [FromServices] FileReportHandler handler, CancellationToken cancellationToken) =>
        ApiResults.From(
            await handler.HandleAsync(principal.RequireUserId(), command, cancellationToken),
            filed => Results.Created($"/api/v1/reports/{filed.Id}", filed));

    private static async Task<IResult> DashboardAsync(ClaimsPrincipal principal, [FromServices] GetDashboardHandler handler, CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    private static async Task<IResult> SosBoardAsync(ClaimsPrincipal principal, [FromServices] GetSosBoardHandler handler, CancellationToken cancellationToken, bool includeResolved = false) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), includeResolved, cancellationToken));

    private static async Task<IResult> MissedCheckInsAsync(ClaimsPrincipal principal, [FromServices] ListMissedCheckInsHandler handler, CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    private static async Task<IResult> ChangeDestinationStatusAsync(string slug, ChangeDestinationStatusCommand command, ClaimsPrincipal principal, [FromServices] ChangeDestinationStatusHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), slug, command, cancellationToken));

    private static async Task<IResult> ReportsAsync(ClaimsPrincipal principal, [FromServices] ListReportsHandler handler, CancellationToken cancellationToken, ReportKind? kind = null, ReportStatus status = ReportStatus.Open) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), kind, status, cancellationToken));

    private static async Task<IResult> ResolveReportAsync(long id, ResolveReportCommand command, ClaimsPrincipal principal, [FromServices] ResolveReportHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken));

    public sealed record SosLocationRequest(decimal Latitude, decimal Longitude);

    public sealed record CheckInDoneRequest(string? Note);
}
