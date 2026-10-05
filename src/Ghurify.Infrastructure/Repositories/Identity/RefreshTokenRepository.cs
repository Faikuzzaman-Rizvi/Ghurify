using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Identity;

/// <summary>Rotating refresh tokens.</summary>
public sealed class RefreshTokenRepository(IDbConnectionFactory connectionFactory) : IRefreshTokenRepository
{
    public async Task AddAsync(
        long userId,
        byte[] tokenHash,
        Guid familyId,
        DateTimeOffset expiresOn,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Main].[RefreshToken] ([UserId], [TokenHash], [FamilyId], [ExpiresOn])
            VALUES (@UserId, @TokenHash, @FamilyId, @ExpiresOn);
            """,
            new
            {
                UserId = userId,
                TokenHash = tokenHash,
                FamilyId = familyId,
                ExpiresOn = expiresOn.UtcDateTime,
            },
            cancellationToken: cancellationToken));
    }

    public async Task<RefreshRotationOutcome> RotateAsync(
        byte[] oldTokenHash,
        byte[] newTokenHash,
        DateTimeOffset expiresOn,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@OldTokenHash", oldTokenHash, DbType.Binary, size: 32);
        parameters.Add("@NewTokenHash", newTokenHash, DbType.Binary, size: 32);
        parameters.Add("@ExpiresOn", expiresOn.UtcDateTime, DbType.DateTime2);
        parameters.Add("@Now", now.UtcDateTime, DbType.DateTime2);
        parameters.Add("@UserId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@FamilyId", dbType: DbType.Guid, direction: ParameterDirection.Output);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        // Procedure: revoking the old token and inserting its successor must commit together.
        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.SetRefreshTokenRotated,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return (RefreshRotationOutcome)parameters.Get<byte>("@Result");
    }

    public async Task<RefreshToken?> FindByHashAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<RefreshTokenRow>(new CommandDefinition(
            """
            SELECT [Id], [UserId], [TokenHash], [FamilyId], [ExpiresOn], [RevokedOn]
            FROM   [Main].[RefreshToken]
            WHERE  [TokenHash] = @TokenHash
              AND  [Archived] = 0;
            """,
            new { TokenHash = tokenHash },
            cancellationToken: cancellationToken));

        return row is null
            ? null
            : new RefreshToken(
                row.Id,
                row.UserId,
                row.TokenHash,
                row.FamilyId,
                new DateTimeOffset(row.ExpiresOn, TimeSpan.Zero),
                row.RevokedOn is null ? null : new DateTimeOffset(row.RevokedOn.Value, TimeSpan.Zero));
    }

    public async Task<int> RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@FamilyId", familyId, DbType.Guid);
        parameters.Add("@Now", now.UtcDateTime, DbType.DateTime2);
        parameters.Add("@Revoked", dbType: DbType.Int32, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.SetRefreshTokenFamilyRevoked,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return parameters.Get<int>("@Revoked");
    }

    private sealed record RefreshTokenRow(
        long Id,
        long UserId,
        byte[] TokenHash,
        Guid FamilyId,
        DateTime ExpiresOn,
        DateTime? RevokedOn);
}
