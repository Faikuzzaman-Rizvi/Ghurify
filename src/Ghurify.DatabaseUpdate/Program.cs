using System.Globalization;
using Ghurify.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace Ghurify.DatabaseUpdate;

/// <summary>
/// Entry point for the data-only migration console.
///
/// Two release modes, matching the fixed deploy order
/// (DbUp pre -> dacpac publish -> DbUp data -> apps):
///
///   dotnet run -- pre   runs Scripts/Pre/*  against the OLD schema, before the dacpac.
///                       Only for intentional data loss that BlockOnPossibleDataLoss blocks.
///   dotnet run          runs Scripts/{Year}/* against the NEW schema, after the dacpac.
///
/// And one that is never part of a release:
///
///   dotnet run -- demo  runs Scripts/Demo/* : sample hosts and trips for showcases and
///                       local development. Run it after the data scripts.
///
/// This console never creates, alters or drops schema objects. That is the dacpac's job.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The API's user-secrets store. Sharing it means one
    /// <c>dotnet user-secrets set "Database:ConnectionString" ...</c> points both the API and
    /// this console at the same database, and the credential never lands in the repository.
    /// </summary>
    private const string UserSecretsId = "170067e9-9eba-4275-bfc6-b0290584fd37";

    private static int Main(string[] args)
    {
        var argument = args.Length > 0 ? args[0] : string.Empty;

        var stage = argument.ToUpperInvariant() switch
        {
            "PRE" => Migrator.Stage.Pre,
            "DEMO" => Migrator.Stage.Demo,
            _ => Migrator.Stage.Data,
        };

        var stageName = stage switch
        {
            Migrator.Stage.Pre => "pre-schema",
            Migrator.Stage.Demo => "demo",
            _ => "data",
        };
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Ghurify DbUp: running {stageName} scripts."));

        string connectionString;
        try
        {
            connectionString = ReadConnectionString();
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        // Step 1 of a first-ever deploy runs before the dacpac has created the database.
        // There is no old data to move, so this is success, not failure.
        if (stage == Migrator.Stage.Pre && !Migrator.DatabaseExists(connectionString))
        {
            Console.WriteLine(
                "Ghurify DbUp: the database does not exist yet, so there is no data to move. "
                + "The dacpac publish will create it. Nothing to do.");
            return 0;
        }

        var result = Migrator.Run(connectionString, stage);

        if (!result.Successful)
        {
            Console.Error.WriteLine(result.Error);
            return 1;
        }

        var count = result.Scripts.Count();
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Ghurify DbUp: success. {count} {stageName} script(s) applied."));
        return 0;
    }

    /// <summary>
    /// Reads the connection string from configuration or the environment and fails fast
    /// when it is missing: running migrations against the wrong database by accident
    /// is far worse than not running at all.
    /// </summary>
    private static string ReadConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets(UserSecretsId)
            // The gitignored .env at the repository root, shared with the API.
            .AddInMemoryCollection(DotEnvFile.Load(Directory.GetCurrentDirectory(), AppContext.BaseDirectory))
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration["GHURIFY_DB"]
            ?? configuration["Database:ConnectionString"]
            ?? configuration.GetConnectionString("Ghurify");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No connection string. Set the GHURIFY_DB environment variable, or "
                + "Database:ConnectionString / ConnectionStrings:Ghurify in configuration.");
        }

        return connectionString;
    }
}
