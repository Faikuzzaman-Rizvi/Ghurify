namespace Ghurify.Application.Identity;

/// <summary>
/// Short-lived proof that the person at the keyboard just typed their own password.
///
/// The access token says which account a request belongs to. It does not say that the human
/// holding it is the account's owner: it lives in the browser for fifteen minutes and could have
/// been taken from there. For the handful of actions that could be used to take over the
/// platform — editing roles, putting somebody on the admin desk, moving money by hand — the
/// password is asked for again, and this is the receipt.
///
/// Issued as a separate signed token with its own audience, so it can never be presented as an
/// access token and an access token can never be presented as step-up proof. It carries no
/// permission of its own: it only answers "this is really them", and the ordinary permission
/// check still decides what they may do.
/// </summary>
public interface IStepUpTokens
{
    /// <summary>A receipt for <paramref name="userId"/>, valid for the configured few minutes.</summary>
    StepUpToken Issue(long userId);

    /// <summary>
    /// Whether <paramref name="token"/> is a receipt this server issued for
    /// <paramref name="userId"/> and has not expired. False for anything else, including a
    /// valid receipt belonging to somebody else.
    /// </summary>
    bool IsValidFor(string? token, long userId);
}

/// <summary>A step-up receipt and when it stops working.</summary>
public sealed record StepUpToken(string Value, DateTimeOffset ExpiresOn, int ExpiresInSeconds);
