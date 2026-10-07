-- Sets (or replaces) an account's password, in one transaction with what must go with it:
--   * an account still waiting for email confirmation becomes Active, because the code that
--     authorised this change was sent to that address;
--   * with @RevokeSessions = 1, every refresh token is revoked, so a reset or change signs the
--     account out on every device (the caller then starts one fresh session);
--   * the "must reset" flag an admin may have set is cleared.
-- A suspended or closed account keeps its status: a password does not lift a suspension.
CREATE PROCEDURE [Main].[SetUserPassword]
    @UserId          BIGINT,
    @PasswordHash    VARBINARY (64),
    @PasswordSalt    VARBINARY (32),
    @Iterations      INT,
    @RevokeSessions  BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    UPDATE [Main].[UserCredential] WITH (UPDLOCK, HOLDLOCK)
    SET    [PasswordHash] = @PasswordHash,
           [PasswordSalt] = @PasswordSalt,
           [Iterations]   = @Iterations,
           [Algorithm]    = 1,
           [MustReset]    = 0,
           [ChangedOn]    = SYSUTCDATETIME(),
           [UpdatedOn]    = SYSUTCDATETIME(),
           [UpdatedId]    = @UserId
    WHERE  [UserId] = @UserId;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [Main].[UserCredential] ([UserId], [PasswordHash], [PasswordSalt], [Iterations], [UpdatedId])
        VALUES (@UserId, @PasswordHash, @PasswordSalt, @Iterations, @UserId);
    END;

    UPDATE [Main].[User]
    SET    [Status]    = 1,
           [UpdatedOn] = SYSUTCDATETIME(),
           [UpdatedId] = @UserId
    WHERE  [Id] = @UserId
      AND  [Status] = 4;

    IF @RevokeSessions = 1
    BEGIN
        UPDATE [Main].[RefreshToken]
        SET    [RevokedOn] = SYSUTCDATETIME(),
               [UpdatedOn] = SYSUTCDATETIME()
        WHERE  [UserId] = @UserId
          AND  [RevokedOn] IS NULL;
    END;

    COMMIT TRAN;
END;
