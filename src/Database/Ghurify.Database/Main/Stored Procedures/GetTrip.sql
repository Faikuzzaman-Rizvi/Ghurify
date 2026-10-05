-- One trip's public page: the trip with its destination and host, then its cost breakdown,
-- then its day-by-day plan. Three result sets, one round trip.
--
-- Only live trips (Published or Full) are returned; a draft is visible to its host alone,
-- through a separate path. If the trip is hidden, all three sets come back empty, so the
-- caller cannot tell "does not exist" from "not for you".
CREATE PROCEDURE [Main].[GetTrip]
    @Id                BIGINT,
    @IncludeWomenOnly  BIT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @VisibleId BIGINT;

    SELECT @VisibleId = [t].[Id]
    FROM   [Main].[Trip] AS [t]
    WHERE  [t].[Id] = @Id
      AND  [t].[Archived] = 0
      AND  [t].[Status] IN (2, 3)
      AND  (@IncludeWomenOnly = 1 OR [t].[GroupType] <> 2);

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
           [u].[Created]       AS [HostSince]
    FROM   [Main].[Trip]        AS [t]
    JOIN   [Main].[Destination] AS [d] ON [d].[Id] = [t].[DestinationId]
    JOIN   [Main].[User]        AS [u] ON [u].[Id] = [t].[HostId]
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
