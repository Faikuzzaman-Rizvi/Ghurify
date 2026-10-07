using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Identity;

/// <summary>Roles, status and verification level, for authorization.</summary>
public sealed class UserAccessRepository(IDbConnectionFactory connectionFactory) : IUserAccessRepository
{
    public async Task<UserAccess?> GetAsync(long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Procedures.Main.GetUserAccess,
            new { UserId = userId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var row = await results.ReadSingleOrDefaultAsync<AccessRow>();
        var roles = (await results.ReadAsync<byte>()).Select(role => (Role)role).ToHashSet();

        if (row is null)
        {
            return null;
        }

        return new UserAccess(
            row.UserId,
            (UserStatus)row.Status,
            row.Gender is null ? null : (Gender)row.Gender.Value,
            roles,
            row.VerifiedLevel is null ? null : (VerificationLevel)row.VerifiedLevel.Value);
    }

    public async Task GrantRoleAsync(long userId, Role role, long? grantedById, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // Single table. The lock hint makes "insert unless held" one step, so a double click
        // cannot hit the unique index.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Main].[UserRole] ([UserId], [Role], [GrantedById], [UpdatedId])
            SELECT @UserId, @Role, @GrantedById, @GrantedById
            WHERE  NOT EXISTS (SELECT 1
                               FROM   [Main].[UserRole] WITH (UPDLOCK, HOLDLOCK)
                               WHERE  [UserId] = @UserId
                                 AND  [Role] = @Role
                                 AND  [Archived] = 0);
            """,
            new { UserId = userId, Role = (byte)role, GrantedById = grantedById },
            cancellationToken: cancellationToken));
    }

    public async Task RevokeRoleAsync(long userId, Role role, long revokedById, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Main].[UserRole]
            SET    [Archived]  = 1,
                   [UpdatedOn] = SYSUTCDATETIME(),
                   [UpdatedId] = @RevokedById
            WHERE  [UserId] = @UserId
              AND  [Role] = @Role
              AND  [Archived] = 0;
            """,
            new { UserId = userId, Role = (byte)role, RevokedById = revokedById },
            cancellationToken: cancellationToken));
    }

    private sealed record AccessRow(long UserId, byte Status, byte? Gender, byte? VerifiedLevel);
}
