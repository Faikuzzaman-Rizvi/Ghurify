-- Records one wrong guess against a code and locks it once the limit is reached.
--
-- Read-modify-write in a single statement: counting attempts in the application would let two
-- simultaneous guesses both read "4 attempts" and both proceed, giving an attacker extra tries.
-- The OUTPUT clause returns the state after the update, so the caller never re-reads.
CREATE PROCEDURE [Main].[SetOtpCodeAttempted]
    @Id           BIGINT,
    @MaxAttempts  TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE [Main].[OtpCode]
    SET    [Attempts]  = [Attempts] + 1,
           [LockedOn]  = CASE
                             WHEN [Attempts] + 1 >= @MaxAttempts THEN SYSUTCDATETIME()
                             ELSE [LockedOn]
                         END,
           [UpdatedOn] = SYSUTCDATETIME()
    OUTPUT inserted.[Attempts],
           CAST(CASE WHEN inserted.[LockedOn] IS NULL THEN 0 ELSE 1 END AS BIT) AS [IsLocked]
    WHERE  [Id] = @Id
      AND  [Archived] = 0;
END;
