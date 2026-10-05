-- Every destination, with how many live trips start there on or after @FromDate and the
-- cheapest of them, for the destination cards and the search filter.
CREATE PROCEDURE [Main].[QueryDestinations]
    @FromDate          DATE,
    @IncludeWomenOnly  BIT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT   [d].[Id],
             [d].[Slug],
             [d].[Name],
             [d].[NameBn],
             [d].[Division],
             [d].[DivisionBn],
             [d].[Summary],
             [d].[SummaryBn],
             [d].[Kind],
             [d].[Status],
             [d].[StatusNote],
             [d].[StatusNoteBn],
             [d].[Location].[Lat]             AS [Latitude],
             [d].[Location].[Long]            AS [Longitude],
             ISNULL([upcoming].[TripCount], 0) AS [UpcomingTrips],
             [upcoming].[FromPrice]
    FROM     [Main].[Destination] AS [d]
    OUTER APPLY
    (
        SELECT COUNT(1)                 AS [TripCount],
               MIN([t].[PricePerPerson]) AS [FromPrice]
        FROM   [Main].[Trip] AS [t]
        WHERE  [t].[DestinationId] = [d].[Id]
          AND  [t].[Archived] = 0
          AND  [t].[Status] IN (2, 3)
          AND  [t].[StartDate] >= @FromDate
          AND  (@IncludeWomenOnly = 1 OR [t].[GroupType] <> 2)
    ) AS [upcoming]
    WHERE    [d].[Archived] = 0
    ORDER BY ISNULL([upcoming].[TripCount], 0) DESC, [d].[Name] ASC;
END;
