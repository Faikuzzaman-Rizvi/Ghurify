using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Identity;

/// <summary>A user's own profile. Every call is keyed on the caller's own user id.</summary>
public sealed class ProfileRepository(IDbConnectionFactory connectionFactory) : IProfileRepository
{
    public async Task<ProfileDetails?> GetAsync(long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Main.GetUserProfile,
            new { UserId = userId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var row = await results.ReadSingleOrDefaultAsync<ProfileRow>();
        var roles = (await results.ReadAsync<byte>()).Select(role => (Role)role).ToList();
        var permissions = await results.ReadAsync<string>();
        var staffRoles = await results.ReadAsync<ProfileStaffRole>();

        if (row is null)
        {
            return null;
        }

        return new ProfileDetails(
            row.UserId,
            EmailAddress.FromStorage(row.Email).ToMasked(),
            row.DisplayName,
            row.Gender is null ? null : (Gender)row.Gender.Value,
            row.Phone,
            row.Bio,
            row.HomeDistrict,
            row.EmergencyContactName,
            row.EmergencyContactPhone,
            [Role.Traveler, .. roles],
            row.VerifiedLevel is null ? null : (VerificationLevel)row.VerifiedLevel.Value,
            DateOnly.FromDateTime(row.Created),
            row.AvatarUpdatedOn is null ? null : new DateTimeOffset(DateTime.SpecifyKind(row.AvatarUpdatedOn.Value, DateTimeKind.Utc)).ToUnixTimeSeconds(),
            // Keys this build does not define are dropped, so an older API serves an older UI
            // exactly the sections it knows how to render.
            [.. PermissionCatalog.Known(permissions).Order(StringComparer.Ordinal)],
            row.IsSuperAdmin,
            [.. staffRoles]);
    }

    public async Task<string?> SetAvatarAsync(long userId, string? avatarBlob, long actorId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            Procedures.Main.SetUserAvatar,
            new { UserId = userId, AvatarBlob = avatarBlob, ActorId = actorId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task<string?> GetAvatarBlobAsync(long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // Only for accounts in use: a suspended or closed account's picture is not served.
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            SELECT [p].[AvatarBlob]
            FROM   [Main].[UserProfile] AS [p]
            JOIN   [Main].[User]        AS [u] ON [u].[Id] = [p].[UserId]
            WHERE  [p].[UserId] = @UserId
              AND  [p].[Archived] = 0
              AND  [u].[Status] = 1
              AND  [u].[Archived] = 0;
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> SaveAsync(long userId, ProfileUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@UserId", userId, DbType.Int64);
        parameters.Add("@DisplayName", update.DisplayName, DbType.String, size: 100);
        parameters.Add("@Gender", (byte?)update.Gender, DbType.Byte);
        parameters.Add("@Phone", update.Phone?.Value, DbType.String, size: 20);
        parameters.Add("@Bio", update.Bio, DbType.String, size: 500);
        parameters.Add("@HomeDistrict", update.HomeDistrict, DbType.String, size: 60);
        parameters.Add("@EmergencyContactName", update.EmergencyContactName, DbType.String, size: 100);
        parameters.Add("@EmergencyContactPhone", update.EmergencyContactPhone?.Value, DbType.String, size: 20);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.SetUserProfile,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return parameters.Get<byte>("@Result") == 0;
    }

    private sealed record ProfileRow(
        long UserId,
        string Email,
        string? DisplayName,
        byte? Gender,
        string? Phone,
        string? Bio,
        string? HomeDistrict,
        string? EmergencyContactName,
        string? EmergencyContactPhone,
        DateTime Created,
        byte? VerifiedLevel,
        DateTime? AvatarUpdatedOn,
        bool IsSuperAdmin);
}
