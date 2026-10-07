-- An admin suspends, reactivates or closes an account. Anything but Active also revokes every
-- refresh token in the same transaction, so the person is signed out everywhere within one
-- access-token lifetime. Only Active, Suspended and Deactivated may be set here; an account
-- waiting for email confirmation is confirmed by its owner, not by an admin.
--
-- @Result 0 = changed, 1 = no such account, 2 = it already had that status.
CREATE PROCEDURE [Main].[SetUserStatus]
    @UserId   BIGINT,
    @Status   TINYINT,
    @ActorId  BIGINT,
    @Result   TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Result = 1;

    BEGIN TRAN;

    DECLARE @Current TINYINT;
    SELECT @Current = [Status]
    FROM   [Main].[User] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [Id] = @UserId AND [Archived] = 0;

    IF @Current IS NULL
    BEGIN
        COMMIT TRAN;
        RETURN;
    END;

    IF @Current = @Status
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    UPDATE [Main].[User]
    SET    [Status] = @Status, [UpdatedOn] = SYSUTCDATETIME(), [UpdatedId] = @ActorId
    WHERE  [Id] = @UserId;

    IF @Status <> 1
    BEGIN
        UPDATE [Main].[RefreshToken]
        SET    [RevokedOn] = SYSUTCDATETIME(), [UpdatedOn] = SYSUTCDATETIME()
        WHERE  [UserId] = @UserId AND [RevokedOn] IS NULL;
    END;

    SET @Result = 0;
    COMMIT TRAN;
END;
