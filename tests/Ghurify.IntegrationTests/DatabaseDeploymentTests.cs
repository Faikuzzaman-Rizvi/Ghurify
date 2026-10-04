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
        Assert.Contains("Archived", actual);
        Assert.Contains("Created", actual);
        Assert.Contains("UpdatedOn", actual);
        Assert.Contains("UpdatedId", actual);
    }

    [Fact]
    public async Task MainUser_RejectsASecondAccountOnTheSamePhoneNumber()
    {
        await using var connection = await OpenAsync();
        const string Phone = "+8801711111111";

        try
        {
            await InsertUserAsync(connection, Phone, "First account");

            var duplicate = await Assert.ThrowsAsync<SqlException>(
                () => InsertUserAsync(connection, Phone, "Second account"));

            // 2601/2627 are the unique index / unique constraint violations.
            Assert.Contains(duplicate.Number, UniqueViolationErrorNumbers);
        }
        finally
        {
            // Temporal tables reject DELETE on the history table, so clearing the current
            // row is all this test can and should undo.
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM [Main].[User] WHERE [Phone] = @Phone;",
                new { Phone },
                cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task MainUser_RejectsAPhoneNumberThatIsNotE164()
    {
        await using var connection = await OpenAsync();

        var violation = await Assert.ThrowsAsync<SqlException>(
            () => InsertUserAsync(connection, "01711111111", "Missing country code"));

        // 547 is a CHECK constraint violation.
        Assert.Equal(547, violation.Number);
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

    private static Task<int> InsertUserAsync(SqlConnection connection, string phone, string displayName) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Main].[User] ([Phone], [DisplayName], [Status])
            VALUES (@phone, @displayName, 1);
            """,
            new { phone, displayName },
            cancellationToken: TestContext.Current.CancellationToken));

    private async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }
}
