-- One person's travel map. Four result sets:
--   1. who they are, and whether they share the map (no row: no such active person);
--   2. their visits, newest first, with the place (a Ghurify destination, or their own named
--      point) and the trip each one came from;
--   3. photos: from their own stories about the places they visited (at most @PhotosPerPlace per
--      place), and, for the owner only, the photos they put on each visit themselves;
--   4. their confirmed trips still to come (as host or paid traveller).
--
-- @IncludePrivate = 1 is the owner looking at their own map: everything. 0 is anyone else: nothing
-- unless the map is shared, and then only visits to Ghurify destinations, with no notes, no photos
-- of their own and no trips to come. A self-added pin could be someone's home, a photo can show
-- who was there, and a future trip says where someone will be and when.
CREATE PROCEDURE [Main].[QueryTravelMap]
    @UserId          BIGINT,
    @IncludePrivate  BIT,
    @Today           DATE,
    @PhotosPerPlace  INT = 8
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Shared BIT =
    (
        SELECT ISNULL([p].[ShareTravelMap], 0)
        FROM   [Main].[User]             AS [u]
        LEFT JOIN [Main].[UserProfile]   AS [p] ON [p].[UserId] = [u].[Id] AND [p].[Archived] = 0
        WHERE  [u].[Id] = @UserId
          AND  [u].[Archived] = 0
          AND  [u].[Status] = 1
    );

    DECLARE @Visible BIT = CASE WHEN @IncludePrivate = 1 OR @Shared = 1 THEN 1 ELSE 0 END;

    SELECT [u].[Id] AS [UserId], [u].[DisplayName], @Shared AS [Shared]
    FROM   [Main].[User] AS [u]
    WHERE  [u].[Id] = @UserId
      AND  @Shared IS NOT NULL;

    SELECT   [v].[Id],
             [v].[Source],
             [v].[VisitedOn],
             CASE WHEN @IncludePrivate = 1 THEN [v].[Note] END AS [Note],
             [d].[Slug]                                        AS [DestinationSlug],
             [d].[Name]                                        AS [DestinationName],
             [d].[NameBn]                                      AS [DestinationNameBn],
             [d].[Division]                                    AS [DestinationDivision],
             [d].[Kind]                                        AS [DestinationKind],
             [v].[PlaceName],
             [v].[Division]                                    AS [PlaceDivision],
             ISNULL([d].[Location].[Lat], [v].[Location].[Lat])   AS [Latitude],
             ISNULL([d].[Location].[Long], [v].[Location].[Long]) AS [Longitude],
             [t].[Id]                                          AS [TripId],
             [t].[Title]                                       AS [TripTitle],
             [t].[StartDate]                                   AS [TripStartDate],
             [t].[EndDate]                                     AS [TripEndDate],
             [h].[DisplayName]                                 AS [HostName],
             CAST(CASE WHEN [t].[HostId] = @UserId THEN 1 ELSE 0 END AS BIT) AS [AsHost],
             -- Their photos still being checked: the page waits for them.
             CASE WHEN @IncludePrivate = 1
                  THEN (SELECT COUNT(1)
                        FROM   [Social].[Media] AS [m]
                        WHERE  [m].[VisitId] = [v].[Id]
                          AND  [m].[Status] = 2
                          AND  [m].[Archived] = 0)
                  ELSE 0
             END                                               AS [PhotosProcessing]
    FROM     [Main].[Visit]       AS [v]
    LEFT JOIN [Main].[Destination] AS [d] ON [d].[Id] = [v].[DestinationId]
    LEFT JOIN [Main].[Trip]        AS [t] ON [t].[Id] = [v].[TripId]
    LEFT JOIN [Main].[User]        AS [h] ON [h].[Id] = [t].[HostId]
    WHERE    [v].[UserId] = @UserId
      AND    [v].[Archived] = 0
      AND    @Visible = 1
      AND    (@IncludePrivate = 1 OR [v].[DestinationId] IS NOT NULL)
    ORDER BY [v].[VisitedOn] DESC, [v].[Id] DESC;

    -- Their story photos about each visited destination: posts naming the place, or the trip.
    WITH [places] AS
    (
        SELECT DISTINCT [v].[DestinationId]
        FROM   [Main].[Visit] AS [v]
        WHERE  [v].[UserId] = @UserId
          AND  [v].[Archived] = 0
          AND  [v].[DestinationId] IS NOT NULL
          AND  @Visible = 1
    ),
    [photos] AS
    (
        SELECT [d].[Slug] AS [DestinationSlug],
               [m].[Id]   AS [MediaId],
               [p].[Id]   AS [PostId],
               [m].[Kind],
               [m].[ProcessedBlob],
               ROW_NUMBER() OVER (PARTITION BY [d].[Id] ORDER BY [m].[Id] DESC) AS [Rank]
        FROM   [Social].[Post]      AS [p]
        JOIN   [Social].[Media]     AS [m] ON [m].[PostId] = [p].[Id]
        LEFT JOIN [Main].[Trip]     AS [t] ON [t].[Id] = [p].[TripId]
        JOIN   [Main].[Destination] AS [d] ON [d].[Id] = ISNULL([p].[DestinationId], [t].[DestinationId])
        JOIN   [places]             AS [pl] ON [pl].[DestinationId] = [d].[Id]
        WHERE  [p].[AuthorId] = @UserId
          AND  [p].[Archived] = 0
          AND  [p].[Status] = 1
          AND  [m].[Status] = 3
          AND  [m].[Archived] = 0
          AND  [m].[ProcessedBlob] IS NOT NULL
    )
    SELECT   [DestinationSlug], CAST(NULL AS BIGINT) AS [VisitId], [MediaId], [PostId], [Kind], [ProcessedBlob]
    FROM     [photos]
    WHERE    [Rank] <= @PhotosPerPlace
    UNION ALL
    -- The photos they put on their visits: the owner's view only.
    SELECT   NULL, [m].[VisitId], [m].[Id], NULL, [m].[Kind], [m].[ProcessedBlob]
    FROM     [Main].[Visit]   AS [v]
    JOIN     [Social].[Media] AS [m] ON [m].[VisitId] = [v].[Id]
    WHERE    @IncludePrivate = 1
      AND    [v].[UserId] = @UserId
      AND    [v].[Archived] = 0
      AND    [m].[OwnerId] = @UserId
      AND    [m].[Status] = 3
      AND    [m].[Archived] = 0
      AND    [m].[ProcessedBlob] IS NOT NULL
    ORDER BY [MediaId] DESC;

    -- Trips to come: as the host, or with a paid seat. Two index seeks rather than one scan.
    SELECT   [t].[Id] AS [TripId], [t].[Title], [t].[StartDate], [t].[EndDate], [up].[AsHost],
             [d].[Slug] AS [DestinationSlug], [d].[Name] AS [DestinationName], [d].[NameBn] AS [DestinationNameBn],
             [d].[Location].[Lat] AS [Latitude], [d].[Location].[Long] AS [Longitude]
    FROM
    (
        SELECT [t].[Id] AS [TripId], CAST(1 AS BIT) AS [AsHost]
        FROM   [Main].[Trip] AS [t]
        WHERE  [t].[HostId] = @UserId
        UNION
        SELECT [b].[TripId], CAST(0 AS BIT)
        FROM   [Pay].[Booking] AS [b]
        WHERE  [b].[UserId] = @UserId
          AND  [b].[Status] = 2
    ) AS [up]
    JOIN     [Main].[Trip]        AS [t] ON [t].[Id] = [up].[TripId]
    JOIN     [Main].[Destination] AS [d] ON [d].[Id] = [t].[DestinationId]
    WHERE    @IncludePrivate = 1
      AND    @Shared IS NOT NULL
      AND    [t].[Archived] = 0
      AND    [t].[Status] IN (2, 3)
      AND    [t].[EndDate] >= @Today
    ORDER BY [t].[StartDate], [t].[Id];
END;
