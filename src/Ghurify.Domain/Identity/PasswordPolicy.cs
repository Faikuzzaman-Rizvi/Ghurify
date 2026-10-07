namespace Ghurify.Domain.Identity;

/// <summary>
/// What makes a password acceptable, following NIST SP 800-63B: long enough, not one of the
/// passwords attackers try first, and no composition rules ("one symbol, one capital"), which
/// make passwords harder to remember without making them harder to guess.
///
/// Any characters are allowed, Bangla included, and spaces too, so a passphrase works.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;

    /// <summary>Generous, but bounded: hashing a megabyte of "password" is a cheap DoS.</summary>
    public const int MaxLength = 128;

    /// <summary>
    /// Why a password is refused, or <see cref="PasswordProblem.None"/>. The email is used to
    /// refuse a password that is just the address or its name part.
    /// </summary>
    public static PasswordProblem Check(string? password, EmailAddress? email = null)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return PasswordProblem.TooShort;
        }

        if (password.Length > MaxLength)
        {
            return PasswordProblem.TooLong;
        }

        var lowered = password.Trim().ToLowerInvariant();

        if (Common.Contains(lowered) || UsesTooFewCharacters(lowered) || IsSimpleRun(lowered))
        {
            return PasswordProblem.TooCommon;
        }

        if (email is { } address)
        {
            var local = address.Value.Split('@')[0];
            if (lowered == address.Value || (local.Length >= 4 && lowered.Contains(local, StringComparison.Ordinal)))
            {
                return PasswordProblem.TooCommon;
            }
        }

        return PasswordProblem.None;
    }

    /// <summary>"aaaaaaaaaa", "ababababab": long, but trivially guessed.</summary>
    private static bool UsesTooFewCharacters(string value) => value.Distinct().Count() <= 2;

    /// <summary>"1234567890", "abcdefghij", "0987654321": runs of consecutive characters.</summary>
    private static bool IsSimpleRun(string value)
    {
        var up = true;
        var down = true;
        for (var index = 1; index < value.Length; index++)
        {
            up &= value[index] == value[index - 1] + 1;
            down &= value[index] == value[index - 1] - 1;
        }

        return up || down;
    }

    /// <summary>
    /// Ten characters or longer and near the top of every leaked-password list, plus the obvious
    /// local ones. Not exhaustive; it stops the guesses an attacker makes first.
    /// </summary>
    private static readonly HashSet<string> Common = new(StringComparer.Ordinal)
    {
        "1234567890", "12345678910", "123456789a", "1q2w3e4r5t", "1qaz2wsx3edc", "abcd123456",
        "password12", "password123", "password1234", "passw0rd123", "p@ssw0rd123", "qwertyuiop",
        "qwerty1234", "qwerty12345", "qwerty123456", "1qazxsw23edc", "asdfghjkl1", "zxcvbnm123",
        "iloveyou12", "iloveyou123", "princess12", "football12", "baseball12", "welcome123",
        "welcome1234", "letmein123", "admin12345", "administrator", "trustno1234", "sunshine12",
        "superman12", "michael123", "monkey1234", "dragon1234", "master1234", "computer12",
        "bangladesh", "bangladesh1", "bangladesh12", "bangladesh123", "bangladesh1971", "dhaka12345",
        "dhaka123456", "ilovebangladesh", "chittagong", "sylhet12345", "ghurify123", "ghurify1234",
        "ghurifyapp", "ghurify2026", "travel1234", "travel12345", "allahuakbar", "bismillah1",
        "bismillah123", "alhamdulillah", "changeme123", "default1234", "secret1234", "mypassword",
        "mypassword1", "newpassword", "newpassword1", "password!!", "abc123abc123", "a1b2c3d4e5",
    };
}

/// <summary>Why a password was refused. Shown to the user, since it is their own password.</summary>
public enum PasswordProblem
{
    None = 0,
    TooShort = 1,
    TooLong = 2,
    TooCommon = 3,
}
