namespace Ghurify.Infrastructure.Configuration;

/// <summary>
/// Reads local settings from a <c>.env</c> file at the repository root, for development.
///
/// Format: one <c>KEY=VALUE</c> per line; <c>#</c> starts a comment; values may be wrapped in
/// single or double quotes. Nested settings use a double underscore, the same convention .NET
/// uses for environment variables: <c>Email__Password=...</c> is <c>Email:Password</c>.
///
/// The file is gitignored. <c>.env.example</c> (committed) lists every key with no values.
///
/// This source file is also compiled into Ghurify.DatabaseUpdate (linked in its .csproj), so
/// the API and the migration console read the same file the same way.
/// </summary>
public static class DotEnvFile
{
    public const string FileName = ".env";

    /// <summary>
    /// Looks for <c>.env</c> in <paramref name="startDirectory"/> and each parent, so it is found
    /// whether the app runs from the repository root, its project folder, or bin/Debug.
    /// </summary>
    public static string? Find(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, FileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>
    /// The settings in the nearest <c>.env</c> above any of <paramref name="startDirectories"/>,
    /// as configuration keys (<c>Email:Password</c>). Empty when there is no file.
    /// Blank values are skipped, so an unfilled line in a copied template changes nothing.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> Load(params string[] startDirectories)
    {
        ArgumentNullException.ThrowIfNull(startDirectories);

        var path = startDirectories.Select(Find).FirstOrDefault(found => found is not null);

        return path is null
            ? new Dictionary<string, string?>()
            : Parse(File.ReadAllLines(path));
    }

    public static Dictionary<string, string?> Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.Ordinal))
            {
                line = line["export ".Length..].TrimStart();
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim().Replace("__", ":", StringComparison.Ordinal);
            var value = Unquote(line[(separator + 1)..].Trim());

            if (value.Length > 0)
            {
                settings[key] = value;
            }
        }

        return settings;
    }

    /// <summary>
    /// Quoted values are taken literally (so a password may contain <c>#</c> or <c>=</c>);
    /// unquoted values end at a <c> #</c> comment.
    /// </summary>
    private static string Unquote(string value)
    {
        if (value.Length >= 2
            && (value[0] == '"' || value[0] == '\'')
            && value[^1] == value[0])
        {
            return value[1..^1];
        }

        var comment = value.IndexOf(" #", StringComparison.Ordinal);
        return comment >= 0 ? value[..comment].TrimEnd() : value;
    }
}
