-- The signed-in user's own profile: the account, the profile row (if saved yet), the roles held,
-- the strongest identity check passed, and (for staff) what they may do on the admin desk.
-- Four result sets: the profile, the platform roles, the admin permissions, then the admin
-- roles held (so the portal can name the job this person does).
--
-- The permissions are what the web app hides unusable buttons and sections with. They are a
-- convenience for the UI only: every call is checked again by the API, which reads them itself
-- rather than trusting anything the browser sends.
--
-- Callers pass the signed-in user's own id; there is no way to read someone else's through this.
CREATE PROCEDURE [Main].[GetUserProfile]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [u].[Id]                      AS [UserId],
           [u].[Email],
           [u].[DisplayName],
           [u].[Gender],
           [u].[Phone],
           [p].[Bio],
           [p].[HomeDistrict],
           [p].[EmergencyContactName],
           [p].[EmergencyContactPhone],
           [u].[Created],
           (SELECT MAX([v].[Level])
            FROM   [Main].[Verification] AS [v]
            WHERE  [v].[UserId] = [u].[Id]
              AND  [v].[Status] = 2
              AND  [v].[Archived] = 0) AS [VerifiedLevel],
           -- A version only while there is a picture: a removed one keeps its timestamp.
           IIF([p].[AvatarBlob] IS NULL, NULL, [p].[AvatarUpdatedOn]) AS [AvatarUpdatedOn],
           CAST(IIF(EXISTS (SELECT 1
                            FROM [Main].[UserStaffRole] AS [h]
                            JOIN [Main].[StaffRole]     AS [s] ON [s].[Id] = [h].[StaffRoleId]
                                                              AND [s].[Archived] = 0
                            WHERE [h].[UserId] = [u].[Id]
                              AND [h].[Archived] = 0
                              AND [s].[IsSuperAdmin] = 1), 1, 0) AS BIT) AS [IsSuperAdmin]
    FROM      [Main].[User]        AS [u]
    LEFT JOIN [Main].[UserProfile] AS [p] ON [p].[UserId] = [u].[Id] AND [p].[Archived] = 0
    WHERE     [u].[Id] = @UserId
      AND     [u].[Archived] = 0;

    SELECT   [r].[Role]
    FROM     [Main].[UserRole] AS [r]
    WHERE    [r].[UserId] = @UserId
      AND    [r].[Archived] = 0
    ORDER BY [r].[Role];

    -- DISTINCT because two staff roles may both grant the same permission. Empty for a super
    -- admin, who holds every permission implicitly; the flag above tells the web app that.
    SELECT DISTINCT [p].[Permission]
    FROM   [Main].[UserStaffRole]       AS [h]
    JOIN   [Main].[StaffRole]           AS [s] ON [s].[Id] = [h].[StaffRoleId] AND [s].[Archived] = 0
    JOIN   [Main].[StaffRolePermission] AS [p] ON [p].[StaffRoleId] = [s].[Id] AND [p].[Archived] = 0
    WHERE  [h].[UserId] = @UserId
      AND  [h].[Archived] = 0;

    SELECT   [s].[Key], [s].[Name], [s].[NameBn]
    FROM     [Main].[UserStaffRole] AS [h]
    JOIN     [Main].[StaffRole]     AS [s] ON [s].[Id] = [h].[StaffRoleId] AND [s].[Archived] = 0
    WHERE    [h].[UserId] = @UserId
      AND    [h].[Archived] = 0
    ORDER BY [s].[IsSuperAdmin] DESC, [s].[Name] ASC;
END;
