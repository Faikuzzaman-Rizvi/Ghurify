-- Everything the admin desk needs about one person, in one round trip:
--   1. the account and profile (no password hash, no ID numbers);
--   2. their roles;
--   3. their identity checks, newest first, with how many photos each has;
--   4. the trips they host, newest first (20);
--   5. their bookings, newest first (20);
--   6. counts: reports about them that are open, and in total.
CREATE PROCEDURE [Main].[GetAdminUser]
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [u].[Id],
           [u].[Email],
           [u].[DisplayName],
           [u].[Phone],
           [u].[Gender],
           [u].[Status],
           [u].[Created],
           [p].[HomeDistrict],
           [p].[EmergencyContactName],
           [p].[EmergencyContactPhone],
           -- A version only while there is a picture: a removed one keeps its timestamp.
           IIF([p].[AvatarBlob] IS NULL, NULL, [p].[AvatarUpdatedOn]) AS [AvatarUpdatedOn],
           CAST(CASE WHEN [c].[Id] IS NULL THEN 0 ELSE 1 END AS BIT) AS [HasPassword],
           CAST(ISNULL([c].[MustReset], 0) AS BIT)                    AS [MustResetPassword]
    FROM      [Main].[User]           AS [u]
    LEFT JOIN [Main].[UserProfile]    AS [p] ON [p].[UserId] = [u].[Id] AND [p].[Archived] = 0
    LEFT JOIN [Main].[UserCredential] AS [c] ON [c].[UserId] = [u].[Id] AND [c].[Archived] = 0
    WHERE     [u].[Id] = @UserId
      AND     [u].[Archived] = 0;

    SELECT   [Role]
    FROM     [Main].[UserRole]
    WHERE    [UserId] = @UserId AND [Archived] = 0
    ORDER BY [Role];

    SELECT   [v].[Id],
             [v].[Level],
             [v].[Status],
             [v].[IdType],
             [v].[Provider],
             [v].[Reason],
             [v].[Created],
             [v].[ReviewedOn],
             (SELECT COUNT(1) FROM [Main].[VerificationDocument] AS [d]
              WHERE [d].[VerificationId] = [v].[Id]) AS [DocumentCount]
    FROM     [Main].[Verification] AS [v]
    WHERE    [v].[UserId] = @UserId AND [v].[Archived] = 0
    ORDER BY [v].[Id] DESC;

    SELECT TOP (20)
             [t].[Id],
             [t].[Title],
             [t].[Status],
             [t].[StartDate],
             [t].[Seats],
             [t].[SeatsTaken]
    FROM     [Main].[Trip] AS [t]
    WHERE    [t].[HostId] = @UserId AND [t].[Archived] = 0
    ORDER BY [t].[Id] DESC;

    SELECT TOP (20)
             [b].[Id],
             [b].[TripId],
             [t].[Title] AS [TripTitle],
             [b].[Status],
             [b].[Amount],
             [b].[Created]
    FROM     [Pay].[Booking] AS [b]
    JOIN     [Main].[Trip]   AS [t] ON [t].[Id] = [b].[TripId]
    WHERE    [b].[UserId] = @UserId AND [b].[Archived] = 0
    ORDER BY [b].[Id] DESC;

    SELECT (SELECT COUNT(1) FROM [Safety].[Report] AS [r]
            WHERE [r].[Kind] = 1 AND [r].[TargetId] = @UserId AND [r].[Status] = 1 AND [r].[Archived] = 0) AS [OpenReports],
           (SELECT COUNT(1) FROM [Safety].[Report] AS [r]
            WHERE [r].[Kind] = 1 AND [r].[TargetId] = @UserId AND [r].[Archived] = 0)                    AS [TotalReports];
END;
