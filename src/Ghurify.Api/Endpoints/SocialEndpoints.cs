using System.Security.Claims;
using Ghurify.Application.Admin;
using Ghurify.Application.Social;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// Stories and the people behind them: media uploads (straight to storage with short links), the
/// feed, likes, comments, follows, public profiles and reviews after a trip.
/// Reading is open (people browse stories before signing up); writing needs a signed-in user, and
/// every write to someone's own content is filtered by their id.
/// </summary>
public static class SocialEndpoints
{
    public static IEndpointRouteBuilder MapSocialEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var media = app.MapGroup("/api/v1/media").WithTags("Social").RequireAuthorization();

        media.MapPost("/upload-url", UploadUrlAsync)
            .WithName("CreateMediaUploadUrl")
            .WithSummary("A short-lived link to upload one photo or video straight to storage.")
            .RequireRateLimiting(RateLimitPolicies.Content)
            .Produces<UploadLink>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        media.MapPost("/{id:long}/complete", CompleteUploadAsync)
            .WithName("CompleteMediaUpload")
            .WithSummary("Says the upload finished; the file is checked and its location data stripped.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/api/v1/feed", FeedAsync)
            .WithTags("Social")
            .WithName("GetFeed")
            .WithSummary("Stories from people you follow, your own, and destination stories, newest first.")
            .AllowAnonymous()
            .Produces<PostPage>();

        var posts = app.MapGroup("/api/v1/posts").WithTags("Social").RequireAuthorization();

        posts.MapPost("/", CreatePostAsync)
            .WithName("CreatePost")
            .WithSummary("Publishes a story with your uploaded photos.")
            .RequireRateLimiting(RateLimitPolicies.Content)
            .Produces<PostCreated>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        posts.MapPut("/{id:long}", EditPostAsync)
            .WithName("EditPost")
            .WithSummary("Edits your own story: the text, the destination, and which photos stay.")
            .RequireRateLimiting(RateLimitPolicies.Content)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        posts.MapDelete("/{id:long}", DeletePostAsync)
            .WithName("DeletePost")
            .WithSummary("Removes your own story.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Moderation from the admin portal: moderators and admins see every story and may remove any.
        var moderation = app.MapGroup("/api/v1/admin/posts")
            .WithTags("Admin")
            .RequireAuthorization(Authorization.Policies.Moderator);

        moderation.MapGet("/", ListAllPostsAsync)
            .WithName("ListAllPosts")
            .WithSummary("Every story, or one author's, newest first.")
            .Produces<PostPage>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        moderation.MapPost("/{id:long}/remove", RemovePostAsync)
            .WithName("RemovePost")
            .WithSummary("Removes anyone's story, with a reason that is audited and sent to the author.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        posts.MapPost("/{id:long}/likes", LikeAsync)
            .WithName("LikePost")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        posts.MapDelete("/{id:long}/likes", UnlikeAsync)
            .WithName("UnlikePost")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        posts.MapGet("/{id:long}/comments", CommentsAsync)
            .WithName("ListComments")
            .AllowAnonymous()
            .Produces<IReadOnlyList<CommentView>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        posts.MapPost("/{id:long}/comments", AddCommentAsync)
            .WithName("AddComment")
            .RequireRateLimiting(RateLimitPolicies.Content)
            .Produces<CommentView>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapDelete("/api/v1/comments/{id:long}", DeleteCommentAsync)
            .WithTags("Social")
            .WithName("DeleteComment")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var users = app.MapGroup("/api/v1/users").WithTags("Social");

        users.MapGet("/{id:long}", ProfileAsync)
            .WithName("GetPublicProfile")
            .WithSummary("A person's public page: ratings, hosted trips and reviews. Never contact details.")
            .AllowAnonymous()
            .Produces<PublicProfile>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        users.MapGet("/{id:long}/posts", UserPostsAsync)
            .WithName("ListUserPosts")
            .AllowAnonymous()
            .Produces<PostPage>();

        users.MapPost("/{id:long}/follow", FollowAsync)
            .WithName("FollowUser")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        users.MapDelete("/{id:long}/follow", UnfollowAsync)
            .WithName("UnfollowUser")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var reviews = app.MapGroup("/api/v1/trips/{id:long}").WithTags("Social").RequireAuthorization();

        reviews.MapPost("/reviews", AddReviewAsync)
            .WithName("AddReview")
            .WithSummary("Reviews someone you travelled with, after the trip. One per person per trip.")
            .Produces<ReviewAdded>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        reviews.MapGet("/reviewable", ReviewableAsync)
            .WithName("ListReviewable")
            .WithSummary("Who you can review on this completed trip.")
            .Produces<IReadOnlyList<Reviewable>>();

        return app;
    }

    private static async Task<IResult> UploadUrlAsync(UploadRequest request, ClaimsPrincipal principal, [FromServices] CreateUploadUrlHandler handler, CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), request, cancellationToken));

    private static async Task<IResult> CompleteUploadAsync(long id, ClaimsPrincipal principal, [FromServices] CompleteUploadHandler handler, CancellationToken cancellationToken) =>
        ApiResults.From(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken), _ => Results.Accepted());

    private static async Task<IResult> FeedAsync(ClaimsPrincipal principal, [FromServices] GetFeedHandler handler, CancellationToken cancellationToken, long? before = null) =>
        Results.Ok(await handler.HandleAsync(principal.FindUserId(), authorId: null, before, cancellationToken));

    private static async Task<IResult> CreatePostAsync(CreatePostCommand command, ClaimsPrincipal principal, [FromServices] CreatePostHandler handler, CancellationToken cancellationToken) =>
        ApiResults.From(
            await handler.HandleAsync(principal.RequireUserId(), command, cancellationToken),
            created => Results.Created($"/api/v1/posts/{created.Id}", created));

    private static async Task<IResult> DeletePostAsync(long id, ClaimsPrincipal principal, [FromServices] DeletePostHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> EditPostAsync(long id, EditPostCommand command, ClaimsPrincipal principal, [FromServices] EditPostHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken));

    private static async Task<IResult> ListAllPostsAsync(
        ClaimsPrincipal principal,
        [FromServices] ListAllPostsHandler handler,
        CancellationToken cancellationToken,
        long? authorId = null,
        long? before = null) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), authorId, before, cancellationToken));

    private static async Task<IResult> RemovePostAsync(long id, AdminReason command, ClaimsPrincipal principal, [FromServices] RemovePostHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken));

    private static async Task<IResult> LikeAsync(long id, ClaimsPrincipal principal, [FromServices] LikePostHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, like: true, cancellationToken));

    private static async Task<IResult> UnlikeAsync(long id, ClaimsPrincipal principal, [FromServices] LikePostHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, like: false, cancellationToken));

    private static async Task<IResult> CommentsAsync(long id, [FromServices] ListCommentsHandler handler, CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(id, cancellationToken));

    private static async Task<IResult> AddCommentAsync(long id, AddCommentRequest request, ClaimsPrincipal principal, [FromServices] AddCommentHandler handler, CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(principal.RequireUserId(), id, request.Body, cancellationToken));

    private static async Task<IResult> DeleteCommentAsync(long id, ClaimsPrincipal principal, [FromServices] DeleteCommentHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    private static async Task<IResult> ProfileAsync(long id, ClaimsPrincipal principal, [FromServices] GetPublicProfileHandler handler, CancellationToken cancellationToken) =>
        ApiResults.Ok(await handler.HandleAsync(id, principal.FindUserId(), cancellationToken));

    private static async Task<IResult> UserPostsAsync(long id, ClaimsPrincipal principal, [FromServices] GetFeedHandler handler, CancellationToken cancellationToken, long? before = null) =>
        Results.Ok(await handler.HandleAsync(principal.FindUserId(), id, before, cancellationToken));

    private static async Task<IResult> FollowAsync(long id, ClaimsPrincipal principal, [FromServices] FollowUserHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, follow: true, cancellationToken));

    private static async Task<IResult> UnfollowAsync(long id, ClaimsPrincipal principal, [FromServices] FollowUserHandler handler, CancellationToken cancellationToken) =>
        ApiResults.NoContent(await handler.HandleAsync(principal.RequireUserId(), id, follow: false, cancellationToken));

    private static async Task<IResult> AddReviewAsync(long id, AddReviewCommand command, ClaimsPrincipal principal, [FromServices] AddReviewHandler handler, CancellationToken cancellationToken) =>
        ApiResults.From(
            await handler.HandleAsync(principal.RequireUserId(), id, command, cancellationToken),
            added => Results.Created($"/api/v1/trips/{id}/reviews/{added.Id}", added));

    private static async Task<IResult> ReviewableAsync(long id, ClaimsPrincipal principal, [FromServices] ListReviewableHandler handler, CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(principal.RequireUserId(), id, cancellationToken));

    public sealed record AddCommentRequest(string? Body);
}
