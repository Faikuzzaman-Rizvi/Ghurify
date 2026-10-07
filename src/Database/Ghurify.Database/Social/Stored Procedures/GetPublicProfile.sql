-- A person's public page: who they are (never contact details), how they are rated, who follows
-- them, the trips they host or hosted, and the reviews they received. Four result sets.
-- @ViewerId (optional) tells whether the viewer follows them.
CREATE PROCEDURE [Social].[GetPublicProfile]
    @UserId    BIGINT,
    @ViewerId  BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [u].[Id]           AS [UserId],
           [u].[DisplayName],
           [p].[Bio],
           [p].[HomeDistrict],
           [u].[Created],
           (SELECT MAX([v].[Level]) FROM [Main].[Verification] AS [v]
            WHERE [v].[UserId] = [u].[Id] AND [v].[Status] = 2 AND [v].[Archived] = 0) AS [VerifiedLevel],
           CAST(CASE WHEN EXISTS (SELECT 1 FROM [Main].[UserRole] AS [r]
                                  WHERE [r].[UserId] = [u].[Id] AND [r].[Role] = 2 AND [r].[Archived] = 0)
                     THEN 1 ELSE 0 END AS BIT) AS [IsHost],
           (SELECT COUNT(1) FROM [Social].[Follow] AS [f] WHERE [f].[FolloweeId] = [u].[Id] AND [f].[Archived] = 0) AS [Followers],
           (SELECT COUNT(1) FROM [Social].[Follow] AS [f] WHERE [f].[FollowerId] = [u].[Id] AND [f].[Archived] = 0) AS [Following],
           CAST(CASE WHEN EXISTS (SELECT 1 FROM [Social].[Follow] AS [f]
                                  WHERE [f].[FollowerId] = @ViewerId AND [f].[FolloweeId] = [u].[Id] AND [f].[Archived] = 0)
                     THEN 1 ELSE 0 END AS BIT) AS [FollowedByMe],
           ISNULL([s].[AsHostCount], 0)     AS [AsHostCount],
           [s].[AsHostAverage],
           ISNULL([s].[AsTravelerCount], 0) AS [AsTravelerCount],
           [s].[AsTravelerAverage]
    FROM      [Main].[User]            AS [u]
    LEFT JOIN [Main].[UserProfile]     AS [p] ON [p].[UserId] = [u].[Id] AND [p].[Archived] = 0
    LEFT JOIN [Social].[RatingSummary] AS [s] ON [s].[UserId] = [u].[Id]
    WHERE     [u].[Id] = @UserId
      AND     [u].[Archived] = 0
      AND     [u].[Status] = 1;

    SELECT   TOP (12)
             [t].[Id], [t].[Title], [d].[Slug] AS [DestinationSlug], [d].[Name] AS [DestinationName],
             [d].[NameBn] AS [DestinationNameBn], [t].[StartDate], [t].[EndDate], [t].[Status]
    FROM     [Main].[Trip]        AS [t]
    JOIN     [Main].[Destination] AS [d] ON [d].[Id] = [t].[DestinationId]
    WHERE    [t].[HostId] = @UserId
      AND    [t].[Status] IN (2, 3, 5)
      AND    [t].[Archived] = 0
    ORDER BY [t].[StartDate] DESC;

    SELECT   TOP (20)
             [r].[Id], [r].[TripId], [t].[Title] AS [TripTitle], [r].[ReviewerId], [u].[DisplayName] AS [ReviewerName],
             [r].[Direction], [r].[Rating], [r].[Body], [r].[Created]
    FROM     [Social].[Review] AS [r]
    JOIN     [Main].[Trip]     AS [t] ON [t].[Id] = [r].[TripId]
    JOIN     [Main].[User]     AS [u] ON [u].[Id] = [r].[ReviewerId]
    WHERE    [r].[RevieweeId] = @UserId
      AND    [r].[Archived] = 0
    ORDER BY [r].[Id] DESC;
END;
