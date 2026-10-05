using System.Data;
using Dapper;
using Ghurify.DatabaseUpdate;
using Ghurify.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;

namespace Ghurify.IntegrationTests;

/// <summary>
/// Confirms the dacpac really produced the schema the rules describe, and that DbUp ran.
/// These catch the case where a .sql file builds but deploys to something unintended.
/// </summary>
[Collection(SharedDatabase.Name)]
[Trait("Category", "Integration")]
public sealed class DatabaseDeploymentTests(SqlServerFixture database)
{
    /// <summary>SQL Server error numbers for a unique index and a unique constraint violation.</summary>
    private static readonly int[] UniqueViolationErrorNumbers = [2601, 2627];

    [Theory]
    [InlineData("Main")]
    [InlineData("Pay")]
    [InlineData("Social")]
    [InlineData("Safety")]
    public async Task Dacpac_CreatesEverySchema(string schemaName)
    {
        await using var connection = await OpenAsync();

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM sys.schemas WHERE name = @schemaName;",
            new { schemaName },
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(1, exists);
    }

    [Fact]
    public async Task MainUser_IsSystemVersioned_WithItsHistoryTable()
    {
        await using var connection = await OpenAsync();

        var historyTable = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            """
            SELECT   SCHEMA_NAME(history.schema_id) + '.' + history.name
            FROM     sys.tables AS versioned
            JOIN     sys.tables AS history ON versioned.history_table_id = history.object_id
            WHERE    versioned.name = 'User'
              AND    SCHEMA_NAME(versioned.schema_id) = 'Main'
              AND    versioned.temporal_type = 2;
            """,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Main.UserHistory", historyTable);
    }

    [Fact]
    public async Task MainUser_HasTheStandardColumns()
    {
        await using var connection = await OpenAsync();

        var columns = await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT name
            FROM   sys.columns
            WHERE  object_id = OBJECT_ID('Main.User');
            """,
            cancellationToken: TestContext.Current.CancellationToken));

        var actual = columns.ToHashSet(StringComparer.Ordinal);

        // Every table carries these; repositories and audit tooling rely on them.
        Assert.Contains("Id", actual);
        Assert.Contains("Email", actual);
        Assert.Contains("Archived", actual);
        Assert.Contains("Created", actual);
        Assert.Contains("UpdatedOn", actual);
        Assert.Contains("UpdatedId", actual);
    }

    [Fact]
    public async Task MainUser_RejectsASecondAccountOnTheSameEmail()
    {
        await using var connection = await OpenAsync();
        const string Email = "duplicate@ghurify.test";

        try
        {
            await InsertUserAsync(connection, Email, "First account");

            var duplicate = await Assert.ThrowsAsync<SqlException>(
                () => InsertUserAsync(connection, Email, "Second account"));

            // 2601/2627 are the unique index / unique constraint violations.
            Assert.Contains(duplicate.Number, UniqueViolationErrorNumbers);
        }
        finally
        {
            // Temporal tables reject DELETE on the history table, so clearing the current
            // row is all this test can and should undo.
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM [Main].[User] WHERE [Email] = @Email;",
                new { Email },
                cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@ghurify.test")]
    [InlineData("MixedCase@Ghurify.test")]   // must be stored lower-cased
    public async Task MainUser_RejectsAnEmailTheApplicationWouldNeverProduce(string email)
    {
        await using var connection = await OpenAsync();

        // The CHECK constraint is the last line of defence: even a bad migration script or a
        // hand-written INSERT cannot put an unusable address in the identity column.
        var violation = await Assert.ThrowsAsync<SqlException>(
            () => InsertUserAsync(connection, email, "Should not be stored"));

        // 547 is a CHECK constraint violation.
        Assert.Equal(547, violation.Number);
    }

    [Fact]
    public async Task MainUser_AcceptsAnAccountWithNoPhoneNumber()
    {
        await using var connection = await OpenAsync();
        const string Email = "nophone@ghurify.test";

        try
        {
            // Phone is collected later on the profile, so sign-up must work without one.
            await InsertUserAsync(connection, Email, "No phone yet");

            var phone = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT [Phone] FROM [Main].[User] WHERE [Email] = @Email;",
                new { Email },
                cancellationToken: TestContext.Current.CancellationToken));

            Assert.Null(phone);
        }
        finally
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM [Main].[User] WHERE [Email] = @Email;",
                new { Email },
                cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task MainIdList_TableValuedParameter_AcceptsAListOfIds()
    {
        await using var connection = await OpenAsync();

        using var ids = new DataTable();
        ids.Columns.Add("Id", typeof(long));
        ids.Rows.Add(1L);
        ids.Rows.Add(2L);
        ids.Rows.Add(3L);

        var parameters = new DynamicParameters();
        parameters.Add("@Ids", ids.AsTableValuedParameter("Main.IdList"));

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM @Ids;",
            parameters,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(3, total);
    }

    [Fact]
    public async Task DbUp_RecordsTheDataScriptsItApplied()
    {
        await using var connection = await OpenAsync();

        var applied = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT ScriptName FROM dbo.SchemaVersions;",
            cancellationToken: TestContext.Current.CancellationToken));

        var expected = Migrator.ScriptNames(Migrator.Stage.Data);

        Assert.NotEmpty(expected);
        Assert.Equal(expected.Order(StringComparer.Ordinal), applied.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Migrator_KeepsPreScriptsOutOfADataRun()
    {
        var dataScripts = Migrator.ScriptNames(Migrator.Stage.Data);
        var preScripts = Migrator.ScriptNames(Migrator.Stage.Pre);

        Assert.NotEmpty(preScripts);
        Assert.All(preScripts, name => Assert.Contains(".Scripts.Pre.", name, StringComparison.Ordinal));
        Assert.All(dataScripts, name => Assert.DoesNotContain(".Scripts.Pre.", name, StringComparison.Ordinal));
        Assert.Empty(dataScripts.Intersect(preScripts, StringComparer.Ordinal));
    }

    [Fact]
    public void Migrator_KeepsDemoScriptsOutOfEveryReleaseRun()
    {
        var demoScripts = Migrator.ScriptNames(Migrator.Stage.Demo);
        var releaseScripts = Migrator.ScriptNames(Migrator.Stage.Data)
            .Concat(Migrator.ScriptNames(Migrator.Stage.Pre));

        // Demo hosts and trips are made up; they must never reach production through a release.
        Assert.NotEmpty(demoScripts);
        Assert.All(demoScripts, name => Assert.Contains(".Scripts.Demo.", name, StringComparison.Ordinal));
        Assert.All(releaseScripts, name => Assert.DoesNotContain(".Scripts.Demo.", name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task MainTrip_IsSystemVersioned_WithItsHistoryTable()
    {
        await using var connection = await OpenAsync();

        var historyTable = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            """
            SELECT   SCHEMA_NAME(history.schema_id) + '.' + history.name
            FROM     sys.tables AS versioned
            JOIN     sys.tables AS history ON versioned.history_table_id = history.object_id
            WHERE    versioned.name = 'Trip'
              AND    SCHEMA_NAME(versioned.schema_id) = 'Main'
              AND    versioned.temporal_type = 2;
            """,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("Main.TripHistory", historyTable);
    }

    [Fact]
    public async Task DemoStage_LoadsTripsWhoseCostsAddUpAndRunsOnlyOnce()
    {
        var first = Migrator.Run(database.ConnectionString, Migrator.Stage.Demo);
        Assert.True(first.Successful, first.Error?.Message);

        // A second run must change nothing: the journal records the script as applied.
        var second = Migrator.Run(database.ConnectionString, Migrator.Stage.Demo);
        Assert.True(second.Successful, second.Error?.Message);
        Assert.Empty(second.Scripts);

        await using var connection = await OpenAsync();

        var unbalanced = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1)
            FROM   [Main].[Trip] AS t
            JOIN   [Main].[User] AS u ON u.[Id] = t.[HostId]
            WHERE  u.[Email] LIKE '%@demo.ghurify.app'
              AND  t.[PricePerPerson] <> (SELECT ISNULL(SUM(c.[Amount]), 0)
                                          FROM   [Main].[TripCostItem] AS c
                                          WHERE  c.[TripId] = t.[Id]);
            """,
            cancellationToken: TestContext.Current.CancellationToken));

        var demoTrips = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1)
            FROM   [Main].[Trip] AS t
            JOIN   [Main].[User] AS u ON u.[Id] = t.[HostId]
            WHERE  u.[Email] LIKE '%@demo.ghurify.app';
            """,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(12, demoTrips);
        Assert.Equal(0, unbalanced);
    }

    private static Task<int> InsertUserAsync(SqlConnection connection, string email, string displayName) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Main].[User] ([Email], [DisplayName], [Status])
            VALUES (@email, @displayName, 1);
            """,
            new { email, displayName },
            cancellationToken: TestContext.Current.CancellationToken));

    private async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }
}
