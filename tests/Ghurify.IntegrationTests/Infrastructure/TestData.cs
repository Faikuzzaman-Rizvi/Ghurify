using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Ghurify.IntegrationTests.Infrastructure;

/// <summary>
/// Creates the people a test needs (travellers, hosts, admins) straight in the database, mints
/// real access tokens for them, and removes every row they own on dispose, so a test never
/// depends on or disturbs what another test created.
/// </summary>
public sealed class TestData(string connectionString) : IAsyncDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly List<long> _users = [];

    public string ConnectionString { get; } = connectionString;

    public async Task<TestUser> CreateUserAsync(
        Gender gender = Gender.Female,
        string? name = null,
        Role[]? roles = null,
        VerificationLevel? verified = null)
    {
        var token = TestContext.Current.CancellationToken;
        var email = $"user-{Guid.NewGuid():N}@ghurify.test";

        await using var connection = await OpenAsync();

        var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO [Main].[User] ([Email], [DisplayName], [Gender], [Status], [Phone])
            OUTPUT inserted.[Id]
            VALUES (@Email, @Name, @Gender, 1, @Phone);
            """,
            new { Email = email, Name = name ?? "Test person", Gender = (byte)gender, Phone = NewPhone() },
            cancellationToken: token));

        _users.Add(id);

        foreach (var role in roles ?? [])
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO [Main].[UserRole] ([UserId], [Role]) VALUES (@UserId, @Role);",
                new { UserId = id, Role = (byte)role },
                cancellationToken: token));
        }

        if (verified is { } level)
        {
            // A random hash: the test only needs an approved check, not a real NID.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [Main].[Verification] ([UserId], [Level], [Status], [NidHash], [Provider], [ProviderRef], [ReviewedOn])
                VALUES (@UserId, @Level, 2, @Hash, 'fake', @Ref, SYSUTCDATETIME());
                """,
                new { UserId = id, Level = (byte)level, Hash = RandomNumberGenerator.GetBytes(32), Ref = Guid.NewGuid().ToString("N") },
                cancellationToken: token));
        }

        return new TestUser(id, email, gender);
    }

    /// <summary>A unique Bangladeshi mobile number in E.164, so phone-gated flows (payment) work.</summary>
    private static string NewPhone() =>
        "+88019" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A host who may publish: Host role and the selfie check passed.</summary>
    public Task<TestUser> CreateVerifiedHostAsync(Gender gender = Gender.Female) =>
        CreateUserAsync(gender, "Test host", [Role.Host], VerificationLevel.NidSelfie);

    /// <summary>A traveller who may request to join trips.</summary>
    public Task<TestUser> CreateVerifiedTravelerAsync(Gender gender = Gender.Female) =>
        CreateUserAsync(gender, "Test traveller", verified: VerificationLevel.Nid);

    public Task<TestUser> CreateAdminAsync() => CreateUserAsync(Gender.Male, "Test admin", [Role.Admin]);

    /// <summary>
    /// A live trip straight in the database: three days at Sajek starting in <paramref name="startIn"/>
    /// days, priced at the sum of two cost lines, with a full itinerary.
    /// </summary>
    public async Task<long> CreatePublishedTripAsync(
        TestUser host,
        short seats = 10,
        byte groupType = 1,
        int startIn = 20,
        string destination = "sajek")
    {
        ArgumentNullException.ThrowIfNull(host);
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenAsync();

        var tripId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO [Main].[Trip] ([HostId], [DestinationId], [Title], [Summary], [StartDate], [EndDate],
                                       [MeetingPoint], [Seats], [PricePerPerson], [GroupType], [Status], [PublishedOn])
            OUTPUT inserted.[Id]
            SELECT @HostId, [Id], N'Integration trip', N'A trip created by an integration test.',
                   DATEADD(DAY, @StartIn, CAST(SYSUTCDATETIME() AS DATE)),
                   DATEADD(DAY, @StartIn + 2, CAST(SYSUTCDATETIME() AS DATE)),
                   N'Arambagh', @Seats, 6000, @GroupType, 2, SYSUTCDATETIME()
            FROM   [Main].[Destination]
            WHERE  [Slug] = @Slug;
            """,
            new { HostId = host.Id, Seats = seats, GroupType = groupType, StartIn = startIn, Slug = destination },
            cancellationToken: token));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Main].[TripCostItem] ([TripId], [Category], [Amount], [SortOrder])
            VALUES (@TripId, 1, 4000, 1), (@TripId, 2, 2000, 2);

            INSERT INTO [Main].[ItineraryDay] ([TripId], [DayNo], [Title], [Details], [Difficulty])
            VALUES (@TripId, 1, N'Day one', N'Arrive.', 1),
                   (@TripId, 2, N'Day two', N'Explore.', 2),
                   (@TripId, 3, N'Day three', N'Home.', 1);
            """,
            new { TripId = tripId },
            cancellationToken: token));

        return tripId;
    }

    /// <summary>Remembers a user created through the API, so dispose removes it too.</summary>
    public void Track(long userId) => _users.Add(userId);

    public async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    /// <summary>A client that sends this user's bearer token.</summary>
    public static HttpClient ClientFor(GhurifyApiFactory api, TestUser user)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(user);

        var client = api.CreateClientWithoutCookieJar();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(api, user));
        return client;
    }

    /// <summary>Mints a real access token with the API's own issuer, as sign-in would.</summary>
    public static string TokenFor(GhurifyApiFactory api, TestUser user)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(user);

        var issuer = api.Services.GetRequiredService<ITokenIssuer>();
        var domainUser = new User(
            user.Id, EmailAddress.FromStorage(user.Email), phone: null, displayName: null, user.Gender,
            UserStatus.Active, DateTimeOffset.UtcNow);

        return issuer.IssueAccessToken(domainUser).Value;
    }

    public async ValueTask DisposeAsync()
    {
        if (_users.Count == 0)
        {
            return;
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        // Children first, then the users. Each statement is scoped to rows these users own or
        // rows hanging off their trips. History rows of temporal tables are left: they cannot be
        // deleted while versioning is on, and only current rows affect other tests.
        using var ids = new System.Data.DataTable();
        ids.Columns.Add("Id", typeof(long));
        foreach (var id in _users.Distinct())
        {
            ids.Rows.Add(id);
        }

        await connection.ExecuteAsync(CleanupSql, new { Ids = ids.AsTableValuedParameter("[Main].[IdList]") });
    }

    /// <summary>
    /// One batch, kept in dependency order. New tables that reference users or trips add their
    /// delete here.
    /// </summary>
    private const string CleanupSql =
        """
        DECLARE @UserIds TABLE ([Id] BIGINT PRIMARY KEY);
        INSERT INTO @UserIds SELECT [Id] FROM @Ids;

        DECLARE @TripIds TABLE ([Id] BIGINT PRIMARY KEY);
        INSERT INTO @TripIds SELECT [Id] FROM [Main].[Trip] WHERE [HostId] IN (SELECT [Id] FROM @UserIds);

        DECLARE @BookingIds TABLE ([Id] BIGINT PRIMARY KEY);
        INSERT INTO @BookingIds SELECT [Id] FROM [Pay].[Booking]
        WHERE [TripId] IN (SELECT [Id] FROM @TripIds) OR [UserId] IN (SELECT [Id] FROM @UserIds);

        DECLARE @PostIds TABLE ([Id] BIGINT PRIMARY KEY);
        INSERT INTO @PostIds SELECT [Id] FROM [Social].[Post]
        WHERE [AuthorId] IN (SELECT [Id] FROM @UserIds) OR [TripId] IN (SELECT [Id] FROM @TripIds);

        DELETE FROM [Social].[Like] WHERE [PostId] IN (SELECT [Id] FROM @PostIds) OR [UserId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Social].[Comment] WHERE [PostId] IN (SELECT [Id] FROM @PostIds) OR [AuthorId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Social].[Media] WHERE [PostId] IN (SELECT [Id] FROM @PostIds) OR [OwnerId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Social].[Post] WHERE [Id] IN (SELECT [Id] FROM @PostIds);
        DELETE FROM [Social].[Follow] WHERE [FollowerId] IN (SELECT [Id] FROM @UserIds) OR [FolloweeId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Social].[Review] WHERE [TripId] IN (SELECT [Id] FROM @TripIds)
            OR [ReviewerId] IN (SELECT [Id] FROM @UserIds) OR [RevieweeId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Social].[RatingSummary] WHERE [UserId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Social].[ChatReadMarker] WHERE [TripId] IN (SELECT [Id] FROM @TripIds) OR [UserId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Social].[ChatMessage] WHERE [TripId] IN (SELECT [Id] FROM @TripIds) OR [SenderId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Safety].[SosEvent] WHERE [TripId] IN (SELECT [Id] FROM @TripIds) OR [UserId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Safety].[CheckIn] WHERE [TripId] IN (SELECT [Id] FROM @TripIds);
        DELETE FROM [Safety].[Report] WHERE [ReporterId] IN (SELECT [Id] FROM @UserIds) OR [ResolvedById] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Safety].[DestinationAlert] WHERE [CreatedById] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Pay].[Payout] WHERE [TripId] IN (SELECT [Id] FROM @TripIds) OR [HostId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Pay].[Refund] WHERE [BookingId] IN (SELECT [Id] FROM @BookingIds);
        DELETE FROM [Pay].[EscrowLedger] WHERE [BookingId] IN (SELECT [Id] FROM @BookingIds);
        DELETE FROM [Pay].[WebhookEvent] WHERE [TransactionRef] IN
            (SELECT [TransactionRef] FROM [Pay].[Payment] WHERE [BookingId] IN (SELECT [Id] FROM @BookingIds));
        DELETE FROM [Pay].[Payment] WHERE [BookingId] IN (SELECT [Id] FROM @BookingIds);
        DELETE FROM [Main].[Notification] WHERE [UserId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Pay].[Booking] WHERE [TripId] IN (SELECT [Id] FROM @TripIds) OR [UserId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Main].[JoinRequest] WHERE [TripId] IN (SELECT [Id] FROM @TripIds) OR [UserId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Main].[TripCostItem] WHERE [TripId] IN (SELECT [Id] FROM @TripIds);
        DELETE FROM [Main].[ItineraryDay] WHERE [TripId] IN (SELECT [Id] FROM @TripIds);
        DELETE FROM [Main].[Trip] WHERE [Id] IN (SELECT [Id] FROM @TripIds);

        DELETE FROM [Safety].[AuditLog] WHERE [ActorId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Main].[VerificationDocument] WHERE [UserId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Main].[Verification] WHERE [UserId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Main].[UserRole] WHERE [UserId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Main].[UserProfile] WHERE [UserId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Main].[RefreshToken] WHERE [UserId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Main].[UserCredential] WHERE [UserId] IN (SELECT [Id] FROM @UserIds);
        DELETE FROM [Main].[OtpCode] WHERE [Email] IN (SELECT [u].[Email] FROM [Main].[User] AS [u] WHERE [u].[Id] IN (SELECT [Id] FROM @UserIds));
        DELETE FROM [Main].[User] WHERE [Id] IN (SELECT [Id] FROM @UserIds);
        """;
}

public sealed record TestUser(long Id, string Email, Gender Gender);
