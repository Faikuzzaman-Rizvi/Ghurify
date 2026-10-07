using System.Security.Claims;
using FluentValidation;

using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// The signed-in user's own profile and identity checks, under /api/v1/me. There is no user id in
/// these routes: the caller can only ever reach their own data.
/// </summary>
public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var me = app.MapGroup("/api/v1/me")
            .WithTags("Profile")
            .RequireAuthorization();

        me.MapGet("/profile", GetProfileAsync)
            .WithName("GetMyProfile")
            .WithSummary("The signed-in user's profile, roles and verification level.")
            .Produces<ProfileDetails>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        me.MapPut("/profile", UpdateProfileAsync)
            .WithName("UpdateMyProfile")
            .WithSummary("Saves the signed-in user's profile.")
            .Produces<ProfileDetails>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        me.MapPost("/roles/host", BecomeHostAsync)
            .WithName("BecomeHost")
            .WithSummary("Turns on hosting. Publishing still needs the selfie identity check.")
            .Produces(StatusCodes.Status204NoContent);

        me.MapGet("/verification", GetVerificationsAsync)
            .WithName("GetMyVerifications")
            .WithSummary("The signed-in user's identity checks, newest first.")
            .Produces<IReadOnlyList<VerificationRecord>>();

        me.MapGet("/verification/documents", ListDocumentsAsync)
            .WithName("ListMyVerificationDocuments")
            .WithSummary("Identity photos uploaded and not yet submitted with a check.")
            .Produces<IReadOnlyList<MyDocumentView>>();

        me.MapPost("/verification/documents", StartDocumentUploadAsync)
            .WithName("StartVerificationDocumentUpload")
            .WithSummary("A short-lived link to upload one identity photo to private storage.")
            .RequireRateLimiting(RateLimitPolicies.Content)
            .Produces<UploadTicket>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        me.MapPost("/verification/documents/{id:long}/complete", CompleteDocumentUploadAsync)
            .WithName("CompleteVerificationDocumentUpload")
            .WithSummary("Checks the uploaded photo and removes its metadata.")
            .Produces<MyDocumentView>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        me.MapDelete("/verification/documents/{id:long}", RemoveDocumentAsync)
            .WithName("RemoveVerificationDocument")
            .WithSummary("Deletes a photo that has not been submitted.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        me.MapPost("/avatar", StartAvatarUploadAsync)
            .WithName("StartAvatarUpload")
            .WithSummary("A short-lived link to upload a profile picture.")
            .RequireRateLimiting(RateLimitPolicies.Content)
            .Produces<UploadTicket>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        me.MapPost("/avatar/complete", CompleteAvatarUploadAsync)
            .WithName("CompleteAvatarUpload")
            .WithSummary("Checks the uploaded photo and makes it the profile picture.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        me.MapDelete("/avatar", RemoveAvatarAsync)
            .WithName("RemoveAvatar")
            .WithSummary("Removes the profile picture.")
            .Produces(StatusCodes.Status204NoContent);

        // Anyone may see a profile picture (trip pages are public). Redirects to a link that works
        // for an hour; the browser caches the redirect briefly so pages stay fast.
        app.MapGet("/api/v1/users/{id:long}/avatar", GetAvatarAsync)
            .WithTags("Profile")
            .WithName("GetAvatar")
            .WithSummary("Redirects to the user's profile picture, or 204 if they have none.")
            .AllowAnonymous()
            .Produces(StatusCodes.Status302Found)
            .Produces(StatusCodes.Status204NoContent);

        me.MapPost("/verification", StartVerificationAsync)
            .WithName("StartVerification")
            .WithSummary("Starts an identity check with the uploaded ID photos: reviewed by the provider or by an admin.")
            .RequireRateLimiting(RateLimitPolicies.Verification)
            .Produces<VerificationRecord>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // The provider calls this; it has no user token, so the body signature is the credential.
        app.MapPost("/api/v1/verification/callback", HandleCallbackAsync)
            .WithTags("Profile")
            .WithName("VerificationCallback")
            .WithSummary("e-KYC provider callback. Signed with HMAC-SHA256 in X-Ekyc-Signature.")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> GetProfileAsync(
        ClaimsPrincipal principal,
        [FromServices] GetProfileHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileCommand command,
        ClaimsPrincipal principal,
        [FromServices] UpdateProfileHandler handler,
        [FromServices] IValidator<UpdateProfileCommand> validator,
        CancellationToken cancellationToken)
    {
        if (await ApiResults.ValidateAsync(validator, command, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), command, cancellationToken));
    }

    private static async Task<IResult> BecomeHostAsync(
        ClaimsPrincipal principal,
        [FromServices] BecomeHostHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    private static async Task<IResult> GetVerificationsAsync(
        ClaimsPrincipal principal,
        [FromServices] GetMyVerificationsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    private static async Task<IResult> StartVerificationAsync(
        StartVerificationRequest request,
        ClaimsPrincipal principal,
        [FromServices] StartVerificationHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(
            principal.RequireUserId(),
            new StartVerificationCommand(request.Level, request.IdNumber, request.DateOfBirth, request.DocumentIds ?? [], request.IdType ?? IdDocumentType.Nid),
            cancellationToken));

    private static async Task<IResult> ListDocumentsAsync(
        ClaimsPrincipal principal,
        [FromServices] ListMyDocumentsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(principal.RequireUserId(), cancellationToken));

    private static async Task<IResult> StartDocumentUploadAsync(
        StartDocumentUploadCommand command,
        ClaimsPrincipal principal,
        [FromServices] StartDocumentUploadHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), command, cancellationToken));

    private static async Task<IResult> CompleteDocumentUploadAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] CompleteDocumentUploadHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> RemoveDocumentAsync(
        long id,
        ClaimsPrincipal principal,
        [FromServices] RemoveDocumentHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> StartAvatarUploadAsync(
        AvatarUploadRequest request,
        ClaimsPrincipal principal,
        [FromServices] StartAvatarUploadHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), request.ContentType, request.SizeBytes, cancellationToken));

    private static async Task<IResult> CompleteAvatarUploadAsync(
        CompleteAvatarCommand command,
        ClaimsPrincipal principal,
        [FromServices] CompleteAvatarUploadHandler handler,
        CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), command, cancellationToken));

    private static async Task<IResult> RemoveAvatarAsync(
        ClaimsPrincipal principal,
        [FromServices] RemoveAvatarHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = principal.RequireUserId();
        return ApiResults.NoContent(await handler.HandleAsync(userId, userId, cancellationToken));
    }

    private static async Task<IResult> GetAvatarAsync(
        long id,
        HttpContext context,
        [FromServices] GetAvatarLinkHandler handler,
        CancellationToken cancellationToken)
    {
        var link = await handler.HandleAsync(id, cancellationToken);
        if (link is null)
        {
            // No picture is a normal answer, not an error: the image fails quietly and the page
            // shows the person's initial. A 404 here put a red "failed to load" line in the browser
            // console for every person without a photo. Unknown ids answer the same, so this never
            // tells anyone which accounts exist.
            context.Response.Headers.CacheControl = "private, max-age=60";
            return Results.NoContent();
        }

        // Shorter than the link's own lifetime, so a cached redirect never points at a dead link.
        context.Response.Headers.CacheControl = "private, max-age=600";
        return Results.Redirect(link.ToString());
    }

    private static async Task<IResult> HandleCallbackAsync(
        HttpRequest request,
        [FromServices] HandleVerificationCallbackHandler handler,
        CancellationToken cancellationToken)
    {
        // The signature covers the exact bytes sent, so the body is read raw, not model-bound.
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);

        return ApiResults.NoContent(await handler.HandleAsync(
            request.Headers["X-Ekyc-Signature"].ToString(), body, cancellationToken));
    }

    /// <summary>The ID number is never echoed back in any response.</summary>
    public sealed record StartVerificationRequest(
        VerificationLevel Level,
        string IdNumber,
        DateOnly DateOfBirth,
        IReadOnlyList<long>? DocumentIds,
        IdDocumentType? IdType);

    public sealed record AvatarUploadRequest(string ContentType, long SizeBytes);
}
