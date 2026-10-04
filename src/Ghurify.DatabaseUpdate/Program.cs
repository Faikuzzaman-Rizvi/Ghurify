using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Ghurify.DatabaseUpdate;

/// <summary>
/// Entry point for the data-only migration console.
///
/// Two modes, matching the fixed deploy order
/// (DbUp pre -> dacpac publish -> DbUp data -> apps):
///
///   dotnet run -- pre   runs Scripts/Pre/*  against the OLD schema, before the dacpac.
///                       Only for intentional data loss that BlockOnPossibleDataLoss blocks.
///   dotnet run          runs Scripts/{Year}/* against the NEW schema, after the dacpac.
///
/// This console never creates, alters or drops schema objects. That is the dacpac's job.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var stage = args.Length > 0 && string.Equals(args[0], "pre", StringComparison.OrdinalIgnoreCase)
            ? Migrator.Stage.Pre
            : Migrator.Stage.Data;

        var stageName = stage == Migrator.Stage.Pre ? "pre-schema" : "data";
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
