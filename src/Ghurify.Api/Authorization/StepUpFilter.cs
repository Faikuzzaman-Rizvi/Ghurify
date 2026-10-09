using Ghurify.Api.Endpoints;
using Ghurify.Application.Identity;

namespace Ghurify.Api.Authorization;

/// <summary>
/// Refuses a request unless the caller re-entered their password in the last few minutes.
///
/// Attached to the handful of routes that could be used to take over the platform: editing admin
/// roles, putting somebody on the desk, moving money by hand. The permission check already asks
/// "may this account do this?"; this asks "is this really them?", because an access token sitting
/// in a browser for fifteen minutes is not proof of that.
///
/// The portal reads the <c>step_up_required</c> code, asks for the password, and retries with the
/// receipt in <see cref="HeaderName"/>.
/// </summary>
public sealed class StepUpFilter(IStepUpTokens tokens) : IEndpointFilter
{
    /// <summary>Where the portal sends the receipt. Not the Authorization header: that is the session.</summary>
    public const string HeaderName = "X-Ghurify-Step-Up";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var userId = context.HttpContext.User.FindUserId();

        if (userId is null)
        {
            // Unreachable behind authorization; a closed answer rather than a crash if it ever is.
            return Results.Problem(
                title: "Not allowed",
                detail: "You are not allowed to do this.",
                statusCode: StatusCodes.Status403Forbidden,
                extensions: new Dictionary<string, object?> { ["code"] = "forbidden" });
        }

        var receipt = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (!tokens.IsValidFor(receipt, userId.Value))
        {
            return Results.Problem(
                title: "Confirm it is you",
                detail: "Enter your password again to confirm this change.",
                statusCode: StatusCodes.Status403Forbidden,
                extensions: new Dictionary<string, object?> { ["code"] = "step_up_required" });
        }

        return await next(context);
    }
}

/// <summary>Reads nicely at the call site: <c>.RequireStepUp()</c> beside the permission.</summary>
public static class StepUpFilterExtensions
{
    public static TBuilder RequireStepUp<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddEndpointFilter<TBuilder, StepUpFilter>();
        return builder;
    }
}
