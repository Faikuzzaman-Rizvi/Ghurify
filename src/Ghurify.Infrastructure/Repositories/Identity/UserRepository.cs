using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Identity;

/// <summary>Accounts.</summary>
public sealed class UserRepository(IDbConnectionFactory connectionFactory) : IUserRepository
{
    public async Task<User> GetOrAddByEmailAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // Procedure: the existence check and the insert have to be one atomic step.
        var row = await connection.QuerySingleAsync<UserRow>(new CommandDefinition(
            Procedures.Main.GetOrAddUserByEmail,
            new { Email = email.Value },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return ToDomain(row);
    }

    public async Task<User?> FindByIdAsync(long userId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(
            """
            SELECT [Id], [Email], [Phone], [DisplayName], [Gender], [Status], [Created]
            FROM   [Main].[User]
            WHERE  [Id] = @Id
              AND  [Archived] = 0;
            """,
            new { Id = userId },
            cancellationToken: cancellationToken));

        return row is null ? null : ToDomain(row);
    }

    private static User ToDomain(UserRow row) => new(
        row.Id,
        EmailAddress.FromStorage(row.Email),
        // Optional: collected later on the profile, for SOS and payouts.
        row.Phone is null ? null : PhoneNumber.FromStorage(row.Phone),
        row.DisplayName,
        row.Gender is null ? null : (Gender)row.Gender.Value,
        (UserStatus)row.Status,
        new DateTimeOffset(row.Created, TimeSpan.Zero));

    private sealed record UserRow(
        long Id,
        string Email,
        string? Phone,
        string? DisplayName,
        byte? Gender,
        byte Status,
        DateTime Created);
}
