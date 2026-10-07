using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Identity;

/// <summary>Accounts and their password hashes.</summary>
public sealed class CredentialRepository(IDbConnectionFactory connectionFactory) : ICredentialRepository
{
    public async Task<(RegistrationOutcome Outcome, long UserId)> AddPendingUserAsync(
        EmailAddress email,
        string displayName,
        PasswordHash password,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@Email", email.Value, DbType.String, size: 256);
        parameters.Add("@DisplayName", displayName, DbType.String, size: 100);
        parameters.Add("@PasswordHash", password.Hash, DbType.Binary, size: 64);
        parameters.Add("@PasswordSalt", password.Salt, DbType.Binary, size: 32);
        parameters.Add("@Iterations", password.Iterations, DbType.Int32);
        parameters.Add("@UserId", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.AddPendingUser, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));

        return ((RegistrationOutcome)parameters.Get<byte>("@Result"), parameters.Get<long>("@UserId"));
    }

    public async Task<UserCredential?> FindByEmailAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<CredentialRow>(new CommandDefinition(
            Procedures.Main.GetUserCredential,
            new { Email = email.Value },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return row is null ? null : ToDomain(row);
    }

    public async Task<UserCredential?> FindByIdAsync(long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // Two tables joined on the key; parameterised Dapper is enough.
        var row = await connection.QuerySingleOrDefaultAsync<CredentialRow>(new CommandDefinition(
            """
            SELECT [u].[Id], [u].[Email], [u].[Phone], [u].[DisplayName], [u].[Gender], [u].[Status], [u].[Created],
                   [c].[PasswordHash], [c].[PasswordSalt], [c].[Iterations], [c].[MustReset]
            FROM   [Main].[User] AS [u]
            LEFT JOIN [Main].[UserCredential] AS [c] ON [c].[UserId] = [u].[Id] AND [c].[Archived] = 0
            WHERE  [u].[Id] = @UserId
              AND  [u].[Archived] = 0;
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        return row is null ? null : ToDomain(row);
    }

    public async Task SetPasswordAsync(long userId, PasswordHash password, bool revokeSessions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.SetUserPassword,
            new
            {
                UserId = userId,
                PasswordHash = password.Hash,
                PasswordSalt = password.Salt,
                password.Iterations,
                RevokeSessions = revokeSessions,
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
    }

    public async Task ConfirmEmailAsync(long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // Only a pending account moves; a suspended one stays suspended.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Main].[User]
            SET    [Status] = 1, [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @UserId
            WHERE  [Id] = @UserId AND [Status] = 4;
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
    }

    private static UserCredential ToDomain(CredentialRow row)
    {
        var user = UserRepository.ToDomain(new UserRepository.UserRow(
            row.Id, row.Email, row.Phone, row.DisplayName, row.Gender, row.Status, row.Created));

        var password = row.PasswordHash is null || row.PasswordSalt is null || row.Iterations is null
            ? null
            : new PasswordHash(row.PasswordHash, row.PasswordSalt, row.Iterations.Value);

        return new UserCredential(user, password, row.MustReset ?? false);
    }

    private sealed record CredentialRow(
        long Id,
        string Email,
        string? Phone,
        string? DisplayName,
        byte? Gender,
        byte Status,
        DateTime Created,
        byte[]? PasswordHash,
        byte[]? PasswordSalt,
        int? Iterations,
        bool? MustReset);
}
