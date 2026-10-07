using System.Globalization;
using System.Security.Claims;
using FluentValidation;
using Ghurify.Application.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// Accounts: register (with an emailed code to confirm the address, once), sign in with email
/// and password, forgot / reset / change password, and the session endpoints. Anonymous by
/// design except the password change: these are the endpoints you reach before you have a
/// token. They carry the strictest rate limit in the API.
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

        // Same addresses, kinder limit: these run on every page load (see RateLimitPolicies.Session).
        var session = app.MapGroup("/api/v1/auth")
            .WithTags("Auth")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Session);

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithSummary("Creates an account and emails a code to confirm the address.")
            .Produces<CodeSentResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/register/confirm", ConfirmEmailAsync)
            .WithName("ConfirmEmail")
            .WithSummary("Confirms the address with the emailed code and signs in.")
            .Produces<SessionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/register/resend", ResendCodeAsync)
            .WithName("ResendConfirmationCode")
            .WithSummary("Sends the confirmation code again.")
            .Produces<CodeSentResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/sign-in", SignInAsync)
            .WithName("SignIn")
            .WithSummary("Signs in with email and password.")
            .Produces<SessionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/password/forgot", ForgotPasswordAsync)
            .WithName("ForgotPassword")
            .WithSummary("Emails a password reset code, if the address has an account.")
            .Produces<CodeSentResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/password/reset", ResetPasswordAsync)
            .WithName("ResetPassword")
            .WithSummary("Sets a new password with the reset code, signs out everywhere else, and signs in.")
            .Produces<SessionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        session.MapPost("/refresh", RefreshAsync)
            .WithName("RefreshSession")
            .WithSummary("Rotates the refresh cookie and returns a new access token; 204 when there is no cookie.")
            .Produces<SessionResponse>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        session.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithSummary("Ends the session and revokes its whole token family.")
            .Produces(StatusCodes.Status204NoContent);

        // Answers "is this token good, and whose is it?" The web app calls it after a silent
        // refresh. The group is anonymous, so the caller is checked by hand below.
        session.MapGet("/whoami", WhoAmIAsync)
            .WithName("WhoAmI")
            .WithSummary("Returns the signed-in user for the presented access token.")
            .Produces<WhoAmIResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Outside the anonymous group: an anonymous group would override RequireAuthorization.
        app.MapPost("/api/v1/auth/password/change", ChangePasswordAsync)
            .WithTags("Auth")
            .WithName("ChangePassword")
            .WithSummary("Changes the password, signs out every other device, and starts a fresh session here.")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Produces<SessionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

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

        return Results.Ok(new WhoAmIResponse(user.Id, user.Email.ToMasked(), user.DisplayName));
    }

    private static async Task<IResult> RegisterAsync(
        RegisterCommand command,
        [FromServices] RegisterHandler handler,
        [FromServices] IValidator<RegisterCommand> validator,
        CancellationToken cancellationToken) =>
        await ValidateAsync(validator, command, cancellationToken)
            ?? CodeSent(await handler.HandleAsync(command, cancellationToken));

    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailCommand command,
        HttpContext context,
        [FromServices] ConfirmEmailHandler handler,
        [FromServices] IValidator<ConfirmEmailCommand> validator,
        CancellationToken cancellationToken) =>
        await ValidateAsync(validator, command, cancellationToken)
            ?? Session(context, await handler.HandleAsync(command, cancellationToken));

    private static async Task<IResult> ResendCodeAsync(
        EmailOnlyCommand command,
        [FromServices] ResendSignUpCodeHandler handler,
        [FromServices] IValidator<EmailOnlyCommand> validator,
        CancellationToken cancellationToken) =>
        await ValidateAsync(validator, command, cancellationToken)
            ?? CodeSent(await handler.HandleAsync(command, cancellationToken));

    private static async Task<IResult> SignInAsync(
        SignInCommand command,
        HttpContext context,
        [FromServices] SignInHandler handler,
        [FromServices] IValidator<SignInCommand> validator,
        CancellationToken cancellationToken) =>
        await ValidateAsync(validator, command, cancellationToken)
            ?? Session(context, await handler.HandleAsync(command, cancellationToken));

    private static async Task<IResult> ForgotPasswordAsync(
        EmailOnlyCommand command,
        [FromServices] ForgotPasswordHandler handler,
        [FromServices] IValidator<EmailOnlyCommand> validator,
        CancellationToken cancellationToken) =>
        await ValidateAsync(validator, command, cancellationToken)
            ?? CodeSent(await handler.HandleAsync(command, cancellationToken));

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordCommand command,
        HttpContext context,
        [FromServices] ResetPasswordHandler handler,
        [FromServices] IValidator<ResetPasswordCommand> validator,
        CancellationToken cancellationToken) =>
        await ValidateAsync(validator, command, cancellationToken)
            ?? Session(context, await handler.HandleAsync(command, cancellationToken));

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordCommand command,
        HttpContext context,
        ClaimsPrincipal principal,
        [FromServices] ChangePasswordHandler handler,
        [FromServices] IValidator<ChangePasswordCommand> validator,
        CancellationToken cancellationToken) =>
        await ValidateAsync(validator, command, cancellationToken)
            ?? Session(context, await handler.HandleAsync(principal.RequireUserId(), command, cancellationToken));

    private static async Task<IResult> RefreshAsync(
        HttpContext context,
        [FromServices] RefreshSessionHandler handler,
        CancellationToken cancellationToken)
    {
        var refreshToken = context.Request.Cookies[RefreshCookieName];

        // No cookie: nobody is signed in on this browser. The web app asks on every first page
        // load, so this is a normal answer, not an error; as a 401 it put a red "failed to load"
        // line in the console of every visitor. A cookie that is presented and refused is still 401.
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Results.NoContent();
        }

        var result = await handler.HandleAsync(refreshToken, cancellationToken);

        if (!result.Succeeded)
        {
            // The old cookie is useless now, and leaving it in place makes the browser retry
            // a token that will never work again.
            ClearRefreshCookie(context);
        }

        return Session(context, result);
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

    private static async Task<IResult?> ValidateAsync<T>(IValidator<T> validator, T command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        return validation.IsValid ? null : Results.ValidationProblem(validation.ToDictionary());
    }

    /// <summary>202: the code was handed to the mail server (or, by design, may have been).</summary>
    private static IResult CodeSent(IdentityResult<CodeSentResult> result) =>
        result.Succeeded
            ? Results.Accepted(value: new CodeSentResponse(result.Value!.ExpiresInSeconds, result.Value.ResendAfterSeconds))
            : ToProblem(result);

    /// <summary>
    /// Returns the access token in the body and puts the refresh token in an httpOnly cookie.
    /// The refresh token is never in the response body: that is the whole point of the cookie.
    /// </summary>
    private static IResult Session(HttpContext context, IdentityResult<SessionResult> result)
    {
        if (!result.Succeeded)
        {
            return ToProblem(result);
        }

        var session = result.Value!;
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
            new SignedInUserResponse(session.User.Id, session.User.MaskedEmail, session.User.DisplayName)));
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
    /// Maps a failure to a status and a stable code the web app translates. Every bad-credential
    /// case has the same status, code and wording, so responses cannot be used to work out which
    /// addresses are registered.
    /// </summary>
    private static IResult ToProblem<T>(IdentityResult<T> result)
    {
        var (status, code, title, detail) = result.Error switch
        {
            IdentityError.InvalidEmail => (400, "invalid_email", "Invalid email address", "Enter a valid email address, for example rizvi@example.com."),
            IdentityError.InvalidName => (400, "invalid_name", "Invalid name", "Your name must be between 2 and 100 characters."),
            IdentityError.RateLimited => (429, "too_many_codes", "Too many requests", "Too many codes have been sent to this address. Try again in a few minutes."),
            // 502, not 500: the API is working, the mail server it depends on is not.
            IdentityError.DeliveryFailed => (502, "email_not_sent", "Could not send the email", "We could not send the email just now. Please try again shortly."),
            IdentityError.AccountNotActive => (403, "account_not_active", "Account unavailable", "This account cannot sign in. Contact support."),
            IdentityError.InvalidCredentials => (401, "invalid_credentials", "Sign-in failed", "The email or password is wrong."),
            IdentityError.SignInPaused => (429, "sign_in_paused", "Too many attempts", "Too many wrong passwords. Wait a few minutes, or reset your password."),
            IdentityError.EmailNotConfirmed => (403, "email_not_confirmed", "Email not confirmed", "Confirm your email address with the code we sent you."),
            IdentityError.PasswordResetRequired => (403, "password_reset_required", "New password needed", "Choose a new password with \"Forgot password\" to continue."),
            IdentityError.PasswordTooShort => (422, "password_too_short", "Password too short", "Use at least 10 characters. A short phrase is easy to remember."),
            IdentityError.PasswordTooCommon => (422, "password_too_common", "Password too easy to guess", "That password is too common or too close to your email. Choose another."),
            IdentityError.CurrentPasswordWrong => (422, "current_password_wrong", "Current password is wrong", "Your current password is not right."),
            IdentityError.InvalidRefreshToken => (401, "session_expired", "Session ended", "Your session has ended. Sign in again."),
            _ => (401, "invalid_code", "Code not accepted", "That code is not valid. Request a new one and try again."),
        };

        var extensions = new Dictionary<string, object?> { ["code"] = code };
        if (result.RetryAfter is { } retryAfter)
        {
            var seconds = Math.Max(1, (int)Math.Ceiling((retryAfter - DateTimeOffset.UtcNow).TotalSeconds));
            extensions["retryAfterSeconds"] = seconds;
            return new RetryAfterResult(Results.Problem(detail, statusCode: status, title: title, extensions: extensions), seconds);
        }

        return Results.Problem(detail, statusCode: status, title: title, extensions: extensions);
    }

    /// <summary>A problem response with a Retry-After header.</summary>
    private sealed class RetryAfterResult(IResult inner, int seconds) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            return inner.ExecuteAsync(httpContext);
        }
    }

    public sealed record CodeSentResponse(int ExpiresInSeconds, int ResendAfterSeconds);

    public sealed record SessionResponse(string AccessToken, int ExpiresInSeconds, SignedInUserResponse User);

    public sealed record SignedInUserResponse(long Id, string MaskedEmail, string? DisplayName);

    public sealed record WhoAmIResponse(long UserId, string MaskedEmail, string? DisplayName);
}
