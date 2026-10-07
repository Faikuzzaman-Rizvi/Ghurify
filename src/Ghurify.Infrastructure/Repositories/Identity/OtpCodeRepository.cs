using System.Data;
using Dapper;
using Ghurify.Application.Abstractions;
using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;
using Ghurify.Infrastructure.Data;

namespace Ghurify.Infrastructure.Repositories.Identity;

/// <summary>
/// One-time codes. The rate limit and the attempt counter live in procedures, because both
/// are read-modify-write steps that must not be racy.
/// </summary>
public sealed class OtpCodeRepository(IDbConnectionFactory connectionFactory) : IOtpCodeRepository
{
    public async Task<OtpSendOutcome> AddAsync(
        EmailAddress email,
        OtpPurpose purpose,
        byte[] codeHash,
        DateTimeOffset expiresOn,
        DateTimeOffset windowStart,
        byte maxPerWindow,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("@Email", email.Value, DbType.String, size: 256);
        parameters.Add("@CodeHash", codeHash, DbType.Binary, size: 32);
        parameters.Add("@Purpose", (byte)purpose, DbType.Byte);
        parameters.Add("@ExpiresOn", expiresOn.UtcDateTime, DbType.DateTime2);
        parameters.Add("@WindowStart", windowStart.UtcDateTime, DbType.DateTime2);
        parameters.Add("@MaxPerWindow", maxPerWindow, DbType.Byte);
        parameters.Add("@Id", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@Result", dbType: DbType.Byte, direction: ParameterDirection.Output);

        await connection.ExecuteAsync(new CommandDefinition(
            Procedures.Main.AddOtpCode,
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        return parameters.Get<byte>("@Result") == 0
            ? OtpSendOutcome.Sent
            : OtpSendOutcome.RateLimited;
    }

    public async Task<OtpCode?> FindLatestAsync(EmailAddress email, OtpPurpose purpose, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // Single-table read, so parameterised Dapper rather than a procedure.
        var row = await connection.QuerySingleOrDefaultAsync<OtpCodeRow>(new CommandDefinition(
            """
            SELECT   TOP (1)
                     [Id], [Email], [CodeHash], [ExpiresOn], [Attempts], [ConsumedOn], [LockedOn]
            FROM     [Main].[OtpCode]
            WHERE    [Email] = @Email
              AND    [Purpose] = @Purpose
              AND    [Archived] = 0
            ORDER BY [Id] DESC;
            """,
            new { Email = email.Value, Purpose = (byte)purpose },
            cancellationToken: cancellationToken));

        return row is null ? null : ToDomain(row);
    }

    public async Task<OtpAttemptOutcome> RegisterFailedAttemptAsync(
        long otpCodeId,
        byte maxAttempts,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<AttemptRow>(new CommandDefinition(
            Procedures.Main.SetOtpCodeAttempted,
            new { Id = otpCodeId, MaxAttempts = maxAttempts },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        // No row means the code vanished underneath us; treat it as locked so nothing proceeds.
        return row is null
            ? new OtpAttemptOutcome(maxAttempts, IsLocked: true)
            : new OtpAttemptOutcome(row.Attempts, row.IsLocked);
    }

    public async Task ConsumeAsync(long otpCodeId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Main].[OtpCode]
            SET    [ConsumedOn] = @Now,
                   [UpdatedOn]  = SYSUTCDATETIME()
            WHERE  [Id] = @Id
              AND  [ConsumedOn] IS NULL;
            """,
            new { Id = otpCodeId, Now = now.UtcDateTime },
            cancellationToken: cancellationToken));
    }

    private static OtpCode ToDomain(OtpCodeRow row) => new(
        row.Id,
        EmailAddress.FromStorage(row.Email),
        row.CodeHash,
        new DateTimeOffset(row.ExpiresOn, TimeSpan.Zero),
        row.Attempts,
        ToUtc(row.ConsumedOn),
        ToUtc(row.LockedOn));

    /// <summary>
    /// Every DATETIME2 in this database is UTC by convention, but the type carries no offset,
    /// so it is attached explicitly on the way out.
    /// </summary>
    private static DateTimeOffset? ToUtc(DateTime? value) =>
        value is null ? null : new DateTimeOffset(value.Value, TimeSpan.Zero);

    private sealed record OtpCodeRow(
        long Id,
        string Email,
        byte[] CodeHash,
        DateTime ExpiresOn,
        byte Attempts,
        DateTime? ConsumedOn,
        DateTime? LockedOn);

    private sealed record AttemptRow(byte Attempts, bool IsLocked);
}
