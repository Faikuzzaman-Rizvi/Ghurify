-- Rotates a refresh token: revokes the presented one and issues its replacement in the same
-- family, as one transaction.
--
-- Both writes must commit together. If the revoke committed and the insert did not, the user
-- would be signed out by a successful refresh; if the insert committed and the revoke did not,
-- two live tokens would exist and replay detection would never fire.
--
-- @Result 0 = rotated, 1 = token not found, 2 = already revoked (replay), 3 = expired.
-- On replay the caller is expected to revoke the whole family.
CREATE PROCEDURE [Main].[SetRefreshTokenRotated]
    @OldTokenHash  VARBINARY (32),
    @NewTokenHash  VARBINARY (32),
    @ExpiresOn     DATETIME2 (0),
    @Now           DATETIME2 (0),
    @UserId        BIGINT           OUTPUT,
    @FamilyId      UNIQUEIDENTIFIER OUTPUT,
    @Result        TINYINT          OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @UserId = NULL;
    SET @FamilyId = NULL;
    SET @Result = 1;

    BEGIN TRAN;

    DECLARE @RevokedOn DATETIME2 (0);
    DECLARE @ExistingExpiresOn DATETIME2 (0);

    SELECT @UserId            = [UserId],
           @FamilyId          = [FamilyId],
           @RevokedOn         = [RevokedOn],
           @ExistingExpiresOn = [ExpiresOn]
    FROM   [Main].[RefreshToken] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [TokenHash] = @OldTokenHash
      AND  [Archived] = 0;

    IF @UserId IS NULL
    BEGIN
        SET @Result = 1;
        COMMIT TRAN;
        RETURN;
    END;

    IF @RevokedOn IS NOT NULL
    BEGIN
        -- Presented twice: the first use already rotated it away, so this one is a replay.
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    IF @ExistingExpiresOn <= @Now
    BEGIN
        SET @Result = 3;
        COMMIT TRAN;
        RETURN;
    END;

    UPDATE [Main].[RefreshToken]
    SET    [RevokedOn]      = @Now,
           [ReplacedByHash] = @NewTokenHash,
           [UpdatedOn]      = SYSUTCDATETIME()
    WHERE  [TokenHash] = @OldTokenHash;

    INSERT INTO [Main].[RefreshToken] ([UserId], [TokenHash], [FamilyId], [ExpiresOn])
    VALUES (@UserId, @NewTokenHash, @FamilyId, @ExpiresOn);

    SET @Result = 0;

    COMMIT TRAN;
END;
