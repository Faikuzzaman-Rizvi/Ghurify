-- Revokes every live token in a family, in one set-based update.
--
-- Used for signing out, and for shutting down a session after a replayed token is detected.
-- Returns how many tokens were revoked so the caller can log the size of the blast radius.
CREATE PROCEDURE [Main].[SetRefreshTokenFamilyRevoked]
    @FamilyId UNIQUEIDENTIFIER,
    @Now      DATETIME2 (0),
    @Revoked  INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE [Main].[RefreshToken]
    SET    [RevokedOn] = @Now,
           [UpdatedOn] = SYSUTCDATETIME()
    WHERE  [FamilyId] = @FamilyId
      AND  [RevokedOn] IS NULL
      AND  [Archived] = 0;

    SET @Revoked = @@ROWCOUNT;
END;
