using Ghurify.Application.Identity;
using Ghurify.Domain.Identity;

namespace Ghurify.UnitTests.Identity;

/// <summary>
/// Small hand-made fakes, as the testing rules require. They are deliberately simple: each
/// records what it was asked to do so a test can assert on behaviour, not on call counts.
/// </summary>
internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = now;

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

internal sealed class FakeOtpSender : IOtpSender
{
    public List<(EmailAddress Email, string Code)> Sent { get; } = [];

    public Task SendOtpAsync(EmailAddress email, string code, CancellationToken cancellationToken)
    {
        Sent.Add((email, code));
        return Task.CompletedTask;
    }
}

/// <summary>Hashes a code as "email:code" bytes. Not secure; just deterministic for tests.</summary>
internal sealed class FakeOtpCodeService(string fixedCode = "123456") : IOtpCodeService
{
    public string GenerateCode() => fixedCode;

    public byte[] Hash(EmailAddress email, string code) =>
        System.Text.Encoding.UTF8.GetBytes($"{email.Value}:{code}");

    public bool Matches(EmailAddress email, string code, byte[] expectedHash) =>
        Hash(email, code).AsSpan().SequenceEqual(expectedHash);
}

internal sealed class FakeOtpCodeRepository : IOtpCodeRepository
{
    private long _nextId = 1;

    public List<OtpCode> Codes { get; } = [];

    /// <summary>Set to force the rate-limit path without inserting rows first.</summary>
    public bool AlwaysRateLimited { get; set; }

    public Task<OtpSendOutcome> AddAsync(
        EmailAddress email,
        byte[] codeHash,
        DateTimeOffset expiresOn,
        DateTimeOffset windowStart,
        byte maxPerWindow,
        CancellationToken cancellationToken)
    {
        if (AlwaysRateLimited)
        {
            return Task.FromResult(OtpSendOutcome.RateLimited);
        }

        Codes.Add(new OtpCode(
            _nextId++, email, codeHash, expiresOn, attempts: 0, consumedOn: null, lockedOn: null));

        return Task.FromResult(OtpSendOutcome.Sent);
    }

    public Task<OtpCode?> FindLatestAsync(EmailAddress email, CancellationToken cancellationToken) =>
        Task.FromResult(Codes
            .Where(code => code.Email == email)
            .OrderByDescending(code => code.Id)
            .FirstOrDefault());

    public Task<OtpAttemptOutcome> RegisterFailedAttemptAsync(
        long otpCodeId,
        byte maxAttempts,
        CancellationToken cancellationToken)
    {
        var index = Codes.FindIndex(code => code.Id == otpCodeId);

        if (index < 0)
        {
            return Task.FromResult(new OtpAttemptOutcome(maxAttempts, IsLocked: true));
        }

        var existing = Codes[index];
        var attempts = (byte)(existing.Attempts + 1);
        var locked = attempts >= maxAttempts;

        Codes[index] = new OtpCode(
            existing.Id,
            existing.Email,
            existing.CodeHash,
            existing.ExpiresOn,
            attempts,
            existing.ConsumedOn,
            locked ? DateTimeOffset.UtcNow : existing.LockedOn);

        return Task.FromResult(new OtpAttemptOutcome(attempts, locked));
    }

    public Task ConsumeAsync(long otpCodeId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var index = Codes.FindIndex(code => code.Id == otpCodeId);

        if (index >= 0)
        {
            var existing = Codes[index];
            Codes[index] = new OtpCode(
                existing.Id,
                existing.Email,
                existing.CodeHash,
                existing.ExpiresOn,
                existing.Attempts,
                now,
                existing.LockedOn);
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeUserRepository : IUserRepository
{
    private long _nextId = 1;

    public List<User> Users { get; } = [];

    /// <summary>Status given to accounts created on first sign-in.</summary>
    public UserStatus StatusForNewUsers { get; set; } = UserStatus.Active;

    public Task<User> GetOrAddByEmailAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        var existing = Users.Find(user => user.Email == email);

        if (existing is not null)
        {
            return Task.FromResult(existing);
        }

        var created = new User(
            _nextId++, email, phone: null, displayName: null, gender: null, StatusForNewUsers, DateTimeOffset.UtcNow);

        Users.Add(created);
        return Task.FromResult(created);
    }

    public Task<User?> FindByIdAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Find(user => user.Id == userId));
}

internal sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    private long _nextId = 1;

    public List<RefreshToken> Tokens { get; } = [];

    public Task AddAsync(
        long userId,
        byte[] tokenHash,
        Guid familyId,
        DateTimeOffset expiresOn,
        CancellationToken cancellationToken)
    {
        Tokens.Add(new RefreshToken(_nextId++, userId, tokenHash, familyId, expiresOn, revokedOn: null));
        return Task.CompletedTask;
    }

    public Task<RefreshRotationOutcome> RotateAsync(
        byte[] oldTokenHash,
        byte[] newTokenHash,
        DateTimeOffset expiresOn,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var index = Tokens.FindIndex(token => token.TokenHash.AsSpan().SequenceEqual(oldTokenHash));

        if (index < 0)
        {
            return Task.FromResult(RefreshRotationOutcome.NotFound);
        }

        var existing = Tokens[index];

        if (existing.IsRevoked)
        {
            return Task.FromResult(RefreshRotationOutcome.Replayed);
        }

        if (existing.IsExpired(now))
        {
            return Task.FromResult(RefreshRotationOutcome.Expired);
        }

        Tokens[index] = new RefreshToken(
            existing.Id, existing.UserId, existing.TokenHash, existing.FamilyId, existing.ExpiresOn, now);

        Tokens.Add(new RefreshToken(
            _nextId++, existing.UserId, newTokenHash, existing.FamilyId, expiresOn, revokedOn: null));

        return Task.FromResult(RefreshRotationOutcome.Rotated);
    }

    public Task<RefreshToken?> FindByHashAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.Find(token => token.TokenHash.AsSpan().SequenceEqual(tokenHash)));

    public Task<int> RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var revoked = 0;

        for (var index = 0; index < Tokens.Count; index++)
        {
            var token = Tokens[index];

            if (token.FamilyId == familyId && !token.IsRevoked)
            {
                Tokens[index] = new RefreshToken(
                    token.Id, token.UserId, token.TokenHash, token.FamilyId, token.ExpiresOn, now);
                revoked++;
            }
        }

        return Task.FromResult(revoked);
    }
}

/// <summary>Issues predictable tokens so tests can assert on exactly which one was handed out.</summary>
internal sealed class FakeTokenIssuer : ITokenIssuer
{
    private int _counter;

    public AccessToken IssueAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new AccessToken($"access-{user.Id}", DateTimeOffset.UtcNow.AddMinutes(15), 900);
    }

    public (string Token, byte[] Hash) IssueRefreshToken()
    {
        var token = $"refresh-{++_counter}";
        return (token, HashRefreshToken(token));
    }

    public byte[] HashRefreshToken(string token) =>
        System.Text.Encoding.UTF8.GetBytes($"hash:{token}");
}

/// <summary>An <see cref="IOtpSender"/> that always fails, standing in for a dead mail server.</summary>
internal sealed class FailingOtpSender : IOtpSender
{
    public Task SendOtpAsync(EmailAddress email, string code, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The SMTP server rejected the credentials.");
}
