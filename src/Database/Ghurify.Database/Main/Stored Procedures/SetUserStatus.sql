-- An admin suspends, reactivates or closes an account. Anything but Active also revokes every
-- refresh token in the same transaction, so the person is signed out everywhere within one
-- access-token lifetime. Only Active, Suspended and Deactivated may be set here; an account
-- waiting for email confirmation is confirmed by its owner, not by an admin.
--
-- @Result 0 = changed, 1 = no such account, 2 = it already had that status,
--         3 = it is the last active super admin, so suspending it would lock everyone out.
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

    -- Taking the last active super admin out of action would leave nobody who can grant the
    -- role back. Checked under the lock taken above, so two admins cannot each suspend the
    -- other's last one at the same moment. 1 = Active.
    IF @Status <> 1
      AND EXISTS (SELECT 1
                  FROM  [Main].[UserStaffRole] AS [h]
                  JOIN  [Main].[StaffRole]     AS [s] ON [s].[Id] = [h].[StaffRoleId] AND [s].[Archived] = 0
                  WHERE [h].[UserId] = @UserId AND [h].[Archived] = 0 AND [s].[IsSuperAdmin] = 1)
      AND NOT EXISTS (SELECT 1
                      FROM  [Main].[UserStaffRole] AS [h]
                      JOIN  [Main].[StaffRole]     AS [s] ON [s].[Id] = [h].[StaffRoleId] AND [s].[Archived] = 0
                      JOIN  [Main].[User]          AS [u] ON [u].[Id] = [h].[UserId]
                                                         AND [u].[Archived] = 0
                                                         AND [u].[Status] = 1
                      WHERE [h].[Archived] = 0
                        AND [s].[IsSuperAdmin] = 1
                        AND [h].[UserId] <> @UserId)
    BEGIN
        SET @Result = 3;
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
