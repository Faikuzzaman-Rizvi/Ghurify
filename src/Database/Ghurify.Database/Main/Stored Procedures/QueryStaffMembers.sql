-- Everybody on the admin desk, for the staff screen: one row per person, newest grant first,
-- then one row per (person, role) so the caller can show each person's roles.
--
-- Deliberately not paged: a platform has tens of staff, not thousands, and the screen is more
-- useful as one list. If that ever stops being true it gets the same page/total treatment as
-- [Main].[QueryAdminUsers].
CREATE PROCEDURE [Main].[QueryStaffMembers]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT    [u].[Id],
              [u].[Email],
              [u].[DisplayName],
              [u].[Status],
              -- A version only while there is a picture: a removed one keeps its timestamp.
              IIF([p].[AvatarBlob] IS NULL, NULL, [p].[AvatarUpdatedOn]) AS [AvatarUpdatedOn],
              MIN([h].[Created]) AS [StaffSince]
    FROM      [Main].[UserStaffRole] AS [h]
    JOIN      [Main].[User]          AS [u] ON [u].[Id] = [h].[UserId] AND [u].[Archived] = 0
    LEFT JOIN [Main].[UserProfile]   AS [p] ON [p].[UserId] = [u].[Id] AND [p].[Archived] = 0
    WHERE     [h].[Archived] = 0
    GROUP BY  [u].[Id], [u].[Email], [u].[DisplayName], [u].[Status],
              IIF([p].[AvatarBlob] IS NULL, NULL, [p].[AvatarUpdatedOn])
    ORDER BY  [StaffSince] ASC, [u].[Id] ASC;

    SELECT   [h].[UserId],
             [s].[Id]   AS [StaffRoleId],
             [s].[Key],
             [s].[Name],
             [s].[NameBn],
             [s].[IsSuperAdmin],
             [h].[Created] AS [GrantedOn],
             [h].[GrantedById],
             [g].[DisplayName] AS [GrantedByName]
    FROM      [Main].[UserStaffRole] AS [h]
    JOIN      [Main].[StaffRole]     AS [s] ON [s].[Id] = [h].[StaffRoleId] AND [s].[Archived] = 0
    JOIN      [Main].[User]          AS [u] ON [u].[Id] = [h].[UserId] AND [u].[Archived] = 0
    LEFT JOIN [Main].[User]          AS [g] ON [g].[Id] = [h].[GrantedById]
    WHERE     [h].[Archived] = 0
    ORDER BY  [h].[UserId] ASC, [s].[IsSuperAdmin] DESC, [s].[Name] ASC;
END;
