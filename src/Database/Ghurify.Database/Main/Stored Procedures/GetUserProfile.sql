-- The signed-in user's own profile: the account, the profile row (if saved yet), the roles held,
-- and the strongest identity check passed. Two result sets: the profile, then the roles.
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
           [p].[AvatarUpdatedOn]
    FROM      [Main].[User]        AS [u]
    LEFT JOIN [Main].[UserProfile] AS [p] ON [p].[UserId] = [u].[Id] AND [p].[Archived] = 0
    WHERE     [u].[Id] = @UserId
      AND     [u].[Archived] = 0;

    SELECT   [r].[Role]
    FROM     [Main].[UserRole] AS [r]
    WHERE    [r].[UserId] = @UserId
      AND    [r].[Archived] = 0
    ORDER BY [r].[Role];
END;
