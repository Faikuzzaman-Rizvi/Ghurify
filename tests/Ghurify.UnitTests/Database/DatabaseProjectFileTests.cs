using System.Xml.Linq;

namespace Ghurify.UnitTests.Database;

/// <summary>
/// The SSDT project lists every .sql file explicitly (default globbing is off), because a file
/// that is on disk but not in the project is silently left out of the dacpac: the build stays
/// green and the object is missing in production. These tests close that gap.
/// </summary>
public sealed class DatabaseProjectFileTests
{
    private static readonly string[] IncludeElementNames = ["Build", "PreDeploy", "PostDeploy", "None"];

    [Fact]
    public void EverySqlFileOnDisk_IsListedInTheProject()
    {
        var (projectDirectory, listed) = LoadProject();

        var onDisk = EnumerateSqlFiles(projectDirectory);
        var missing = onDisk.Except(listed, StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "These .sql files exist on disk but are not listed in Ghurify.Database.sqlproj, "
            + "so they would not reach the dacpac:"
            + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void EveryFileListedInTheProject_ExistsOnDisk()
    {
        var (projectDirectory, listed) = LoadProject();

        var deleted = listed
            .Where(relativePath => !File.Exists(Path.Combine(projectDirectory, relativePath)))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            deleted.Length == 0,
            "These files are listed in Ghurify.Database.sqlproj but do not exist on disk:"
            + Environment.NewLine + string.Join(Environment.NewLine, deleted));
    }

    [Fact]
    public void NoFileIsListedTwice()
    {
        var (_, listed) = LoadProjectAllowingDuplicates();

        var duplicates = listed
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} (listed {group.Count()} times)")
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            duplicates.Length == 0,
            "These files are listed more than once in Ghurify.Database.sqlproj:"
            + Environment.NewLine + string.Join(Environment.NewLine, duplicates));
    }

    [Fact]
    public void DefaultSqlItemGlobbing_IsDisabled()
    {
        var projectFile = Path.Combine(FindDatabaseProjectDirectory(), "Ghurify.Database.sqlproj");
        var document = XDocument.Load(projectFile);

        var value = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "EnableDefaultSqlItems")?.Value;

        Assert.True(
            string.Equals(value, "False", StringComparison.OrdinalIgnoreCase),
            "EnableDefaultSqlItems must stay False. With globbing on, these tests cannot tell "
            + "whether a file was listed deliberately, and an unlisted file would ship silently.");
    }

    private static (string ProjectDirectory, HashSet<string> Listed) LoadProject()
    {
        var (directory, listed) = LoadProjectAllowingDuplicates();
        return (directory, listed.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private static (string ProjectDirectory, List<string> Listed) LoadProjectAllowingDuplicates()
    {
        var projectDirectory = FindDatabaseProjectDirectory();
        var projectFile = Path.Combine(projectDirectory, "Ghurify.Database.sqlproj");
        var document = XDocument.Load(projectFile);

        var listed = document.Descendants()
            .Where(element => IncludeElementNames.Contains(element.Name.LocalName, StringComparer.Ordinal))
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Normalize(include!))
            .Where(include => include.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return (projectDirectory, listed);
    }

    private static List<string> EnumerateSqlFiles(string projectDirectory)
    {
        return Directory
            .EnumerateFiles(projectDirectory, "*.sql", SearchOption.AllDirectories)
            // bin and obj hold generated copies, not source.
            .Where(path => !IsUnderGeneratedFolder(projectDirectory, path))
            .Select(path => Normalize(Path.GetRelativePath(projectDirectory, path)))
            .ToList();
    }

    private static bool IsUnderGeneratedFolder(string projectDirectory, string path)
    {
        var relative = Path.GetRelativePath(projectDirectory, path);
        var firstSegment = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];

        return string.Equals(firstSegment, "bin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstSegment, "obj", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The project file uses Windows separators; the tests run on Linux in CI.
    /// Comparing normalized paths keeps the result the same on both.
    /// </summary>
    private static string Normalize(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .Trim();

    private static string FindDatabaseProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Database", "Ghurify.Database");
            if (File.Exists(Path.Combine(candidate, "Ghurify.Database.sqlproj")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate src/Database/Ghurify.Database by walking up from "
            + AppContext.BaseDirectory);
    }
}
