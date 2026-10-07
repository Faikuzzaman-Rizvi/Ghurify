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
    public List<(EmailAddress Email, string Code, OtpPurpose Purpose)> Sent { get; } = [];

    public List<(EmailAddress Email, AccountNotice Notice)> Notices { get; } = [];

    public Task SendOtpAsync(EmailAddress email, string code, OtpPurpose purpose, CancellationToken cancellationToken)
    {
        Sent.Add((email, code, purpose));
        return Task.CompletedTask;
    }

    public Task SendNoticeAsync(EmailAddress email, AccountNotice notice, CancellationToken cancellationToken)
    {
        Notices.Add((email, notice));
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

    /// <summary>The purpose each code (by id) was issued for.</summary>
    public Dictionary<long, OtpPurpose> Purposes { get; } = [];

    /// <summary>Set to force the rate-limit path without inserting rows first.</summary>
    public bool AlwaysRateLimited { get; set; }

    public Task<OtpSendOutcome> AddAsync(
        EmailAddress email,
        OtpPurpose purpose,
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

        Purposes[_nextId] = purpose;
        Codes.Add(new OtpCode(
            _nextId++, email, codeHash, expiresOn, attempts: 0, consumedOn: null, lockedOn: null));

        return Task.FromResult(OtpSendOutcome.Sent);
    }

    public Task<OtpCode?> FindLatestAsync(EmailAddress email, OtpPurpose purpose, CancellationToken cancellationToken) =>
        Task.FromResult(Codes
            .Where(code => code.Email == email && Purposes[code.Id] == purpose)
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
    public List<User> Users { get; } = [];

    public User Add(EmailAddress email, UserStatus status = UserStatus.Active)
    {
        var user = new User(Users.Count + 1, email, phone: null, displayName: "Rizvi", gender: null, status, DateTimeOffset.UtcNow);
        Users.Add(user);
        return user;
    }

    public Task<User?> FindByIdAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Find(user => user.Id == userId));
}

/// <summary>Accounts and passwords in memory, mirroring what the procedures do.</summary>
internal sealed class FakeCredentialRepository : ICredentialRepository
{
    public List<UserCredential> Accounts { get; } = [];

    /// <summary>Users whose sessions were ended by a password change.</summary>
    public List<long> SessionsRevokedFor { get; } = [];

    public UserCredential Add(string email, string? password, UserStatus status = UserStatus.Active, bool mustReset = false)
    {
        var user = new User(Accounts.Count + 1, Email.Parse(email), null, "Rizvi", null, status, DateTimeOffset.UtcNow);
        var account = new UserCredential(user, password is null ? null : FakePasswordHasher.HashOf(password), mustReset);
        Accounts.Add(account);
        return account;
    }

    public Task<(RegistrationOutcome Outcome, long UserId)> AddPendingUserAsync(
        EmailAddress email, string displayName, PasswordHash password, CancellationToken cancellationToken)
    {
        var index = Accounts.FindIndex(account => account.User.Email == email);
        if (index < 0)
        {
            var user = new User(Accounts.Count + 1, email, null, displayName, null, UserStatus.PendingEmail, DateTimeOffset.UtcNow);
            Accounts.Add(new UserCredential(user, password, false));
            return Task.FromResult((RegistrationOutcome.Created, user.Id));
        }

        var existing = Accounts[index];
        if (existing.User.Status != UserStatus.PendingEmail)
        {
            return Task.FromResult((RegistrationOutcome.AlreadyRegistered, existing.User.Id));
        }

        Accounts[index] = existing with { Password = password };
        return Task.FromResult((RegistrationOutcome.ReplacedPending, existing.User.Id));
    }

    public Task<UserCredential?> FindByEmailAsync(EmailAddress email, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.Find(account => account.User.Email == email));

    public Task<UserCredential?> FindByIdAsync(long userId, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.Find(account => account.User.Id == userId));

    public Task SetPasswordAsync(long userId, PasswordHash password, bool revokeSessions, CancellationToken cancellationToken)
    {
        var index = Accounts.FindIndex(account => account.User.Id == userId);
        var existing = Accounts[index];
        Accounts[index] = new UserCredential(
            existing.User.Status == UserStatus.PendingEmail ? WithStatus(existing.User, UserStatus.Active) : existing.User,
            password,
            MustReset: false);

        if (revokeSessions)
        {
            SessionsRevokedFor.Add(userId);
        }

        return Task.CompletedTask;
    }

    public Task ConfirmEmailAsync(long userId, CancellationToken cancellationToken)
    {
        var index = Accounts.FindIndex(account => account.User.Id == userId);
        if (Accounts[index].User.Status == UserStatus.PendingEmail)
        {
            Accounts[index] = Accounts[index] with { User = WithStatus(Accounts[index].User, UserStatus.Active) };
        }

        return Task.CompletedTask;
    }

    public UserCredential Get(string email) => Accounts.Single(account => account.User.Email == Email.Parse(email));

    private static User WithStatus(User user, UserStatus status) =>
        new(user.Id, user.Email, user.Phone, user.DisplayName, user.Gender, status, user.Created);
}

/// <summary>
/// Not a real hash: "hash:" + password, with the iteration count recorded. Fast and readable,
/// and lets tests check that the right password was stored and that upgrades happen.
/// </summary>
internal sealed class FakePasswordHasher(int iterations = 600_000) : IPasswordHasher
{
    public int DummyChecks { get; private set; }

    public static PasswordHash HashOf(string password, int iterations = 600_000) =>
        new(System.Text.Encoding.UTF8.GetBytes("hash:" + password), [1, 2, 3], iterations);

    public PasswordHash Hash(string password) => HashOf(password, iterations);

    public bool Verify(string password, PasswordHash stored) =>
        System.Text.Encoding.UTF8.GetBytes("hash:" + password).AsSpan().SequenceEqual(stored.Hash);

    public void VerifyAgainstNothing(string password) => DummyChecks++;

    public bool NeedsRehash(PasswordHash stored) => stored.Iterations < iterations;
}

/// <summary>The database throttle, in memory: pauses after the given number of failures.</summary>
internal sealed class FakeSignInThrottle(FakeClock clock, int maxFailures = 5) : ISignInThrottle
{
    public Dictionary<EmailAddress, int> Failures { get; } = [];

    public Dictionary<EmailAddress, DateTimeOffset> PausedUntil { get; } = [];

    public Task<DateTimeOffset?> PausedUntilAsync(EmailAddress email, CancellationToken cancellationToken) =>
        Task.FromResult(PausedUntil.TryGetValue(email, out var until) && until > clock.UtcNow ? until : (DateTimeOffset?)null);

    public Task<DateTimeOffset?> RegisterFailureAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        Failures[email] = Failures.GetValueOrDefault(email) + 1;
        if (Failures[email] >= maxFailures)
        {
            PausedUntil[email] = clock.UtcNow.AddMinutes(15);
            return Task.FromResult<DateTimeOffset?>(PausedUntil[email]);
        }

        return Task.FromResult<DateTimeOffset?>(null);
    }

    public Task ClearAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        Failures.Remove(email);
        PausedUntil.Remove(email);
        return Task.CompletedTask;
    }
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
    public Task SendOtpAsync(EmailAddress email, string code, OtpPurpose purpose, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The SMTP server rejected the credentials.");

    public Task SendNoticeAsync(EmailAddress email, AccountNotice notice, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The SMTP server rejected the credentials.");
}
