-- Creates a staff role, or edits one, replacing its permissions with @Permissions in the same
-- transaction. @Id NULL (or 0) creates; anything else edits that role.
--
-- What it refuses, so no caller can get it wrong:
--   * a key already used by another live role;
--   * renaming the key of a system role, which the seed script and the console command match on;
--   * touching the super-admin role at all, which holds every permission by definition.
-- The caller must separately refuse permissions the actor does not hold itself, which it can do
-- and this procedure cannot.
--
-- Permissions are reconciled rather than deleted and re-inserted, so re-saving a role unchanged
-- leaves its rows (and their Created dates) alone.
--
-- @Result 0 = saved, 1 = no such role, 2 = that key is taken, 3 = the key of a system role
--         cannot change, 4 = the super-admin role cannot be edited.
CREATE PROCEDURE [Main].[SetStaffRole]
    @Id            BIGINT          = NULL,
    @Key           VARCHAR (40),
    @Name          NVARCHAR (60),
    @NameBn        NVARCHAR (60),
    @Description   NVARCHAR (300)  = NULL,
    @DescriptionBn NVARCHAR (300)  = NULL,
    @Permissions   [Main].[PermissionList] READONLY,
    @ActorId       BIGINT,
    @Result        TINYINT         OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Result = 0;
    IF @Id = 0 SET @Id = NULL;

    BEGIN TRAN;

    -- Serialize against another save of the same key, so two creates cannot both pass the
    -- duplicate check and leave the unique index to fail one of them.
    IF EXISTS (SELECT 1
               FROM  [Main].[StaffRole] WITH (UPDLOCK, HOLDLOCK)
               WHERE [Key] = @Key
                 AND [Archived] = 0
                 AND (@Id IS NULL OR [Id] <> @Id))
    BEGIN
        SET @Result = 2;
        COMMIT TRAN;
        RETURN;
    END;

    IF @Id IS NULL
    BEGIN
        INSERT INTO [Main].[StaffRole]
               ([Key], [Name], [NameBn], [Description], [DescriptionBn], [UpdatedId])
        VALUES (@Key, @Name, @NameBn, @Description, @DescriptionBn, @ActorId);

        SET @Id = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        DECLARE @IsSystem BIT, @IsSuperAdmin BIT, @CurrentKey VARCHAR (40);

        SELECT @IsSystem     = [IsSystem],
               @IsSuperAdmin = [IsSuperAdmin],
               @CurrentKey   = [Key]
        FROM   [Main].[StaffRole] WITH (UPDLOCK, HOLDLOCK)
        WHERE  [Id] = @Id AND [Archived] = 0;

        IF @CurrentKey IS NULL
        BEGIN
            SET @Result = 1;
            COMMIT TRAN;
            RETURN;
        END;

        IF @IsSuperAdmin = 1
        BEGIN
            SET @Result = 4;
            COMMIT TRAN;
            RETURN;
        END;

        IF @IsSystem = 1 AND @CurrentKey <> @Key
        BEGIN
            SET @Result = 3;
            COMMIT TRAN;
            RETURN;
        END;

        UPDATE [Main].[StaffRole]
        SET    [Key]           = @Key,
               [Name]          = @Name,
               [NameBn]        = @NameBn,
               [Description]   = @Description,
               [DescriptionBn] = @DescriptionBn,
               [UpdatedOn]     = SYSUTCDATETIME(),
               [UpdatedId]     = @ActorId
        WHERE  [Id] = @Id;
    END;

    -- Revoke what is no longer wanted.
    UPDATE [p]
    SET    [p].[Archived]  = 1,
           [p].[UpdatedOn] = SYSUTCDATETIME(),
           [p].[UpdatedId] = @ActorId
    FROM   [Main].[StaffRolePermission] AS [p]
    WHERE  [p].[StaffRoleId] = @Id
      AND  [p].[Archived] = 0
      AND  NOT EXISTS (SELECT 1 FROM @Permissions AS [w] WHERE [w].[Permission] = [p].[Permission]);

    -- Grant what is missing. A permission granted, revoked and granted again gets a new row
    -- rather than reviving the archived one: after several rounds there would be more than one
    -- to revive, and the unique index would (rightly) refuse them. The archived rows stay as
    -- the record of what this role could do when.
    INSERT INTO [Main].[StaffRolePermission] ([StaffRoleId], [Permission], [UpdatedId])
    SELECT @Id, [w].[Permission], @ActorId
    FROM   @Permissions AS [w]
    WHERE  NOT EXISTS (SELECT 1
                       FROM  [Main].[StaffRolePermission] AS [p]
                       WHERE [p].[StaffRoleId] = @Id
                         AND [p].[Permission] = [w].[Permission]
                         AND [p].[Archived] = 0);

    COMMIT TRAN;

    SELECT @Id AS [Id];
END;
