namespace Ghurify.Application.Abstractions;

/// <summary>The kind of expected failure, which decides the HTTP status the caller sees.</summary>
public enum ErrorKind
{
    /// <summary>400: the request is malformed.</summary>
    Validation = 1,

    /// <summary>403: the caller is known but may not do this.</summary>
    Forbidden = 2,

    /// <summary>404: there is nothing there, or nothing this caller may see.</summary>
    NotFound = 3,

    /// <summary>409: the request clashes with the current state (a seat already taken, a duplicate).</summary>
    Conflict = 4,

    /// <summary>422: well formed, but a business rule says no.</summary>
    Rule = 5,

    /// <summary>502: a service we depend on (a payment gateway) failed; the request may be retried.</summary>
    BadGateway = 6,
}

/// <summary>
/// An expected failure. <see cref="Code"/> is stable and machine-readable (the web app maps it to
/// translated text); <see cref="Message"/> is plain English for API users and logs.
/// </summary>
public sealed record AppError(ErrorKind Kind, string Code, string Message)
{
    public static AppError Validation(string code, string message) => new(ErrorKind.Validation, code, message);

    public static AppError Forbidden(string message = "You are not allowed to do this.") =>
        new(ErrorKind.Forbidden, "forbidden", message);

    public static AppError NotFound(string code, string message) => new(ErrorKind.NotFound, code, message);

    public static AppError Conflict(string code, string message) => new(ErrorKind.Conflict, code, message);

    public static AppError Rule(string code, string message) => new(ErrorKind.Rule, code, message);
}

/// <summary>
/// Outcome of a use case: a value, or the expected reason it failed. Handlers return this
/// instead of throwing; exceptions are kept for faults. A handler can simply
/// <c>return value;</c> or <c>return AppError.NotFound(...);</c>.
/// </summary>
public readonly record struct Result<T>
{
    internal Result(T? value, AppError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public AppError? Error { get; }

    public bool Succeeded => Error is null;

    public static implicit operator Result<T>(T value) => new(value, null);

    public static implicit operator Result<T>(AppError error) => new(default, error);
}

/// <summary>
/// Builds results where the implicit conversion cannot apply: C# allows no conversion from an
/// interface type, so a handler returning <c>IReadOnlyList&lt;T&gt;</c> uses <see cref="Ok"/>.
/// </summary>
public static class Result
{
    public static Result<T> Ok<T>(T value) => new(value, null);
}

/// <summary>A result with nothing to return on success.</summary>
public readonly record struct Done
{
    public static Done Value => default;
}
