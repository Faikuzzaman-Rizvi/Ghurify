-- Posts, newest first, a page at a time (keyset on Id, older than @BeforeId).
--
-- With @AuthorId: one person's posts (their profile).
-- Without:        the viewer's feed: posts by people they follow, their own, and every destination
--                 story (a post about a destination is public by nature).
-- Hidden and archived posts never appear. Two result sets: the posts, then their media (Ready
-- only: nothing still being checked or stripped of location data is ever shown).
CREATE PROCEDURE [Social].[QueryPosts]
    @ViewerId  BIGINT = NULL,
    @AuthorId  BIGINT = NULL,
    @BeforeId  BIGINT = NULL,
    @Take      INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Page TABLE ([Id] BIGINT NOT NULL PRIMARY KEY);

    INSERT INTO @Page ([Id])
    SELECT TOP (@Take) [p].[Id]
    FROM   [Social].[Post] AS [p]
    WHERE  [p].[Archived] = 0
      AND  [p].[Status] = 1
      AND  (@BeforeId IS NULL OR [p].[Id] < @BeforeId)
      AND  ((@AuthorId IS NOT NULL AND [p].[AuthorId] = @AuthorId)
            OR (@AuthorId IS NULL
                AND ([p].[AuthorId] = @ViewerId
                     OR [p].[DestinationId] IS NOT NULL
                     OR EXISTS (SELECT 1
                                FROM   [Social].[Follow] AS [f]
                                WHERE  [f].[FollowerId] = @ViewerId
                                  AND  [f].[FolloweeId] = [p].[AuthorId]
                                  AND  [f].[Archived] = 0))))
    ORDER BY [p].[Id] DESC;

    SELECT   [p].[Id],
             [p].[AuthorId],
             [u].[DisplayName]  AS [AuthorName],
             (SELECT MAX([v].[Level]) FROM [Main].[Verification] AS [v]
              WHERE [v].[UserId] = [u].[Id] AND [v].[Status] = 2 AND [v].[Archived] = 0) AS [AuthorVerifiedLevel],
             [p].[Body],
             [d].[Slug]         AS [DestinationSlug],
             [d].[Name]         AS [DestinationName],
             [d].[NameBn]       AS [DestinationNameBn],
             [p].[TripId],
             (SELECT COUNT(1) FROM [Social].[Like] AS [l] WHERE [l].[PostId] = [p].[Id] AND [l].[Archived] = 0) AS [Likes],
             (SELECT COUNT(1) FROM [Social].[Comment] AS [c] WHERE [c].[PostId] = [p].[Id] AND [c].[Archived] = 0) AS [Comments],
             CAST(CASE WHEN EXISTS (SELECT 1 FROM [Social].[Like] AS [l]
                                    WHERE [l].[PostId] = [p].[Id] AND [l].[UserId] = @ViewerId AND [l].[Archived] = 0)
                       THEN 1 ELSE 0 END AS BIT) AS [LikedByMe],
             [p].[Created]
    FROM     @Page                AS [page]
    JOIN     [Social].[Post]      AS [p] ON [p].[Id] = [page].[Id]
    JOIN     [Main].[User]        AS [u] ON [u].[Id] = [p].[AuthorId]
    LEFT JOIN [Main].[Destination] AS [d] ON [d].[Id] = [p].[DestinationId]
    ORDER BY [p].[Id] DESC;

    SELECT   [m].[Id],
             [m].[PostId],
             [m].[Kind],
             [m].[ContentType],
             [m].[ProcessedBlob]
    FROM     [Social].[Media] AS [m]
    JOIN     @Page            AS [page] ON [page].[Id] = [m].[PostId]
    WHERE    [m].[Status] = 3
      AND    [m].[Archived] = 0
    ORDER BY [m].[PostId], [m].[Id];
END;
