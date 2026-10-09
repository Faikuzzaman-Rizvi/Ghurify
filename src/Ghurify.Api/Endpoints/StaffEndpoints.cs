using System.Security.Claims;
using FluentValidation;
using Ghurify.Api.Authorization;
using Ghurify.Application.Admin;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// The super admin's own section: who is on the admin desk, what each role may do, and the
/// password confirmation that the dangerous calls here demand.
/// </summary>
public static class StaffEndpoints
{
    public static IEndpointRouteBuilder MapStaffEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var staff = app.MapGroup("/api/v1/admin/staff")
            .WithTags("Admin")
            .RequireAuthorization(Policies.Staff);

        staff.MapGet("/roles", ListRolesAsync)
            .WithName("ListStaffRoles")
            .WithSummary("Every admin role, every permission the platform has, and which of them the caller may hand out.")
            .RequireAuthorization(Policies.Require(Permissions.StaffView))
            .Produces<StaffRolesView>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        staff.MapPost("/roles", CreateRoleAsync)
            .WithName("CreateStaffRole")
            .WithSummary("Creates an admin role. Needs the password again.")
            .RequireAuthorization(Policies.Require(Permissions.StaffRolesManage))
            .RequireStepUp()
            .Produces<StaffRoleView>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        staff.MapPut("/roles/{id:long}", UpdateRoleAsync)
            .WithName("UpdateStaffRole")
            .WithSummary("Renames a role or changes what it may do. Needs the password again.")
            .RequireAuthorization(Policies.Require(Permissions.StaffRolesManage))
            .RequireStepUp()
            .Produces<StaffRoleView>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        staff.MapDelete("/roles/{id:long}", DeleteRoleAsync)
            .WithName("DeleteStaffRole")
            .WithSummary("Removes an unused role. Refuses a built-in one, or one somebody still holds.")
            .RequireAuthorization(Policies.Require(Permissions.StaffRolesManage))
            .RequireStepUp()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        staff.MapGet("/members", ListMembersAsync)
            .WithName("ListStaffMembers")
            .WithSummary("Everybody on the admin desk, with the roles each holds and who granted them.")
            .RequireAuthorization(Policies.Require(Permissions.StaffView))
            .Produces<StaffMembersView>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        staff.MapPost("/members/{userId:long}", AssignRoleAsync)
            .WithName("AssignStaffRole")
            .WithSummary("Grants or revokes one admin role. Needs the password again.")
            .RequireAuthorization(Policies.Require(Permissions.StaffAssign))
            .RequireStepUp()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // Not behind a permission: confirming your own password is how you prove who you are,
        // so every staff member can reach it. Rate-limited like the other credential endpoints.
        app.MapPost("/api/v1/admin/step-up", StepUpAsync)
            .WithTags("Admin")
            .WithName("StartStepUp")
            .WithSummary("Confirms the caller's password and returns a short-lived receipt for the actions that need one.")
            .RequireAuthorization(Policies.Staff)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Produces<StepUpResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    private static async Task<IResult> ListRolesAsync(
        ClaimsPrincipal principal,
        [FromServices] ListStaffRolesHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    private static Task<IResult> CreateRoleAsync(
        SaveStaffRoleCommand command,
        ClaimsPrincipal principal,
        [FromServices] SaveStaffRoleHandler handler,
        [FromServices] IValidator<SaveStaffRoleCommand> validator,
        CancellationToken cancellationToken) =>
        SaveAsync(null, command, principal, handler, validator, cancellationToken);

    private static Task<IResult> UpdateRoleAsync(
        long id,
        SaveStaffRoleCommand command,
        ClaimsPrincipal principal,
        [FromServices] SaveStaffRoleHandler handler,
        [FromServices] IValidator<SaveStaffRoleCommand> validator,
        CancellationToken cancellationToken) =>
        SaveAsync(id, command, principal, handler, validator, cancellationToken);

    private static async Task<IResult> SaveAsync(
        long? id,
        SaveStaffRoleCommand command,
        ClaimsPrincipal principal,
        SaveStaffRoleHandler handler,
        IValidator<SaveStaffRoleCommand> validator,
        CancellationToken cancellationToken)
    {
        if (await ApiResults.ValidateAsync(validator, command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return ApiResults.Ok(
            await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken));
    }

    private static async Task<IResult> DeleteRoleAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] DeleteStaffRoleHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> ListMembersAsync(
        ClaimsPrincipal principal,
        [FromServices] ListStaffMembersHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    private static async Task<IResult> AssignRoleAsync(
        long userId,
        AssignStaffRoleCommand command,
        ClaimsPrincipal principal,
        [FromServices] AssignStaffRoleHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(
            await handler.HandleAsync(principal.RequireUserId(), userId, command, cancellationToken));

    private static async Task<IResult> StepUpAsync(
        StepUpRequest request,
        ClaimsPrincipal principal,
        [FromServices] StartStepUpHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(principal.RequireUserId(), request.Password, cancellationToken);

        return ApiResults.From(result, token => Results.Ok(
            new StepUpResponse(token.Value, token.ExpiresOn, token.ExpiresInSeconds)));
    }

    /// <summary>The admin's own password, to prove they are at the keyboard.</summary>
    public sealed record StepUpRequest(string? Password);

    /// <summary>
    /// The receipt the portal sends back in <see cref="StepUpFilter.HeaderName"/>. Short-lived
    /// and tied to this account; it carries no permission of its own.
    /// </summary>
    public sealed record StepUpResponse(string Token, DateTimeOffset ExpiresOn, int ExpiresInSeconds);
}
