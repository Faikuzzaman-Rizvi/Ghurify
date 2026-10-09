-- What an account may do: its status and gender, whether it is a super admin, the platform roles
-- it holds, the strongest identity check it has passed, and the admin permissions its staff
-- roles add up to.
--
-- Read on every authorized request, so it touches only narrow indexes
-- (IX_UserStaffRole_UserId and UX_StaffRolePermission_RoleId_Permission carry the staff joins).
--
-- Three result sets: the account (empty if it does not exist), its platform roles, then its
-- admin permissions (empty for everyone who is not staff, and for a super admin, who holds
-- every permission implicitly rather than by row).
CREATE PROCEDURE [Main].[GetUserAccess]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [u].[Id]      AS [UserId],
           [u].[Status],
           [u].[Gender],
           (SELECT MAX([v].[Level])
            FROM   [Main].[Verification] AS [v]
            WHERE  [v].[UserId] = [u].[Id]
              AND  [v].[Status] = 2
              AND  [v].[Archived] = 0) AS [VerifiedLevel],
           CAST(IIF(EXISTS (SELECT 1
                            FROM [Main].[UserStaffRole] AS [h]
                            JOIN [Main].[StaffRole]     AS [s] ON [s].[Id] = [h].[StaffRoleId]
                                                              AND [s].[Archived] = 0
                            WHERE [h].[UserId] = [u].[Id]
                              AND [h].[Archived] = 0
                              AND [s].[IsSuperAdmin] = 1), 1, 0) AS BIT) AS [IsSuperAdmin]
    FROM   [Main].[User] AS [u]
    WHERE  [u].[Id] = @UserId
      AND  [u].[Archived] = 0;

    SELECT [r].[Role]
    FROM   [Main].[UserRole] AS [r]
    WHERE  [r].[UserId] = @UserId
      AND  [r].[Archived] = 0;

    -- DISTINCT because two staff roles may both grant the same permission.
    SELECT DISTINCT [p].[Permission]
    FROM   [Main].[UserStaffRole]       AS [h]
    JOIN   [Main].[StaffRole]           AS [s] ON [s].[Id] = [h].[StaffRoleId] AND [s].[Archived] = 0
    JOIN   [Main].[StaffRolePermission] AS [p] ON [p].[StaffRoleId] = [s].[Id] AND [p].[Archived] = 0
    WHERE  [h].[UserId] = @UserId
      AND  [h].[Archived] = 0;
END;
