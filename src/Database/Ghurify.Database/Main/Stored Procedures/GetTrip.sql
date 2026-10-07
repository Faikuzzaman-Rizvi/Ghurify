-- One trip's public page: the trip with its destination and host, then its cost breakdown,
-- then its day-by-day plan. Three result sets, one round trip.
--
-- Live trips (Published or Full) are returned to everyone; any other state only to its own host
-- (@ViewerId), who needs to see a draft to edit it. If the trip is hidden, all three sets come
-- back empty, so the caller cannot tell "does not exist" from "not for you".
CREATE PROCEDURE [Main].[GetTrip]
    @Id                BIGINT,
    @IncludeWomenOnly  BIT,
    @ViewerId          BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @VisibleId BIGINT;

    SELECT @VisibleId = [t].[Id]
    FROM   [Main].[Trip] AS [t]
    WHERE  [t].[Id] = @Id
      AND  [t].[Archived] = 0
      AND  (([t].[Status] IN (2, 3) AND (@IncludeWomenOnly = 1 OR [t].[GroupType] <> 2))
            OR [t].[HostId] = @ViewerId);

    SELECT [t].[Id],
           [t].[Title],
           [t].[Summary],
           [d].[Slug]          AS [DestinationSlug],
           [d].[Name]          AS [DestinationName],
           [d].[NameBn]        AS [DestinationNameBn],
           [d].[Kind]          AS [DestinationKind],
           [d].[Status]        AS [DestinationStatus],
           [d].[StatusNote]    AS [DestinationStatusNote],
           [d].[StatusNoteBn]  AS [DestinationStatusNoteBn],
           [t].[StartDate],
           [t].[EndDate],
           [t].[MeetingPoint],
           [t].[Seats],
           [t].[SeatsTaken],
           [t].[PricePerPerson],
           [t].[GroupType],
           [t].[Status],
           [u].[Id]            AS [HostId],
           [u].[DisplayName]   AS [HostName],
           [u].[Created]       AS [HostSince],
           (SELECT MAX([v].[Level])
            FROM   [Main].[Verification] AS [v]
            WHERE  [v].[UserId] = [u].[Id]
              AND  [v].[Status] = 2
              AND  [v].[Archived] = 0) AS [HostVerifiedLevel],
           ISNULL([mix].[Women], 0)  AS [WomenGoing],
           ISNULL([mix].[Men], 0)    AS [MenGoing],
           ISNULL([mix].[Others], 0) AS [OthersGoing]
    FROM   [Main].[Trip]        AS [t]
    JOIN   [Main].[Destination] AS [d] ON [d].[Id] = [t].[DestinationId]
    JOIN   [Main].[User]        AS [u] ON [u].[Id] = [t].[HostId]
    -- Group mix: who holds a seat (held or paid), by gender. Counts only, never names, so a
    -- traveller can judge the group without the page exposing who is in it.
    OUTER APPLY
    (
        SELECT SUM(CASE WHEN [tu].[Gender] = 1 THEN 1 ELSE 0 END)                      AS [Women],
               SUM(CASE WHEN [tu].[Gender] = 2 THEN 1 ELSE 0 END)                      AS [Men],
               SUM(CASE WHEN [tu].[Gender] IS NULL OR [tu].[Gender] NOT IN (1, 2) THEN 1 ELSE 0 END) AS [Others]
        FROM   [Pay].[Booking] AS [b]
        JOIN   [Main].[User]   AS [tu] ON [tu].[Id] = [b].[UserId]
        WHERE  [b].[TripId] = [t].[Id]
          AND  [b].[Status] IN (1, 2)
          AND  [b].[Archived] = 0
    ) AS [mix]
    WHERE  [t].[Id] = @VisibleId;

    SELECT   [Category],
             [Description],
             [Amount]
    FROM     [Main].[TripCostItem]
    WHERE    [TripId] = @VisibleId
      AND    [Archived] = 0
    ORDER BY [SortOrder] ASC, [Id] ASC;

    SELECT   [DayNo],
             [Title],
             [Details],
             [Difficulty]
    FROM     [Main].[ItineraryDay]
    WHERE    [TripId] = @VisibleId
      AND    [Archived] = 0
    ORDER BY [DayNo] ASC;
END;
