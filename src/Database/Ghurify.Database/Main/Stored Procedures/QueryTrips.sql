-- Trip search: live trips (Published or Full) starting on or after @FromDate, filtered,
-- sorted and paged in one round trip.
--
-- @IncludeWomenOnly is decided by the caller from who is asking: women-only trips are hidden
-- from people who cannot join them, rather than shown and then refused.
-- @VerifiedHostsOnly keeps trips whose host still holds the Host role and a passed selfie check
-- (publishing required both; this catches a host whose verification was later withdrawn).
-- @Sort 0 = soonest first, 1 = cheapest first, 2 = most expensive first.
-- TotalCount repeats on every row so paging needs no second query.
--
-- OPTION (RECOMPILE): every filter is optional, and a plan cached for "no filters" is a poor
-- plan for "one destination, one week". Search is not hot enough for the compile to matter.
CREATE PROCEDURE [Main].[QueryTrips]
    @FromDate          DATE,
    @ToDate            DATE            = NULL,
    @DestinationSlug   VARCHAR (60)    = NULL,
    @MaxPrice          DECIMAL (18, 2) = NULL,
    @GroupType         TINYINT         = NULL,
    @MinSeats          SMALLINT        = NULL,
    @IncludeWomenOnly  BIT,
    @VerifiedHostsOnly BIT             = 0,
    @Sort              TINYINT         = 0,
    @Offset            INT,
    @PageSize          INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   [t].[Id],
             [t].[Title],
             [d].[Slug]          AS [DestinationSlug],
             [d].[Name]          AS [DestinationName],
             [d].[NameBn]        AS [DestinationNameBn],
             [d].[Kind]          AS [DestinationKind],
             [d].[Status]        AS [DestinationStatus],
             [t].[StartDate],
             [t].[EndDate],
             [t].[Seats],
             [t].[SeatsTaken],
             [t].[PricePerPerson],
             [t].[GroupType],
             [t].[Status],
             [u].[DisplayName]   AS [HostName],
             [hv].[Level]        AS [HostVerifiedLevel],
             COUNT(1) OVER ()    AS [TotalCount]
    FROM     [Main].[Trip]        AS [t]
    JOIN     [Main].[Destination] AS [d] ON [d].[Id] = [t].[DestinationId]
    JOIN     [Main].[User]        AS [u] ON [u].[Id] = [t].[HostId]
    OUTER APPLY
    (
        SELECT MAX([v].[Level]) AS [Level]
        FROM   [Main].[Verification] AS [v]
        WHERE  [v].[UserId] = [t].[HostId]
          AND  [v].[Status] = 2
          AND  [v].[Archived] = 0
    ) AS [hv]
    WHERE    [t].[Archived] = 0
      AND    [d].[Archived] = 0
      AND    [u].[Archived] = 0
      AND    [t].[Status] IN (2, 3)
      AND    [t].[StartDate] >= @FromDate
      AND    (@ToDate IS NULL OR [t].[StartDate] <= @ToDate)
      AND    (@DestinationSlug IS NULL OR [d].[Slug] = @DestinationSlug)
      AND    (@MaxPrice IS NULL OR [t].[PricePerPerson] <= @MaxPrice)
      AND    (@GroupType IS NULL OR [t].[GroupType] = @GroupType)
      AND    (@MinSeats IS NULL OR [t].[Seats] - [t].[SeatsTaken] >= @MinSeats)
      AND    (@IncludeWomenOnly = 1 OR [t].[GroupType] <> 2)
      AND    (@VerifiedHostsOnly = 0
              OR ([hv].[Level] = 3
                  AND EXISTS (SELECT 1
                              FROM   [Main].[UserRole] AS [r]
                              WHERE  [r].[UserId] = [t].[HostId]
                                AND  [r].[Role] = 2
                                AND  [r].[Archived] = 0)))
    ORDER BY CASE WHEN @Sort = 1 THEN [t].[PricePerPerson] END ASC,
             CASE WHEN @Sort = 2 THEN [t].[PricePerPerson] END DESC,
             [t].[StartDate] ASC,
             [t].[Id] ASC
    OFFSET   @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END;
