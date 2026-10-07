using System.Security.Claims;
using Ghurify.Api.Authorization;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Application.Payments;
using Ghurify.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// The admin desk. Each route carries a policy, and each use case checks the role again, so a
/// mistake in one layer does not open the other.
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var admin = app.MapGroup("/api/v1/admin")
            .WithTags("Admin")
            .RequireAuthorization(Policies.Staff);

        admin.MapGet("/verifications", QueryVerificationsAsync)
            .WithName("QueryVerificationQueue")
            .WithSummary("Identity checks in one status, oldest first.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces<VerificationQueuePage>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        admin.MapGet("/verifications/{id:long}/documents", VerificationDocumentsAsync)
            .WithName("GetVerificationDocuments")
            .WithSummary("The ID photos of one check, as links that work for five minutes. Each viewing is audited.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces<IReadOnlyList<ReviewDocumentView>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        admin.MapDelete("/users/{id:long}/avatar", RemoveUserAvatarAsync)
            .WithName("RemoveUserAvatar")
            .WithSummary("Removes someone's profile picture (for example, an inappropriate one). Audited.")
            .RequireAuthorization(Policies.Moderator)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        admin.MapPost("/verifications/{id:long}/review", ReviewVerificationAsync)
            .WithName("ReviewVerification")
            .WithSummary("Approves or rejects a pending identity check. A rejection needs a reason.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPost("/users/{id:long}/roles", ChangeRoleAsync)
            .WithName("ChangeUserRole")
            .WithSummary("Grants or revokes a role.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapGet("/payouts", QueryPayoutsAsync)
            .WithName("QueryPayoutQueue")
            .WithSummary("Payouts in one status; Released ones are waiting to be sent to hosts.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces<IReadOnlyList<PayoutView>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        admin.MapPost("/payouts/{id:long}/approve", ApprovePayoutAsync)
            .WithName("ApprovePayout")
            .WithSummary("Confirms a released payout has been sent to the host. Audited.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapGet("/audit", QueryAuditAsync)
            .WithName("QueryAuditLog")
            .WithSummary("Recent admin and safety-desk actions, newest first.")
            .RequireAuthorization(Policies.AdminOnly)
            .Produces<IReadOnlyList<AuditEntry>>();

        return app;
    }

    private static async Task<IResult> QueryVerificationsAsync(
        ClaimsPrincipal principal,
        [FromServices] QueryVerificationQueueHandler handler,
        CancellationToken cancellationToken,
        VerificationStatus status = VerificationStatus.Pending,
        int page = 1) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), status, page, cancellationToken));

    private static async Task<IResult> ReviewVerificationAsync(
        long id,
        ReviewVerificationCommand command,
        ClaimsPrincipal principal,
        [FromServices] ReviewVerificationHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken));

    private static async Task<IResult> VerificationDocumentsAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] GetVerificationDocumentsHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> RemoveUserAvatarAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] RemoveAvatarHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> ChangeRoleAsync(
        long id,
        ChangeRoleRequest request,
        ClaimsPrincipal principal,
        [FromServices] ChangeRoleHandler handler,
        [FromServices] IAuditLog audit,
        CancellationToken cancellationToken)
    {
        var actorId = principal.RequireUserId();
        var result = await handler.HandleAsync(actorId, id, request.Role, request.Grant, cancellationToken);

        if (result.Succeeded)
        {
            await audit.WriteAsync(
                actorId, request.Grant ? "role.grant" : "role.revoke", "User", id, request.Role.ToString(), cancellationToken);
        }

        return ApiResults.NoContent(result);
    }

    private static async Task<IResult> QueryPayoutsAsync(
        ClaimsPrincipal principal,
        [FromServices] ListPayoutQueueHandler handler,
        CancellationToken cancellationToken,
        PayoutStatus status = PayoutStatus.Released) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), status, cancellationToken));

    private static async Task<IResult> ApprovePayoutAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] ApprovePayoutHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> QueryAuditAsync(
        [FromServices] IAuditLog audit,
        CancellationToken cancellationToken,
        string? entityType = null,
        long? entityId = null) =>
        Results.Ok(await audit.QueryAsync(entityType, entityId, take: 100, cancellationToken));

    public sealed record ChangeRoleRequest(Role Role, bool Grant);
}
