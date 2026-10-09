-- Every live staff role, with how many people hold it, for the roles screen.
--
-- Two result sets: the roles, then one row per (role, permission) so the caller can group them
-- without a second round trip. The super-admin role returns no permission rows: it holds every
-- permission implicitly, and the screen says so rather than listing them.
CREATE PROCEDURE [Main].[QueryStaffRoles]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   [s].[Id],
             [s].[Key],
             [s].[Name],
             [s].[NameBn],
             [s].[Description],
             [s].[DescriptionBn],
             [s].[IsSystem],
             [s].[IsSuperAdmin],
             [s].[Created],
             (SELECT COUNT(1)
              FROM   [Main].[UserStaffRole] AS [h]
              WHERE  [h].[StaffRoleId] = [s].[Id]
                AND  [h].[Archived] = 0) AS [MemberCount]
    FROM     [Main].[StaffRole] AS [s]
    WHERE    [s].[Archived] = 0
    -- Super admin first, then the rest of the system roles, then whatever was added since.
    ORDER BY [s].[IsSuperAdmin] DESC, [s].[IsSystem] DESC, [s].[Name] ASC;

    SELECT   [p].[StaffRoleId],
             [p].[Permission]
    FROM     [Main].[StaffRolePermission] AS [p]
    JOIN     [Main].[StaffRole]           AS [s] ON [s].[Id] = [p].[StaffRoleId] AND [s].[Archived] = 0
    WHERE    [p].[Archived] = 0
    ORDER BY [p].[StaffRoleId] ASC, [p].[Permission] ASC;
END;
