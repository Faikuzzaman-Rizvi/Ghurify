-- Archives a staff role and the permissions attached to it.
--
-- Refuses a system role (the four the platform ships with: deleting them would leave a
-- deployment with no way back in) and any role somebody still holds, so access is never removed
-- from a person as a side effect of tidying up the roles list.
--
-- @Result 0 = archived, 1 = no such role, 2 = system roles cannot be deleted,
--         3 = somebody still holds it (@MemberCount says how many).
CREATE PROCEDURE [Main].[DelStaffRole]
    @Id          BIGINT,
    @ActorId     BIGINT,
    @MemberCount INT     OUTPUT,
    @Result      TINYINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Result = 1;
    SET @MemberCount = 0;

    BEGIN TRAN;

    DECLARE @IsSystem BIT;

    SELECT @IsSystem = [IsSystem]
    FROM   [Main].[StaffRole] WITH (UPDLOCK, HOLDLOCK)
    WHERE  [Id] = @Id AND [Archived] = 0;

    IF @IsSystem IS NULL
    BEGIN
        COMMIT TRAN;
        RETURN;
    END;

    IF @IsSystem = 1
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    SELECT @MemberCount = COUNT(1)
    FROM   [Main].[UserStaffRole]
    WHERE  [StaffRoleId] = @Id AND [Archived] = 0;

    IF @MemberCount > 0
    BEGIN
        SET @Result = 3;
        COMMIT TRAN;
        RETURN;
    END;

    UPDATE [Main].[StaffRole]
    SET    [Archived]  = 1,
           [UpdatedOn] = SYSUTCDATETIME(),
           [UpdatedId] = @ActorId
    WHERE  [Id] = @Id;

    UPDATE [Main].[StaffRolePermission]
    SET    [Archived]  = 1,
           [UpdatedOn] = SYSUTCDATETIME(),
           [UpdatedId] = @ActorId
    WHERE  [StaffRoleId] = @Id AND [Archived] = 0;

    SET @Result = 0;
    COMMIT TRAN;
END;
