-- An admin requires a new password (for example, the account may be compromised): the current
-- password stops working for sign-in, and every session ends. The owner sets a new password with
-- "forgot password", which proves they still hold the email address.
--
-- An account with no password yet already has to go through that flow; only its sessions end.
CREATE PROCEDURE [Main].[SetPasswordResetRequired]
    @UserId   BIGINT,
    @ActorId  BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    UPDATE [Main].[UserCredential]
    SET    [MustReset] = 1, [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @ActorId
    WHERE  [UserId] = @UserId;

    UPDATE [Main].[RefreshToken]
    SET    [RevokedOn] = SYSUTCDATETIME(), [UpdatedOn] = SYSUTCDATETIME()
    WHERE  [UserId] = @UserId AND [RevokedOn] IS NULL;

    COMMIT TRAN;
END;
