using System.Data;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Infrastructure.Data;
using Microsoft.Extensions.Options;

namespace Ghurify.Infrastructure.Identity;

/// <summary>
/// Pauses sign-in for an address after repeated wrong passwords, counted in the database so the
/// limit holds across API instances. Rows are keyed on an HMAC of the address with the server
/// secret, so the table never holds an address and cannot be joined back to accounts.
/// </summary>
public sealed class SignInThrottle(IDbConnectionFactory connectionFactory, IOptions<IdentityOptions> options) : ISignInThrottle
{
    private readonly IdentityOptions _options = options.Value;
    private readonly byte[] _key = Encoding.UTF8.GetBytes("sign-in-throttle:" + options.Value.OtpPepper);

    public async Task<DateTimeOffset?> PausedUntilAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var until = await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            """
            SELECT [LockedUntil] FROM [Main].[SignInThrottle]
            WHERE  [EmailHash] = @EmailHash AND [LockedUntil] > SYSUTCDATETIME();
            """,
            new { EmailHash = Hash(email) },
            cancellationToken: cancellationToken));

        return until is null ? null : new DateTimeOffset(until.Value, TimeSpan.Zero);
    }

    public async Task<DateTimeOffset?> RegisterFailureAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleAsync<StateRow>(new CommandDefinition(
            Procedures.Main.SetSignInFailure,
            new
            {
                EmailHash = Hash(email),
                MaxFailures = _options.SignInMaxFailures,
                WindowMinutes = _options.SignInWindowMinutes,
                LockMinutes = _options.SignInPauseMinutes,
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return row.LockedUntil is null ? null : new DateTimeOffset(row.LockedUntil.Value, TimeSpan.Zero);
    }

    public async Task ClearAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM [Main].[SignInThrottle] WHERE [EmailHash] = @EmailHash;",
            new { EmailHash = Hash(email) },
            cancellationToken: cancellationToken));
    }

    private byte[] Hash(EmailAddress email) => HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(email.Value));

    private sealed record StateRow(byte Failures, DateTime? LockedUntil);
}
