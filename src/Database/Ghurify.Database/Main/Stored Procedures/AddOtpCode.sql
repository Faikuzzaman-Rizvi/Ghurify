-- Issues a new OTP, enforcing the per-address send limit in the same transaction as the insert.
--
-- The count and the insert must be atomic: two requests arriving together would otherwise both
-- see "2 sent so far" and both insert, letting a caller exceed the limit by racing it.
--
-- @Result 0 = code stored, 1 = rate limit reached and nothing was written.
CREATE PROCEDURE [Main].[AddOtpCode]
    @Email         NVARCHAR (256),
    @CodeHash      VARBINARY (32),
    @Purpose       TINYINT,
    @ExpiresOn     DATETIME2 (0),
    @WindowStart   DATETIME2 (0),
    @MaxPerWindow  TINYINT,
    @Id            BIGINT  OUTPUT,
    @Result        TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Id = NULL;
    SET @Result = 0;

    BEGIN TRAN;

    DECLARE @SentInWindow INT;

    SELECT @SentInWindow = COUNT(1)
    FROM   [Main].[OtpCode] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [Email] = @Email
      AND  [Created] >= @WindowStart
      AND  [Archived] = 0;

    IF @SentInWindow >= @MaxPerWindow
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    -- Any earlier live code for this address and purpose is retired, so only the newest one
    -- can be used. The send limit above counts every purpose: it caps mail to one inbox.
    UPDATE [Main].[OtpCode]
    SET    [ConsumedOn] = SYSUTCDATETIME(),
           [UpdatedOn]  = SYSUTCDATETIME()
    WHERE  [Email] = @Email
      AND  [Purpose] = @Purpose
      AND  [ConsumedOn] IS NULL
      AND  [LockedOn] IS NULL
      AND  [Archived] = 0;

    INSERT INTO [Main].[OtpCode] ([Email], [CodeHash], [Purpose], [ExpiresOn])
    VALUES (@Email, @CodeHash, @Purpose, @ExpiresOn);

    SET @Id = SCOPE_IDENTITY();

    COMMIT TRAN;
END;
