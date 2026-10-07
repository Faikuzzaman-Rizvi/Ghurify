-- Counts one wrong password for an address (by its keyed hash) and pauses sign-in for it once
-- @MaxFailures is reached inside @WindowMinutes. Returns the state after the update.
--
-- Read-modify-write under one lock: counted in the application, two guesses arriving together
-- would both read "4 failures" and an attacker would get extra tries.
CREATE PROCEDURE [Main].[SetSignInFailure]
    @EmailHash      VARBINARY (32),
    @MaxFailures    TINYINT,
    @WindowMinutes  INT,
    @LockMinutes    INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2 (0) = SYSUTCDATETIME();
    DECLARE @Id BIGINT, @Failures TINYINT, @WindowStart DATETIME2 (0), @LockedUntil DATETIME2 (0);

    BEGIN TRAN;

    SELECT @Id          = [Id],
           @Failures    = [Failures],
           @WindowStart = [WindowStart],
           @LockedUntil = [LockedUntil]
    FROM   [Main].[SignInThrottle] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [EmailHash] = @EmailHash;

    IF @LockedUntil > @Now
    BEGIN
        -- Already paused: a guess during the pause neither counts nor extends it.
        SELECT @Failures AS [Failures], @LockedUntil AS [LockedUntil];
        COMMIT TRAN;
        RETURN;
    END;

    -- No earlier run, a run that has gone stale, or a pause that has ended: start again.
    IF @Id IS NULL OR @WindowStart < DATEADD(MINUTE, -@WindowMinutes, @Now) OR @LockedUntil IS NOT NULL
    BEGIN
        SET @Failures = 1;
        SET @WindowStart = @Now;
    END
    ELSE
    BEGIN
        SET @Failures = CASE WHEN @Failures >= 250 THEN 250 ELSE @Failures + 1 END;
    END;

    SET @LockedUntil = CASE WHEN @Failures >= @MaxFailures THEN DATEADD(MINUTE, @LockMinutes, @Now) END;

    IF @Id IS NULL
    BEGIN
        INSERT INTO [Main].[SignInThrottle] ([EmailHash], [Failures], [WindowStart], [LockedUntil])
        VALUES (@EmailHash, @Failures, @WindowStart, @LockedUntil);
    END
    ELSE
    BEGIN
        UPDATE [Main].[SignInThrottle]
        SET    [Failures]    = @Failures,
               [WindowStart] = @WindowStart,
               [LockedUntil] = @LockedUntil,
               [UpdatedOn]   = SYSUTCDATETIME()
        WHERE  [Id] = @Id;
    END;

    SELECT @Failures AS [Failures], @LockedUntil AS [LockedUntil];

    COMMIT TRAN;
END;
