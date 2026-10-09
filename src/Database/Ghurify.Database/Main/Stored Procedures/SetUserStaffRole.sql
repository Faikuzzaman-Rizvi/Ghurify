-- Puts somebody on the admin desk, or takes them off it: grants or revokes one staff role.
--
-- The guard that matters is here, inside the transaction, rather than in the use case: a
-- deployment must never be left with nobody who can get back in. Two super admins revoking each
-- other at the same moment would both pass a check made before the write, so the count is taken
-- under a lock that holds until the commit.
--
-- @Result 0 = changed, 1 = no such account, 2 = no such role, 3 = it was already like that,
--         4 = that would leave the platform with no super admin.
CREATE PROCEDURE [Main].[SetUserStaffRole]
    @UserId      BIGINT,
    @StaffRoleId BIGINT,
    @Grant       BIT,
    @ActorId     BIGINT,
    @Result      TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Result = 1;

    BEGIN TRAN;

    IF NOT EXISTS (SELECT 1 FROM [Main].[User] WHERE [Id] = @UserId AND [Archived] = 0)
    BEGIN
        COMMIT TRAN;
        RETURN;
    END;

    DECLARE @IsSuperAdmin BIT;

    SELECT @IsSuperAdmin = [IsSuperAdmin]
    FROM   [Main].[StaffRole] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [Id] = @StaffRoleId AND [Archived] = 0;

    IF @IsSuperAdmin IS NULL
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    DECLARE @HeldId BIGINT;

    SELECT @HeldId = [Id]
    FROM   [Main].[UserStaffRole] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [UserId] = @UserId AND [StaffRoleId] = @StaffRoleId AND [Archived] = 0;

    IF @Grant = 1
    BEGIN
        IF @HeldId IS NOT NULL
        BEGIN
            SET @Result = 3;
            COMMIT TRAN;
            RETURN;
        END;

        INSERT INTO [Main].[UserStaffRole] ([UserId], [StaffRoleId], [GrantedById], [UpdatedId])
        VALUES (@UserId, @StaffRoleId, @ActorId, @ActorId);
    END
    ELSE
    BEGIN
        IF @HeldId IS NULL
        BEGIN
            SET @Result = 3;
            COMMIT TRAN;
            RETURN;
        END;

        -- The last way in must stay open.
        IF @IsSuperAdmin = 1
          AND NOT EXISTS (SELECT 1
                          FROM  [Main].[UserStaffRole] AS [h]
                          JOIN  [Main].[User]          AS [u] ON [u].[Id] = [h].[UserId]
                                                             AND [u].[Archived] = 0
                                                             -- 1 = Active: a suspended super
                                                             -- admin is not a way back in.
                                                             AND [u].[Status] = 1
                          WHERE [h].[StaffRoleId] = @StaffRoleId
                            AND [h].[Archived] = 0
                            AND [h].[UserId] <> @UserId)
        BEGIN
            SET @Result = 4;
            COMMIT TRAN;
            RETURN;
        END;

        UPDATE [Main].[UserStaffRole]
        SET    [Archived]  = 1,
               [UpdatedOn] = SYSUTCDATETIME(),
               [UpdatedId] = @ActorId
        WHERE  [Id] = @HeldId;
    END;

    SET @Result = 0;
    COMMIT TRAN;
END;
