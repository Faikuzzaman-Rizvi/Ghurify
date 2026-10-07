using FluentValidation;
using Ghurify.Application.Abstractions;

namespace Ghurify.Api.Endpoints;

/// <summary>
/// Turns use-case results into HTTP responses, so every endpoint reports failures the same way:
/// ProblemDetails with the right status and a stable <c>code</c> the web app translates.
/// </summary>
internal static class ApiResults
{
    public static IResult From<T>(Result<T> result, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.Succeeded ? onSuccess(result.Value!) : Problem(result.Error!);
    }

    public static IResult Ok<T>(Result<T> result) => From(result, value => Results.Ok(value));

    public static IResult NoContent<T>(Result<T> result) => From(result, _ => Results.NoContent());

    public static IResult Problem(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var (status, title) = error.Kind switch
        {
            ErrorKind.Validation => (StatusCodes.Status400BadRequest, "Invalid request"),
            ErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "Not allowed"),
            ErrorKind.NotFound => (StatusCodes.Status404NotFound, "Not found"),
            ErrorKind.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            ErrorKind.BadGateway => (StatusCodes.Status502BadGateway, "Service unavailable"),
            _ => (StatusCodes.Status422UnprocessableEntity, "Not possible"),
        };

        return Results.Problem(
            title: title,
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }

    /// <summary>Runs FluentValidation; null when valid, otherwise the 400 to return.</summary>
    public static async Task<IResult?> ValidateAsync<T>(
        IValidator<T> validator,
        T value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validator);

        var validation = await validator.ValidateAsync(value, cancellationToken);
        return validation.IsValid ? null : Results.ValidationProblem(validation.ToDictionary());
    }

    /// <summary>
    /// The signed-in user's id. Endpoints behind authorization always have one; this throws only
    /// if an endpoint was mistakenly opened to anonymous callers.
    /// </summary>
    public static long RequireUserId(this System.Security.Claims.ClaimsPrincipal principal) =>
        principal.FindUserId() ?? throw new InvalidOperationException("This endpoint requires a signed-in user.");
}
