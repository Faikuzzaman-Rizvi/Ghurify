namespace Ghurify.IntegrationTests.Infrastructure;

/// <summary>
/// One SQL Server container for the whole integration suite.
/// Tests in this collection run one after another, so they may read and write the same database;
/// each test still cleans up the rows it creates.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SharedDatabase : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "Database";
}
