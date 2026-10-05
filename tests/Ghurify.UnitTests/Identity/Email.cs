using Ghurify.Domain.Identity;

namespace Ghurify.UnitTests.Identity;

/// <summary>
/// Parses identifiers for tests that are not themselves about parsing, failing loudly if the
/// literal in the test is wrong. Keeps TryParse's result checked at every call site.
/// </summary>
internal static class Email
{
    public static EmailAddress Parse(string input)
    {
        Assert.True(EmailAddress.TryParse(input, out var email), $"'{input}' should be a valid address.");
        return email!.Value;
    }
}

internal static class Phone
{
    public static PhoneNumber Parse(string input)
    {
        Assert.True(PhoneNumber.TryParse(input, out var phone), $"'{input}' should be a valid number.");
        return phone!.Value;
    }
}
