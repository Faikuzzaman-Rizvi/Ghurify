using DbUp;
using DbUp.Engine;
using Microsoft.Data.SqlClient;

namespace Ghurify.DatabaseUpdate;

/// <summary>
/// Runs the data-only migration scripts. Separated from <see cref="Program"/> so the
/// integration tests drive exactly the same code the deploy pipeline runs, rather than a
/// copy of it that could drift.
/// </summary>
public static class Migrator
{
    private const string PreFolderMarker = ".Scripts.Pre.";
    private const string DemoFolderMarker = ".Scripts.Demo.";
    private const string ScriptsPrefix = "Ghurify.DatabaseUpdate.Scripts.";

    /// <summary>Which set of scripts a run applies.</summary>
    public enum Stage
    {
        /// <summary>Scripts/Pre, run before the dacpac against the old schema.</summary>
        Pre,

        /// <summary>Scripts/{Year}, run after the dacpac against the new schema.</summary>
        Data,

        /// <summary>
        /// Scripts/Demo: sample hosts and trips for showcases and local development. Only ever
        /// run on request (the "demo" argument), never as part of a release.
        /// </summary>
        Demo,
    }

    /// <summary>
    /// Applies every not-yet-applied script for the stage, each in its own transaction.
    /// </summary>
    public static DatabaseUpgradeResult Run(string connectionString, Stage stage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Each stage keeps its own journal table so pre-scripts and data scripts are
        // tracked independently and never re-run each other.
        var journalTable = stage switch
        {
            Stage.Pre => "SchemaVersionsPre",
            Stage.Demo => "SchemaVersionsDemo",
            _ => "SchemaVersions",
        };

        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(Migrator).Assembly,
                scriptName => ShouldRun(scriptName, stage))
            // One transaction per script: a failure rolls back that script only, and the
            // journal records exactly the scripts that committed.
            .WithTransactionPerScript()
            .JournalToSqlTable("dbo", journalTable)
            .LogToConsole()
            .Build();

        return upgrader.PerformUpgrade();
    }

    /// <summary>
    /// Selects Pre, yearly data, or demo scripts, never more than one set in a run. Demo data
    /// in particular must never leak into a release's data run.
    /// </summary>
    internal static bool ShouldRun(string scriptName, Stage stage)
    {
        if (!scriptName.StartsWith(ScriptsPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var isPreScript = scriptName.Contains(PreFolderMarker, StringComparison.Ordinal);
        var isDemoScript = scriptName.Contains(DemoFolderMarker, StringComparison.Ordinal);

        return stage switch
        {
            Stage.Pre => isPreScript,
            Stage.Demo => isDemoScript,
            _ => !isPreScript && !isDemoScript,
        };
    }

    /// <summary>
    /// Whether the target database exists yet.
    ///
    /// On a brand-new environment the dacpac at step 2 is what creates it, so at step 1 there is
    /// no database and, by definition, no old data for the pre-scripts to touch. Connecting to
    /// master to ask is the only way to tell that apart from a genuine connection failure.
    /// </summary>
    public static bool DatabaseExists(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var target = new SqlConnectionStringBuilder(connectionString);
        var databaseName = target.InitialCatalog;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                "The connection string does not name a database (Initial Catalog).");
        }

        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };

        using var connection = new SqlConnection(master.ConnectionString);
        connection.Open();

        using var command = new SqlCommand(
            "SELECT COUNT(1) FROM sys.databases WHERE name = @databaseName;",
            connection);
        command.Parameters.AddWithValue("@databaseName", databaseName);

        return (int)(command.ExecuteScalar() ?? 0) == 1;
    }

    /// <summary>Names of the embedded scripts for a stage, in the order DbUp applies them.</summary>
    public static IReadOnlyList<string> ScriptNames(Stage stage) =>
        [.. typeof(Migrator).Assembly
            .GetManifestResourceNames()
            .Where(name => ShouldRun(name, stage))
            .OrderBy(name => name, StringComparer.Ordinal)];
}
