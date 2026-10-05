using System.Security.Claims;
using FluentValidation;
using Ghurify.Application.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// Email-OTP sign-in. Anonymous by design: these are the endpoints you reach before you have
/// a token. They carry the strictest rate limit in the API.
/// </summary>
public static class IdentityEndpoints
{
    /// <summary>
    /// Name of the cookie holding the refresh token. It is httpOnly, so page scripts cannot
    /// read it: an XSS bug can misuse the session while the page is open, but cannot steal a
    /// credential that keeps working for weeks afterwards.
    /// </summary>
    private const string RefreshCookieName = "ghurify_rt";

    /// <summary>
    /// The refresh cookie is only ever sent to the endpoints that consume it, so it does not
    /// ride along on every ordinary API call.
    /// </summary>
    private const string RefreshCookiePath = "/api/v1/auth";

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth);

        group.MapPost("/otp", RequestOtpAsync)
            .WithName("RequestOtp")
            .WithSummary("Emails a one-time sign-in code.")
            .Produces<RequestOtpResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/verify", VerifyOtpAsync)
            .WithName("VerifyOtp")
            .WithSummary("Exchanges a one-time code for a session.")
            .Produces<SessionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshAsync)
            .WithName("RefreshSession")
            .WithSummary("Rotates the refresh cookie and returns a new access token.")
            .Produces<SessionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithSummary("Ends the session and revokes its whole token family.")
            .Produces(StatusCodes.Status204NoContent);

        // The one authenticated endpoint here: it answers "is this token good, and whose is it?"
        // The web app calls it after a silent refresh to restore the signed-in user.
        group.MapGet("/whoami", WhoAmIAsync)
            .WithName("WhoAmI")
            .WithSummary("Returns the signed-in user for the presented access token.")
            .RequireAuthorization()
            .Produces<WhoAmIResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> WhoAmIAsync(
        ClaimsPrincipal principal,
        [FromServices] IUserRepository users,
        CancellationToken cancellationToken)
    {
        if (principal.FindUserId() is not { } userId)
        {
            return Results.Unauthorized();
        }

        var user = await users.FindByIdAsync(userId, cancellationToken);

        // The token is validly signed but names an account that is gone or blocked.
        if (user is null || !user.CanSignIn)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new WhoAmIResponse(
            user.Id, user.Email.ToMasked(), user.DisplayName));
    }

    private static async Task<IResult> RequestOtpAsync(
        RequestOtpRequest request,
        [FromServices] RequestOtpHandler handler,
        [FromServices] IValidator<RequestOtpCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new RequestOtpCommand(request.Email);

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var result = await handler.HandleAsync(command, cancellationToken);

        if (!result.Succeeded)
        {
            return ToProblem(result.Error);
        }

        var value = result.Value!;

        // 202: the code has been handed to the mail server, not yet delivered.
        return Results.Accepted(
            value: new RequestOtpResponse(value.ExpiresInSeconds, value.ResendAfterSeconds));
    }

    private static async Task<IResult> VerifyOtpAsync(
        VerifyOtpRequest request,
        HttpContext context,
        [FromServices] VerifyOtpHandler handler,
        [FromServices] IValidator<VerifyOtpCommand> validator,
        CancellationToken cancellationToken)
    {
        var command = new VerifyOtpCommand(request.Email, request.Code);

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        var result = await handler.HandleAsync(command, cancellationToken);

        return result.Succeeded
            ? SignIn(context, result.Value!)
            : ToProblem(result.Error);
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext context,
        [FromServices] RefreshSessionHandler handler,
        CancellationToken cancellationToken)
    {
        var refreshToken = context.Request.Cookies[RefreshCookieName];

        var result = await handler.HandleAsync(refreshToken, cancellationToken);

        if (!result.Succeeded)
        {
            // The old cookie is useless now, and leaving it in place makes the browser retry
            // a token that will never work again.
            ClearRefreshCookie(context);
            return ToProblem(result.Error);
        }

        return SignIn(context, result.Value!);
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        [FromServices] LogoutHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(context.Request.Cookies[RefreshCookieName], cancellationToken);

        ClearRefreshCookie(context);

        return Results.NoContent();
    }

    /// <summary>
    /// Returns the access token in the body and puts the refresh token in an httpOnly cookie.
    /// The refresh token is never in the response body: that is the whole point of the cookie.
    /// </summary>
    private static IResult SignIn(HttpContext context, SessionResult session)
    {
        context.Response.Cookies.Append(
            RefreshCookieName,
            session.RefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                // Secure everywhere except plain-HTTP localhost, which browsers allow.
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = RefreshCookiePath,
                Expires = session.RefreshTokenExpiresOn,
                IsEssential = true,
            });

        return Results.Ok(new SessionResponse(
            session.AccessToken,
            session.ExpiresInSeconds,
            new SignedInUserResponse(
                session.User.Id,
                session.User.MaskedEmail,
                session.User.DisplayName)));
    }

    private static void ClearRefreshCookie(HttpContext context) =>
        context.Response.Cookies.Delete(
            RefreshCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = RefreshCookiePath,
            });

    /// <summary>
    /// Maps a failure to a status code. Every bad-credential case becomes the same 401 with
    /// the same wording, so responses cannot be used to work out which addresses are registered.
    /// </summary>
    private static IResult ToProblem(IdentityError error) => error switch
    {
        IdentityError.InvalidEmail => Results.Problem(
            title: "Invalid email address",
            detail: "Enter a valid email address, for example rizvi@example.com.",
            statusCode: StatusCodes.Status400BadRequest),

        IdentityError.RateLimited => Results.Problem(
            title: "Too many requests",
            detail: "Too many codes have been requested for this address. Try again shortly.",
            statusCode: StatusCodes.Status429TooManyRequests),

        // 502, not 500: the API is working, the mail server it depends on is not.
        IdentityError.DeliveryFailed => Results.Problem(
            title: "Could not send the code",
            detail: "We could not send your sign-in code just now. Please try again shortly.",
            statusCode: StatusCodes.Status502BadGateway),

        IdentityError.AccountNotActive => Results.Problem(
            title: "Account unavailable",
            detail: "This account cannot sign in. Contact support.",
            statusCode: StatusCodes.Status403Forbidden),

        _ => Results.Problem(
            title: "Sign-in failed",
            detail: "That code is not valid. Request a new one and try again.",
            statusCode: StatusCodes.Status401Unauthorized),
    };

    public sealed record RequestOtpRequest(string Email);

    public sealed record RequestOtpResponse(int ExpiresInSeconds, int ResendAfterSeconds);

    public sealed record VerifyOtpRequest(string Email, string Code);

    public sealed record SessionResponse(
        string AccessToken,
        int ExpiresInSeconds,
        SignedInUserResponse User);

    public sealed record SignedInUserResponse(long Id, string MaskedEmail, string? DisplayName);

    public sealed record WhoAmIResponse(long UserId, string MaskedEmail, string? DisplayName);
}
