using Ghurify.DatabaseUpdate;
using Microsoft.SqlServer.Dac;
using Testcontainers.MsSql;

namespace Ghurify.IntegrationTests.Infrastructure;

/// <summary>
/// Brings up a real SQL Server in Docker and builds the Ghurify database in it the same way
/// a release does: dacpac first, then the DbUp data scripts.
///
/// Shared by every integration test through <see cref="DatabaseCollection"/>, because starting
/// the container and publishing the dacpac costs far more than the tests themselves.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string DatabaseName = "Ghurify";

    // Pinned to SQL Server 2022, the version production runs.
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    /// <summary>Connection string pointing at the deployed Ghurify database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // The container starts with master only; the dacpac publish creates Ghurify itself.
        var masterConnectionString = _container.GetConnectionString();
        ConnectionString = ToDatabaseConnectionString(masterConnectionString, DatabaseName);

        PublishDacpac(masterConnectionString);
        RunDbUpDataScripts();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Deploys the dacpac with DacFx, using the same options the release pipeline uses.
    /// </summary>
    private static void PublishDacpac(string masterConnectionString)
    {
        var dacpacPath = FindDacpac();

        using var package = DacPackage.Load(dacpacPath);
        var services = new DacServices(masterConnectionString);

        var options = new DacDeployOptions
        {
            // Matches the release pipeline: a deploy must never silently discard rows.
            BlockOnPossibleDataLoss = true,

            // Hangfire and DbUp create their own tables outside SSDT. Dropping objects that
            // are not in the dacpac would delete them.
            DropObjectsNotInSource = false,

            CreateNewDatabase = true,
            IncludeTransactionalScripts = false,
        };

        services.Deploy(
            package,
            DatabaseName,
            upgradeExisting: true,
            options: options);
    }

    /// <summary>
    /// Runs the data scripts through the real migrator, so a broken script fails the build.
    /// Scripts/Pre is not run here: it belongs before the dacpac, and on a database that is
    /// being created from nothing there is no old data for it to touch.
    /// </summary>
    private void RunDbUpDataScripts()
    {
        var result = Migrator.Run(ConnectionString, Migrator.Stage.Data);

        if (!result.Successful)
        {
            throw new InvalidOperationException(
                "DbUp data scripts failed while preparing the test database.", result.Error);
        }
    }

    private static string ToDatabaseConnectionString(string connectionString, string databaseName)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = databaseName,
        };

        return builder.ConnectionString;
    }

    /// <summary>
    /// Finds the dacpac the solution just built. The integration test project references the
    /// SSDT project, so it is always present by the time these tests run.
    /// </summary>
    private static string FindDacpac()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var databaseProject = Path.Combine(directory.FullName, "src", "Database", "Ghurify.Database");

            if (Directory.Exists(databaseProject))
            {
                var binFolder = Path.Combine(databaseProject, "bin");

                var dacpac = Directory.Exists(binFolder)
                    ? Directory.EnumerateFiles(binFolder, "Ghurify.Database.dacpac", SearchOption.AllDirectories)
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault()
                    : null;

                if (dacpac is not null)
                {
                    return dacpac;
                }

                throw new InvalidOperationException(
                    $"No Ghurify.Database.dacpac under {binFolder}. Build the solution first.");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate src/Database/Ghurify.Database by walking up from " + AppContext.BaseDirectory);
    }
}
